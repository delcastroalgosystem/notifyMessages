# NotifyMessages

Serviço multi-tenant de envio de notificações (e-mail/SMS) por E-goi, SendGrid, Twilio e Brevo.

- **`NotifyMessages.Api`** recebe os pedidos (`POST /api/v1/notifications`), gere os templates e recebe os webhooks dos provedores.
- **`NotifyMessages.Worker`** lê a fila (`NotifyMsg.MESSAGE_DISPATCH`) e envia.

Os dois partilham a mesma BD, schema `NotifyMsg`.

## API de envio

| Pedido | Resposta |
|---|---|
| `POST /api/v1/notifications` (header `X-Api-Key`) | `202` com `{ message, trackingId }`, em que `trackingId` é o Id do `MESSAGE_DISPATCH`; header `Location` para o `GET` |
| Pedido repetido (mesmo tenant, template, contacto e `BusinessData`) | `409` problem+json com `dispatchId`, o Id do envio já existente |
| `GET /api/v1/notifications/{id}` | `200` com o estado atual (`status`, `statusName`, `externalId`, `retryCount`, `lastError`, `createdAt`, `processedAt`, `events[]`), ou `404` se não existir ou for de outro tenant |

## Configuração

| Chave | Onde | Notas |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | Api e Worker | A **mesma BD** nos dois |
| `ProviderSettings:Global*:*` | Api e Worker | Credenciais usadas pelos tenants sem configuração própria |
| `ProviderSettings:Tenants:<SecretName>:ApiKey` (Twilio: `:AuthToken`) | Api e Worker | Chave da conta própria de um tenant. A BD guarda só `<SecretName>` em `TENANT_PROVIDER_CONFIG.SECRET_NAME` |
| `WebhookSettings:SharedSecret` | Api | Token `?token=` dos webhooks |
| `DevelopmentSeed:Enabled` | Api | `false` por omissão. `true` cria tenants e templates de exemplo em Development, se a BD não tiver tenants. **Nunca ativar numa BD partilhada** |

Em dev, os segredos vão para User Secrets (`dotnet user-secrets set ... --project NotifyMessages.Api` e `--project NotifyMessages.Worker`). Em produção vão para variáveis de ambiente (`ProviderSettings__Tenants__<SecretName>__ApiKey`) ou para um cofre. **Nunca em `appsettings*.json` nem na BD.**

Se um tenant tem `SECRET_NAME` mas o segredo não existe na configuração, o envio **falha**, com erro no `ERROR_LOG`. Nunca cai para as credenciais globais, para não enviar pela conta errada. As colunas `API_KEY`/`AUTH_TOKEN` são legado: só são lidas quando `SECRET_NAME` está vazio, com um aviso no log.

## Registar um tenant

```powershell
.\Scripts\Register-Tenant.ps1 -TenantName "CLUBE_AAC" -ProviderType Egoi -ProviderSecretName "CLUBE_AAC_Egoi" `
    -ProviderDomain "..." -ProviderSenderId "..." -ConnectionString "<connection string da BD>"
```

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
