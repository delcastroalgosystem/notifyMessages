# NotifyMessages

Serviço multi-tenant de envio de notificações (e-mail/SMS) por E-goi, SendGrid, Twilio e Brevo.

- **`NotifyMessages.Api`** recebe os pedidos (`POST /api/v1/notifications`), gere os templates e recebe os webhooks dos provedores.
- **`NotifyMessages.Worker`** lê a fila (`NotifyMsg.MESSAGE_DISPATCH`) e envia.

Os dois partilham a mesma BD, schema `NotifyMsg`.

## API de envio

| Pedido | Resposta |
|---|---|
| `POST /api/v1/notifications` (header `X-Api-Key`) | `202` com `{ message, trackingId }`, em que `trackingId` é o Id do `MESSAGE_DISPATCH`; header `Location` para o `GET`. Campos opcionais: **`externalKey`** (chave de idempotência do cliente, ex. `SOCIO:1234:2026`) e **`scheduledAt`** (UTC; não envia antes) |
| Pedido repetido | `409` problem+json com `dispatchId`, o Id do envio já existente. Com `externalKey`: repetido = mesma chave no mesmo tenant (os dados podem ser diferentes). Sem ela: mesmo tenant, template, contacto e `BusinessData` |
| Template de outro tenant | `400` "Template inválido" — um tenant só usa os seus templates e os partilhados |
| Contacto na lista de supressão do tenant | `202`, mas o envio fica `Suppressed` e nunca é enviado |
| `GET /api/v1/notifications/{id}` | `200` com o estado atual (`status`, `statusName`, `externalId`, `externalKey`, `batchId`, `scheduledAt`, `sentTo`, `retryCount`, `lastError`, `createdAt`, `processedAt`, `events[]`), ou `404` se não existir ou for de outro tenant |
| `/api/v1/templates` | CRUD + `preview`. Um tenant vê os seus e os partilhados (`tenantId` nulo), só altera os seus; os que cria ficam seus |

## API do conector (gatilhos e lotes)

Para quem entrega envios em quantidade: um **conector** (ex. o conector Smartarena, que lê a BD do software do cliente) ou o próprio software do cliente. Mesma autenticação (`X-Api-Key` do tenant).

| Pedido | Resposta |
|---|---|
| `GET /api/v1/triggers/due` | Gatilhos do tenant **devidos agora** na hora local do tenant (`TENANT.TIME_ZONE`): ativos, depois da `START_DATE`, no `SCHEDULE_DAY` (nulo = todos os dias), depois da `SCHEDULE_TIME`, e **ainda sem lote para esse dia**. Cada um traz `triggerId`, `code`, `templateId`, `parameters`, `requiresApproval`, `startDate`, `intervalMinutes` e o **`runDate`** a usar no lote. **Gatilhos de intervalo** (`INTERVAL_MINUTES`, 5 a 1440): devidos quando passaram N minutos desde a última execução entregue (`LAST_RUN_AT`), ignorando dia e hora. Gatilhos `FILE` (carga manual) nunca aparecem. `?at=<UTC>` simula outro instante — **só fora de Production** |
| `POST /api/v1/batches` | Lote: `triggerId` + `runDate` (lote de conector) **ou** `templateId` (lote de API); `referenceDate`; `items[]` (`externalKey` **obrigatória**, `recipientName`, `recipientContact`, `businessData`, `scheduledAt`); `excluded[]` (`reference`, `reason` — quem a origem deixou de fora e porquê); `warnings[]`. `201` com as contagens `received` / `accepted` / `duplicates` / `suppressed` / `rejected`. Gatilho de intervalo: vários lotes por dia (sem `409`); o conector entrega todas as execuções, mesmo vazias, e sem envios novos (vazio ou tudo duplicado) a resposta é **`204`**: a execução fica registada, mas não há lote |
| Mesmo gatilho e `runDate` outra vez | `409` com `batchId` do lote existente (o conector pode repetir sem medo) |
| `GET /api/v1/batches/{id}` | Estado do lote e `progress` por estado de envio (ex. `{ "Sent": 9, "Failed": 1 }`); `details` (JSON: excluídos, avisos, rejeitados, duplicados). `404` se for de outro tenant |

Regras de cada item: inválido (sem chave, sem nome, contacto que não é e-mail nem telefone) → **rejeitado**, com o motivo em `details`; chave repetida no lote ou já usada antes pelo tenant (por lote ou por `POST /notifications`) → **duplicado**, sem novo envio; contacto em supressão → envio `Suppressed`. Os aceites ficam **`Held`** se o gatilho exige aprovação (lote `PendingApproval`; a aprovação é da API de administração) ou **`Queued`** (lote `Approved`). Quando todos os envios terminam, o lote passa a `Completed`; um lote sem nada para enviar nasce `Completed`. Máximo de 10 000 itens por lote.

## API de administração (`/api/v1/admin`)

Para a interface de gestão (hoje a Area `MessageFlow` do Smartarena ExtraTools; no futuro, o portal do NotifyMessages). **Não usa a `X-Api-Key` de um tenant**: usa uma **chave de plataforma** no header `X-Admin-Key` e o header obrigatório **`X-Acting-User`** (quem faz a ação — fica em `CREATED_BY`, `APPROVED_BY`, `UPDATED_BY`). A Api guarda só os hashes em `AdminApi:KeyHashes:<n>` (vários, para trocar a chave sem cortes); gerar com `Scripts\New-AdminKey.ps1`. Uma chave de tenant não entra na administração, e a de plataforma não serve nos endpoints dos tenants.

| Recurso | Endpoints |
|---|---|
| Tenants | `GET /admin/tenants`; `PUT /admin/tenants/{id}/settings` (`sendingEnabled`, `sandboxContact`, `timeZone` IANA) |
| Templates | `GET /admin/templates?tenantId=` (os do tenant + partilhados); `GET/PUT/DELETE /admin/templates/{id}`; `POST /admin/templates` (`tenantId` nulo = partilhado); `POST /admin/templates/{id}/preview` |
| Gatilhos | `GET /admin/triggers?tenantId=`; `GET /admin/triggers/{id}`; `POST /admin/triggers`; `PUT /admin/triggers/{id}` (desativar = `isActive: false`). Validações: tenant existe, template do tenant ou partilhado, nome único no tenant, dia 1–28, `parameters` JSON válido |
| Lotes | `GET /admin/batches?tenantId=&triggerId=&status=&from=&to=&page=&pageSize=`; `GET /admin/batches/{id}`; `GET /admin/batches/{id}/items?status=`; **`POST /admin/batches/{id}/approve`** (`Held` → `Queued`); **`POST /admin/batches/{id}/cancel`** (o que ainda não foi enviado → `Canceled`); **`POST /admin/batches/upload`** (multipart: `tenantId`, `triggerId` ou `templateId`, `file` `.csv`/`.xlsx` até 5 MB — o lote fica sempre à espera de aprovação) |
| Supressões | `GET /admin/suppressions?tenantId=&search=`; `POST /admin/suppressions` (`tenantId`, `contact`, `reason`, `source`; repetido → `200`; os envios `Held`/`Queued` desse contacto passam a `Suppressed`); `DELETE /admin/suppressions/{id}` |
| Histórico | `GET /admin/dispatches?tenantId=&triggerId=&batchId=&status=&from=&to=&search=&page=&pageSize=` — com `countsByStatus` de todo o filtro |

**Ficheiros (CSV `;`/`,`/tab em UTF-8 ou Latin-1, ou Excel `.xlsx`, primeira folha):** cabeçalho na primeira linha; coluna de contacto obrigatória (`email`, `e-mail`, `contacto`, `telefone`, `telemóvel`, `phone`…), `nome` e `chave` opcionais; as outras colunas passam a variáveis do template com o nome do cabeçalho. Linhas sem chave recebem `FILE:{gatilho|template}:{data}:{contacto}` — carregar o mesmo ficheiro no mesmo dia não duplica.

## Templates: variáveis e blocos condicionais

- `{{Nome}}`: substituída pelo valor da variável (no HTML, escapado).
- `{{#Var}} … {{/Var}}`: o conteúdo só aparece quando `Var` tem valor; `{{^Var}} … {{/Var}}`: só quando não tem (ou não existe). Ex. um e-mail com e sem referência Multibanco no mesmo template. Pode haver blocos dentro de blocos de outras variáveis.

## Sandbox e envio por tenant

- **`TENANT.SANDBOX_CONTACT`** preenchido: todos os e-mails do tenant vão para esse endereço (`sentTo` mostra-o; `recipientContact` fica com o destinatário real). SMS em Sandbox é cancelado.
- **`WorkerOptions:ForceSandbox`**: por omissão **ligado em qualquer ambiente que não seja Production**. Com ele ligado, um tenant **sem** `SANDBOX_CONTACT` não envia nada (a mensagem falha com esse motivo) — nunca chega a um destinatário real por engano. Em Production, só o `SANDBOX_CONTACT` do tenant ativa o Sandbox.
- **`TENANT.SENDING_ENABLED = 0`**: o Worker não envia nada desse tenant; as mensagens esperam na fila (`Queued`).

## Configuração

| Chave | Onde | Notas |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | Api e Worker | A **mesma BD** nos dois |
| `ProviderSettings:Global*:*` | Api e Worker | Credenciais usadas pelos tenants sem configuração própria |
| `ProviderSettings:Tenants:<SecretName>:ApiKey` (Twilio: `:AuthToken`) | Api e Worker | Chave da conta própria de um tenant. A BD guarda só `<SecretName>` em `TENANT_PROVIDER_CONFIG.SECRET_NAME` |
| `WebhookSettings:SharedSecret` | Api | Token `?token=` dos webhooks |
| `DevelopmentSeed:Enabled` | Api | `false` por omissão. `true` cria tenants e templates de exemplo em Development, se a BD não tiver tenants. **Nunca ativar numa BD partilhada** |

Em dev, os segredos vão para User Secrets (`dotnet user-secrets set ... --project NotifyMessages.Api` e `--project NotifyMessages.Worker`). Em produção vão para um **`appsettings.Production.json` na pasta publicada** da Api e do Worker, fora do repositório, legível só pela conta que corre o componente e pelos Administradores: `Scripts\New-ProductionSettings.ps1` cria-o com os campos vazios e aplica as permissões (ver o exemplo no próprio script). Variáveis de ambiente (`ProviderSettings__Tenants__<SecretName>__ApiKey`) ou um cofre também funcionam e têm precedência sobre o ficheiro. **Nunca nos `appsettings*.json` do repositório nem na BD.** Numa publicação que apague ficheiros a mais no destino (Web Deploy "Remove additional files"), excluir o `appsettings.Production.json`.

Se um tenant tem `SECRET_NAME` mas o segredo não existe na configuração, o envio **falha**, com erro no `ERROR_LOG`. Nunca cai para as credenciais globais, para não enviar pela conta errada. As colunas `API_KEY`/`AUTH_TOKEN` são legado: só são lidas quando `SECRET_NAME` está vazio, com um aviso no log.

## Registar um tenant

```powershell
.\Scripts\Register-Tenant.ps1 -TenantName "Associação Académica de Coimbra" -RefName "CLUBE_AAC" `
    -SandboxContact "teste@empresa.pt" -ProviderType Egoi -ProviderSecretName "CLUBE_AAC_Egoi" `
    -ProviderDomain "..." -ProviderSenderId "..." -ConnectionString $cs
```

- `-TenantName`: nome real da entidade. `-RefName`: referência de ligação ao software do cliente (única, ex. o `ClubeRef` do Smartarena).
- `-SandboxContact` (opcional): endereço de teste. `-TimeZone` (opcional, `Europe/Lisbon` por omissão): fuso das agendas dos gatilhos.
- `-ConnectionString` é obrigatória: leia-a dos User Secrets para uma variável (`$cs`), nunca a escreva no script.

O script imprime a API Key do tenant (header `X-Api-Key`). Só o hash fica na BD, por isso guarde-a nesse momento. Imprime também os comandos `dotnet user-secrets set` a correr para o segredo do provedor.

## Trocar a chave de um provedor

Não é preciso compilar nem publicar uma nova versão.

1. Gerar a chave nova no painel do provedor.
2. Substituir o valor **com o mesmo nome** (`ProviderSettings:Tenants:<SecretName>:ApiKey`, ou `Global*` para a conta partilhada) na configuração da **Api e do Worker**.
3. Aplicar:
   - **User Secrets ou `appsettings`:** o .NET recarrega os ficheiros sozinho; o próximo envio já usa a chave nova.
   - **Variável de ambiente ou cofre:** reiniciar os serviços Api e Worker. Os envios que chegam entretanto ficam na fila com estado `Queued` e seguem a seguir.
4. Revogar a chave antiga no provedor só depois de um envio de teste com sucesso.

Trocar a API Key **do tenant no NotifyMessages** (`X-Api-Key`) é outro procedimento: gera-se uma chave nova, grava-se o hash em `NotifyMsg.TENANT.API_KEY_HASH` e atualiza-se o cliente (ex. o `Smartarena.MessageFlow.Agent`).

## Base de dados e migrações

```powershell
dotnet ef database update --project NotifyMessages.Infrastructure --startup-project NotifyMessages.Api
```

- Todas as tabelas ficam no schema `NotifyMsg`, **incluindo o histórico de migrações** (`NotifyMsg.__EFMigrationsHistory`). Assim a instalação numa BD partilhada (ex. `SMA_EXTRATOOLS`) não se mistura com o `dbo.__EFMigrationsHistory` da outra aplicação.
- **BD já existente com o histórico em `dbo`** (instalações anteriores a 2026-09-25): antes do próximo `database update`, mover o histórico com

  ```sql
  ALTER SCHEMA NotifyMsg TRANSFER dbo.__EFMigrationsHistory;
  ```

  Só se a BD for exclusiva do NotifyMessages. Numa BD partilhada, copiar apenas as linhas do NotifyMessages.

## Testes

```powershell
dotnet test NotifyMessages.sln
```
