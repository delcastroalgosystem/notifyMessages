# Plano Mestre de Implementação: NotifyMessages

> **Última Atualização:** 25 de Setembro de 2026  
> **Status:** Checklist da Fundação & Robustez (itens 1-21) 100% Concluída e Verificada — **entrega real via SendGrid, Brevo e E-goi confirmada pelo destinatário** (18/09 e 21/09/2026) | **Schema dedicado `NotifyMsg` implementado** (21/09/2026) | **Webhook de eventos de entrega implementado** (21/09/2026 — SendGrid/Brevo/Twilio validados, E-goi por confirmar) | **CRUD + Preview de Templates implementado** (21/09/2026 — validado end-to-end via testes reais na Api) | **Preparação para o MessageFlow do Smartarena ExtraTools** (25/09/2026 — consulta de estado, chaves dos provedores fora da BD, instalação no `SMA_EXTRATOOLS`; ver secção 4.9) | **Próxima Fase (decidida 25/09/2026): plataforma de controlo de comunicações** — gatilhos, lotes (API e CSV/Excel), supressões, templates por tenant, chave externa de idempotência, API de administração (secção 4.10) | Em aberto (não bloqueante): teste real de SMS (Brevo/Twilio/E-goi), teste real de campanhas E-goi/Brevo | Depois: Zenvia, portal/Dashboard  
> **Target Framework:** .NET 9.0 (todos os projetos)

---

## 1. Visão Geral do Negócio
O **NotifyMessages** é uma plataforma "Middleware" (B2B SaaS) que se conecta a softwares clientes (ERPs de Clubes de Futebol, Contabilidade, Imobiliárias, etc.) para automatizar e gerenciar o envio de mensagens transacionais (Cobrança, Boas-vindas, Avisos) via múltiplos canais (Email, SMS, WhatsApp).

* **Diferencial:** Utiliza **Idempotency Key** (Chave de Unicidade SHA-256) para garantir que mensagens nunca sejam duplicadas ou reenviadas por engano, mesmo com falhas de rede no ERP do cliente.
* **Flexibilidade de Multi-tenant:** Suporta configurações globais de provedores ou chaves/credenciais específicas por Tenant (`TENANT_PROVIDER_CONFIG`).
* **Público-Alvo:** Clubes esportivos, Escritórios de Contabilidade e Sistemas Integrados de Gestão (Brasil e Portugal).

---

## 2. Arquitetura Técnica (Clean Architecture)

A solução `NotifyMessages.sln` é organizada em 6 projetos:

1. **`NotifyMessages.Domain`**: Entidades, Enums e Interfaces fundamentais (sem dependências externas).
2. **`NotifyMessages.Application`**: Casos de uso, DTOs, Services, Interfaces (`IAppDbContext`, `IDispatchService`, `IProviderFactory`, `IEmailProvider`, `ISmsProvider`, `ICampaignProvider`, `IWebhookEventService`), Validators (FluentValidation).
3. **`NotifyMessages.Infrastructure`**: EF Core (`AppDbContext`, `DbSeeder`), SQL Server, Provedores externos (E-goi v2/v3, SendGrid, Twilio, Brevo).
4. **`NotifyMessages.Api`**: Endpoints REST, Swagger, Injeção de Dependência, Middleware de exceções, Validação, Webhooks de entrega.
5. **`NotifyMessages.Worker`**: Background Service com processamento concorrente (`SemaphoreSlim`), polling e retry logic com backoff exponencial.
6. **`NotifyMessages.UnitTests`**: Testes unitários (xUnit + EF Core InMemory) de idempotência, `ProviderFactory`, `Worker` e `WebhookEventService`.

```
NotifyMessages.sln
├── NotifyMessages.Domain          (Entidades: MessageDispatch, MessageDispatchEvent, Template, TenantProviderConfig, Tenant)
├── NotifyMessages.Application     (DTOs, Provider Interfaces, DispatchService, WebhookEventService, Validators)
├── NotifyMessages.Infrastructure  (AppDbContext, DbSeeder, Egoi/SendGrid/Twilio/Brevo Providers, ProviderFactory)
├── NotifyMessages.Api             (POST /api/v1/notifications, /api/v1/webhooks/*, Swagger, DI, ExceptionHandlingMiddleware)
├── NotifyMessages.Worker          (Background Service concurrente, SemaphoreSlim, Retry logic)
└── NotifyMessages.UnitTests       (xUnit, EF Core InMemory, testes de idempotência/ProviderFactory/Worker/WebhookEventService)
```

---

## 3. Estado Atual de Implementação

### 3.1 Mapeamento do Banco de Dados & Entidades

> ✅ **Verificado em 14/09/2026** diretamente na base de dados `NotifyMsgDB` (server `WMC-LENOVO`): a migration `20260725165429_InitialCreate` está aplicada (`__EFMigrationsHistory`) e as 4 tabelas abaixo existem fisicamente. Seed de dados de teste aplicado (ver item 15): 3 Tenants, 3 Templates, 2 TenantProviderConfigs.

- **`TENANT`**: Tabela principal de clientes do SaaS (renomeada de `SAAS_TENANT`).
- **`MESSAGE_DISPATCH`**: Fila de mensagens (`TenantId`, `TemplateId`, `IdempotencyKey`, `CurrentStatus`, `RecipientContact`, `ContextDataJson`). *(nome real na migration/DB — a documentação antiga referia `MESSAGEFLOW_DISPATCH`)*
- **`TEMPLATE`**: Templates por tenant com suporte a variáveis dinâmicas e modo de disparo. *(nome real na migration/DB — a documentação antiga referia `MESSAGEFLOW_TEMPLATE`)*
- **`TENANT_PROVIDER_CONFIG`**: Configuração flexível de credenciais (API Keys/Tokens) por tenant e por canal.
- **`MESSAGE_DISPATCH_EVENTS`**: ✅ **Implementada em 21/09/2026** (ver secção 4.7) — histórico/auditoria de eventos de entrega, alimentada pelos webhooks dos provedores. *(nome real — a documentação antiga referia `MESSAGEFLOW_DISPATCH_EVENTS`)*

### 3.2 Provedores de Mensagens Integrados
- **E-goi Email**: API Transacional (v2). ✅ **Validado com envio real** (21/09/2026, conta do tenant SC Covilhã, entrega confirmada).
- **E-goi SMS**: API Transacional (v2). Implementado, envio real ainda não testado.
- **E-goi Campaign**: API Marketing (v3) para envio em lote / campanhas. Implementado, envio real ainda não testado.
- **SendGrid**: API v3 para envio transacional de e-mail com fallback/configuração por tenant. ✅ **Validado com envio real** (18/09/2026, entrega confirmada).
- **Brevo Email**: API v3 transacional. ✅ **Validado com envio real** (21/09/2026, entrega confirmada).
- **Brevo SMS**: API v3 transacional. Implementado, envio real ainda não testado.
- **Twilio**: SMS API para envios transacionais globais. Implementado, envio real ainda não testado.
- **`ProviderFactory`**: Resolução dinâmica em tempo de execução. Busca primeiro credencial ativa do Tenant em `TENANT_PROVIDER_CONFIG`; caso não exista ou não seja específica, utiliza a chave global do `appsettings.json`. **Desde 25/09/2026** a chave do tenant vem da configuração (`ProviderSettings:Tenants:<SECRET_NAME>:ApiKey`), não da BD — ver secção 4.9.

---

## 4. Checklist & Tabela de Tarefas Unificada

### 4.1 Fundação & Infraestrutura Base
| # | Tarefa | Componente | Status |
|---|--------|------------|--------|
| 1 | Entidade `MessageDispatch` e enum `DispatchStatus` | Domain | ✅ Concluído |
| 2 | `DispatchService` com hash SHA-256 e verificação de idempotência | Application | ✅ Concluído |
| 3 | Controller REST `POST /api/v1/notifications` | Api | ✅ Concluído |
| 4 | Criar enums `ChannelType` e `ProviderType` | Domain | ✅ Concluído |
| 5 | Criar DTOs de Provedores (`EmailMessage`, `SmsMessage`, `CampaignRequest`, `ProviderResult`, `ProviderConfig`) | Application | ✅ Concluído |
| 6 | Criar interfaces dos Provedores (`IEmailProvider`, `ISmsProvider`, `ICampaignProvider`, `IProviderFactory`) | Application | ✅ Concluído |
| 7 | Criar entidade `Template` e `TenantProviderConfig` | Domain | ✅ Concluído |
| 8 | Implementar `EgoiEmailProvider`, `EgoiSmsProvider`, `EgoiCampaignProvider` | Infrastructure | ✅ Concluído |
| 9 | Implementar `SendGridEmailProvider` e `TwilioSmsProvider` | Infrastructure | ✅ Concluído |
| 10 | Implementar `ProviderFactory` com fallback global vs tenant | Infrastructure | ✅ Concluído |
| 11 | Atualizar Worker com processamento paralelo (`SemaphoreSlim`), Backoff Exponential e Status Progress | Worker | ✅ Concluído |
| 12 | Registrar todos os Provedores no Container DI (Api e Worker) | Infrastructure / Entrypoints | ✅ Concluído |
| 13 | Renomear tabela `SAAS_TENANT` para `TENANT` no script SQL e modelo | Banco de Dados | ✅ Concluído |

---

### 4.2 Próxima Etapa Imersiva (Fase Atual)

| # | Tarefa | Prioridade | Esforço | Status |
|---|--------|------------|---------|--------|
| 14 | **EF Core Migrations**: Gerar e aplicar migration para `TENANT`, `TEMPLATE` e `TENANT_PROVIDER_CONFIG` | Alta | Baixo | ✅ **Concluído** (verificado na BD em 14/09/2026) |
| 15 | **Dados de Teste (Seed)**: Criar scripts/seeders com Tenants, Templates e TenantProviderConfigs | Alta | Baixo | ✅ **Concluído** — `DbSeeder.SeedAsync` em `NotifyMessages.Infrastructure/Persistence/DbSeeder.cs`, invocado no arranque da Api (apenas em Development, idempotente). Verificado na BD em 14/09/2026: 3 Tenants, 3 Templates, 2 TenantProviderConfigs |
| 16 | **Configuração de Secrets**: Configurar credenciais/API Keys no `appsettings.json` ou User Secrets | Alta | Baixo | ✅ **Concluído** — `UserSecretsId` configurado em `NotifyMessages.Api` e `NotifyMessages.Worker`; a connection string real de dev (com password) foi movida para User Secrets em ambos os projetos e removida do `appsettings.json` (substituída por placeholder `Trusted_Connection`). As API Keys dos provedores (Egoi, SendGrid, Twilio) continuam vazias no `appsettings.json` — ainda não há chaves reais fornecidas; devem ser adicionadas via `dotnet user-secrets set "ProviderSettings:Global<Provider>:ApiKey" "..."` quando disponíveis, nunca commitadas |
| 17 | **Interface `IDispatchService`**: Extrair interface para desacoplamento no DI | Média | Baixo | ✅ **Concluído** — `IDispatchService` criada em `NotifyMessages.Application/Interfaces`, `DispatchService` implementa-a, DI (`NotifyMessages.Api/Program.cs`) e `NotificationController` atualizados para depender da interface |
| 18 | **Validação com FluentValidation**: Adicionar validadores para `DispatchRequestDto` (formato de e-mail, telefone, tenant/template id) | Alta | Baixo | ✅ **Concluído** — `DispatchRequestDtoValidator` em `NotifyMessages.Application/Validators`, registado via `AddValidatorsFromAssemblyContaining` e invocado no `NotificationController` (testado: payload inválido devolve 400 com mensagens por campo) |
| 19 | **Middleware de Tratamento de Erros Global**: Exception middleware na API | Média | Baixo | ✅ **Concluído** — `ExceptionHandlingMiddleware` em `NotifyMessages.Api/Middleware`, registado logo após `app.Build()`; mapeia `DUPLICATE_REQUEST` para 409 e qualquer outra exceção para 500 (`application/problem+json`). Controller simplificado (try/catch removido, delega no middleware). Testado end-to-end: 400 (validação), 202 (sucesso), 409 (duplicado) |
| 20 | **Testes Unitários & Integração**: Criar projeto `NotifyMessages.UnitTests` (Testar idempotência, ProviderFactory, Worker) | Alta | Médio | ✅ **Concluído** — projeto `NotifyMessages.UnitTests` (xUnit + EF Core InMemory) adicionado à solução com 14 testes: `DispatchServiceTests` (idempotência: criação, duplicidade, dados/contactos diferentes), `ProviderFactoryTests` (resolução de provider, fallback tenant→global, tenant inativo/global ignora config específica), `WorkerTests` (fluxo Sent, Failed após esgotar retries, Canceled por template inativo, usando providers falsos). `dotnet test` — 14/14 passam |
| 21 | **Autenticação & Autorização (API Keys / JWT)**: Proteger o endpoint REST por Tenant | Alta | Médio | ✅ **Concluído** — API Key por Tenant. Nova coluna `TENANT.API_KEY_HASH` (migration `AddTenantApiKeyHash`, índice único filtrado), `ApiKeyHasher` (SHA-256) em `NotifyMessages.Application/Security`, `ApiKeyAuthenticationHandler` em `NotifyMessages.Api/Authentication` (lê header `X-Api-Key`, valida hash + Tenant ativo, gera claims). `NotificationController` protegido com `[Authorize]` e valida que o `TenantId` do payload corresponde ao Tenant autenticado (403 caso contrário). `DbSeeder` gera e regista (log) uma API Key por tenant seedado. Testado end-to-end: sem key → 401; key inválida → 401; key válida com TenantId divergente → 403; key válida e TenantId correto → 202 |

---

### 4.3 Correções Identificadas em Testes com Provedores Reais

| # | Correção | Componente | Status |
|---|--------|------------|--------|
| C1 | **Fallback de remetente ausente**: `Worker.BuildEmailMessage`/`BuildSmsMessage`/`BuildCampaignRequest` só liam `Template.SenderId`/`SenderName`, sem cair para `ProviderConfig.SenderId`/`SenderName`/`FromNumber` (tenant ou global) quando o Template não tinha remetente próprio. Causava falha real no SendGrid (`from.email` vazio) mesmo com credenciais corretas. Corrigido: as três funções passaram a receber o `ProviderConfig` resolvido e aplicam fallback `Template → ProviderConfig`. Validado com envio real via SendGrid (18/09/2026, `MessageId` retornado pelo SendGrid) | Worker | ✅ Concluído e validado |
| C2 | **`GlobalSendGrid:FromEmail` com endereço não verificado**: o valor inicial (`corporate@smartfan.tickets`) não existia como Sender verificado no SendGrid — a API aceitava o pedido (retornava `MessageId`) mas o email nunca chegava. Corrigido trocando o secret para `noreply@smartfan.tickets` (Sender verificado). **Entrega confirmada visualmente** pelo destinatário em 18/09/2026 (email "Bem-vindo(a), Wendel!" recebido na caixa de entrada) | Config (User Secrets) | ✅ Concluído e validado — entrega real confirmada |
| C3 | **`EgoiResponse` a ler o campo JSON errado**: o modelo esperava `"id"` na resposta de sucesso do envio de email (`POST /email/messages/action/send`), mas a E-goi devolve `"messageId"` (ex: `[{"messageId":"2"}]`). Resultado: envios bem-sucedidos ficavam com `ExternalId = NULL`, quebrando a correlação de webhooks por `ExternalId` (secção 4.7). Bug pré-existente, só detetável com um envio real bem-sucedido — nunca tínhamos tido um até validar o domínio da SC Covilhã. Corrigido `[JsonPropertyName]` de `"id"` para `"messageId"` em `EgoiResponse.cs` (usado tanto por `EgoiEmailProvider` como por `EgoiSmsProvider` — o SMS ainda não foi validado com envio real, por isso o mesmo mapeamento aí continua por confirmar). Revalidado com envio real: `ExternalId` passou a vir corretamente preenchido (`"3"`) | Infrastructure (`EgoiResponse.cs`) | ✅ Concluído e validado (email); SMS por confirmar |
| C4 | **`Register-Tenant.ps1` a referenciar `TENANT`/`TENANT_PROVIDER_CONFIG` sem o schema `NotifyMsg`**: o script foi escrito antes da migration `AddNotifyMsgSchema` (secção 4.6), que moveu todas as tabelas de `dbo` para `NotifyMsg`. Ao ser reutilizado para o smoke test do item 4.8 (Templates), falhou com `Invalid object name 'TENANT'`. Corrigido para `NotifyMsg.TENANT` e `NotifyMsg.TENANT_PROVIDER_CONFIG`. Impacto real: qualquer novo registo de tenant real (como o da SC Covilhã) feito **depois** da migration de schema já teria falhado da mesma forma — felizmente o registo da SC Covilhã foi feito antes dessa migration, por isso não foi afetado | Scripts (`Register-Tenant.ps1`) | ✅ Concluído e validado (novo registo de tenant de teste em 21/09/2026) |
| C5 | **Worker partilhava um único `AppDbContext` entre envios em paralelo**: `ExecuteAsync` criava um scope por ciclo e as tarefas paralelas (`SemaphoreSlim`, até `MaxDegreeOfParallelism`) usavam todas o mesmo `DbContext`, que não é thread-safe — com mais de uma mensagem no mesmo lote, tendia a falhar ("A second operation was started on this context..."). Os testes existentes só tinham 1 mensagem por lote, por isso não o apanhavam. Corrigido: o ciclo lê só os Ids em fila e **cada mensagem tem o seu próprio scope/`DbContext`**, recarrega a linha (confirmando que ainda está `Queued`) e grava o seu resultado. Novo teste com 8 mensagens em paralelo | Worker | ✅ Concluído e validado (25/09/2026) |
| C6 | **Pedido duplicado em simultâneo devolvia `500`**: a verificação prévia de idempotência (`Any`) não é atómica; dois pedidos idênticos ao mesmo tempo passavam ambos e o segundo batia no índice único `IX_MessageDispatch_IdempotencyKey`, com `DbUpdateException` → `500`. Corrigido: o `DbUpdateException` é apanhado, procura-se o envio existente pela chave e devolve-se `409` | Application (`DispatchService`) | ✅ Concluído (25/09/2026) |

### 4.4 Próximos Passos (v2 - Futuro)
- [x] Integração com Brevo (Email + SMS transacional) — ver secção 4.5
- [ ] Integração com Brevo WhatsApp
- [ ] Integração com Zenvia (WhatsApp)
- [x] Endpoint Webhook para eventos de entrega (`Delivered`, `Read`, `Bounced`) — ver secção 4.7
- [ ] Dashboard de métricas e status de disparos em tempo real — **a integrar no futuro portal do NotifyMessages**, sobre a API de administração (secção 4.10, P9)
- [ ] Gatilhos, lotes (API e CSV/Excel), supressões e templates por tenant — ver secção 4.10
- [x] Editor e preview visual de templates — ver secção 4.8 (API; sem UI web própria por decisão explícita)

---

### 4.5 Integração Brevo (Email + SMS Transacional)

Implementada em 18/09/2026 como alternativa enquanto a E-goi da casa não suporta transacional e a credencial da SC Covilhã ainda está em validação de domínio.

- **`ProviderType.Brevo`** (Email) e **`ProviderType.BrevoSms`** (SMS) — dois valores separados, seguindo o mesmo padrão já usado para `Egoi`/`EgoiCampaign` (permite `TENANT_PROVIDER_CONFIG` e `BaseUrl`/credenciais independentes por canal, mesmo sendo o mesmo fornecedor).
- **`BrevoEmailProvider`** (`IEmailProvider`) — `POST https://api.brevo.com/v3/smtp/email`, header `api-key`.
- **`BrevoSmsProvider`** (`ISmsProvider`) — `POST https://api.brevo.com/v3/transactionalSMS/send`, header `api-key`.
- Config global: `ProviderSettings:GlobalBrevo` (ApiKey/BaseUrl/FromEmail/FromName) e `ProviderSettings:GlobalBrevoSms` (ApiKey/BaseUrl/SenderId) — adicionados vazios ao `appsettings.json` da Api e do Worker; preencher via User Secrets antes de testar.
- Registados no DI (Api e Worker) e no `ProviderFactory.GetGlobalProviderConfig`. **Sem migration necessária** — `ProviderType` é um `int` genérico, sem FK na BD.
- Templates de teste criados: `Id 4` "Notificação Brevo Email" e `Id 5` "Aviso Brevo SMS" (inseridos diretamente na BD viva, e também adicionados ao `DbSeeder` para futuras instalações limpas).
- Cobertura de testes: `ProviderFactoryTests` ganhou 3 casos novos (fallback global Brevo Email, Brevo SMS, resolução correta do `IEmailProvider` por tipo). Suite total: **17/17 passam**.
- **Email validado com envio real** (21/09/2026): Tenant "Imobiliária Exemplo" (sem `TENANT_PROVIDER_CONFIG` próprio, usa a chave global), Template `Id 4`. Brevo respondeu 201 com `MessageId` (`<...@smtp-relay.mailin.fr>`), status final `Sent`. **Entrega confirmada visualmente pelo destinatário.**
- **SMS (`ProviderType.BrevoSms`, Template `Id 5`)**: implementado e coberto por teste unitário, mas **teste de envio real adiado** — validação por Email já foi considerada suficiente para avançar por agora.

---

### 4.6 Decisão de Arquitetura: Multi-instalação & Schema Dedicado ✅ Implementado

**Contexto (20/09/2026):** o NotifyMessages é um produto único que será implantado em **instalações separadas e isoladas por empresa cliente** (ex: Empresa A atende clubes de futebol, Empresa B atende construtoras) — bases de dados e clientes finais completamente distintos por instalação, **sem qualquer partilha de dados entre elas**. O que é comum entre as instalações é apenas o código-fonte/produto, que precisa de manutenção única. Pelo menos uma dessas instalações vai correr dentro de uma base de dados já existente do lado do cliente (`SMAExtraTools`), que contém tabelas de outros softwares não relacionados — logo as tabelas do NotifyMessages precisam de coexistir sem colidir.

**Decisão:** em vez de prefixar nomes de tabela (`NotifyMsg_TENANT`) ou tratar isto caso a caso, adotar um **schema dedicado do SQL Server (`NotifyMsg`) como padrão em TODAS as instalações do produto** — inclusive nas que têm base de dados própria e dedicada (como a atual `NotifyMsgDB` de desenvolvimento).

**Porquê:**
- Um único código/conjunto de migrations que se comporta de forma idêntica em qualquer instalação (suporta o requisito de manutenção única) — sem necessidade de configuração condicional por cliente.
- Elimina risco de colisão de nomes com tabelas de terceiros em qualquer base onde o produto venha a ser instalado (não só a `SMAExtraTools`).
- Permite isolar permissões por schema, se o DBA do lado do cliente quiser restringir acesso.
- Alternativas descartadas: **Linked Server** entre as bases (inadequado — são empresas/clientes diferentes, criaria uma via de acesso fora do controlo de autenticação da aplicação); **prefixo de tabela** (funciona mas suja nomes permanentemente e não dá isolamento de permissões).

**Implementação (concluída em 21/09/2026):**
```csharp
// AppDbContext.OnModelCreating
modelBuilder.HasDefaultSchema("NotifyMsg");
```
Migration `AddNotifyMsgSchema` gerada e aplicada à `NotifyMsgDB` — moveu `TENANT`, `TEMPLATE`, `TENANT_PROVIDER_CONFIG` e `MESSAGE_DISPATCH` de `dbo` para `NotifyMsg` via `ALTER SCHEMA ... TRANSFER` (operação de metadados, sem perda de dados — confirmado: contagens de linhas idênticas antes/depois). `__EFMigrationsHistory` permanece em `dbo` (comportamento padrão do EF Core, sem risco de colisão dado o nome específico).

> **Atualização 25/09/2026:** a premissa "sem risco de colisão" não se confirmou — o `SMA_EXTRATOOLS` já tem um `dbo.__EFMigrationsHistory` (do Smartarena ExtraTools, que também usa EF Core). O histórico do NotifyMessages passou para **`NotifyMsg.__EFMigrationsHistory`** (item M4 da secção 4.9). A `NotifyMsgDB` existente precisa de `ALTER SCHEMA NotifyMsg TRANSFER dbo.__EFMigrationsHistory;` antes do próximo `database update` (ver README).

**Validado:** build completo (0 erros), suite de testes unitários (17/17), e smoke-test real via `POST /api/v1/notifications` contra a base já migrada — leitura/escrita em `NotifyMsg.MESSAGE_DISPATCH` confirmada com sucesso.

---

### 4.7 Endpoint Webhook para Eventos de Entrega (Delivered/Read/Bounced)

Implementado em 21/09/2026. Fecha a lacuna da tabela `MESSAGE_DISPATCH_EVENTS` identificada desde 14/09/2026 (secção 3.1).

**Modelo de dados:**
- Nova tabela **`NotifyMsg.MESSAGE_DISPATCH_EVENTS`** (`ID`, `DISPATCH_ID` FK→`MESSAGE_DISPATCH` com `ON DELETE CASCADE`, `EVENT_TYPE`, `EVENT_DATE`, `PROVIDER_RESPONSE` — payload bruto do provedor, para auditoria/debug) — migration `AddMessageDispatchEvents`.
- Novo valor no enum `DispatchStatus`: **`Bounced = 5`** (distinto de `Failed = 99`, que continua a representar erros internos de processamento).

**Correlação evento → `MessageDispatch`:** cada provedor tem um mecanismo próprio para "carimbar" o nosso `MessageDispatch.Id` na mensagem enviada, para que o evento de callback volte com essa referência:
- **SendGrid**: `custom_args: { dispatch_id }` no envio → devolvido no campo `dispatch_id` de cada evento do webhook.
- **Brevo**: `tags: [dispatchId]` no envio → devolvido em `tags`/`tag` no payload do evento.
- **Twilio**: sem mecanismo de tag; o `dispatchId` vai embutido na própria URL do `StatusCallback` que registamos por mensagem (`.../webhooks/twilio?token=...&dispatchId=123`) — depende de `WebhookSettings:PublicBaseUrl` estar configurado no Worker (URL pública da Api, alcançável pela Twilio).
- **E-goi**: ⚠️ **não implementado por falta de documentação confiável** (ver nota abaixo) — a correlação cai apenas no fallback por `ExternalId`.
- **Fallback universal**: se não houver correlação por Id, tenta casar pelo `ExternalId` (o Id de mensagem devolvido pelo próprio provedor no envio, já guardado em `MessageDispatch.ExternalId`).

**Endpoints** (`NotifyMessages.Api/Controllers/WebhooksController.cs`, `[AllowAnonymous]` — não usam a API Key de tenant, pois quem chama é o provedor):
- `POST /api/v1/webhooks/sendgrid?token=...`
- `POST /api/v1/webhooks/brevo?token=...`
- `POST /api/v1/webhooks/twilio?token=...&dispatchId=...` (form-urlencoded, formato nativo do Twilio)
- `POST /api/v1/webhooks/egoi?token=...`

**Segurança:** token partilhado na query string (`WebhookSettings:SharedSecret`, comparado com `CryptographicOperations.FixedTimeEquals`), decisão consciente em vez de verificação de assinatura criptográfica por provedor (mais forte, mas exigiria implementação específica por provedor — SendGrid suporta, Brevo não tem equivalente nativo). O mesmo segredo tem de estar configurado na Api (para validar) e no Worker (para construir a URL de callback do Twilio).

**Progressão de status:** `WebhookEventService` só avança o status (`Sent < Delivered < Read`), nunca regride; eventos terminais (`Bounced`/`Failed`/`Canceled`) sobrepõem-se sempre, exceto se o dispatch já estiver num estado terminal (nesse caso, o evento é registado mas o status não muda).

**⚠️ E-goi — payload não confirmado:** pesquisei a documentação oficial (`developers.e-goi.com/transactional/v2` e `usecases/webhooks`), mas o site é uma SPA cujo conteúdo real não é exposto a fetch simples — não há confirmação do formato exato de callback por mensagem transacional (o que foi encontrado, `usecases/webhooks/`, documenta o Webhook Events API v3 de marketing, não o callback transacional v2). O handler `/webhooks/egoi` foi implementado de forma defensiva (tenta campos comuns como `action`/`event`, `message_id`/`messageHash`, `customData`/`custom_data`, grava sempre o payload bruto), mas **precisa de validação com um callback real da E-goi** antes de confiar nele em produção.

**Configuração necessária antes de usar em produção:**
1. `WebhookSettings:SharedSecret` — mesmo valor em Api e Worker (User Secrets).
2. `WebhookSettings:PublicBaseUrl` — só no Worker, necessário para o Twilio (URL pública da Api).
3. Registar as URLs de callback nos painéis do SendGrid (Event Webhook) e Brevo (Webhooks transacionais) apontando para `https://<host-publico>/api/v1/webhooks/{provider}?token=...`. Twilio e E-goi não precisam de configuração manual de painel — Twilio recebe a URL por mensagem, E-goi ainda por confirmar.

**Testado:** suite de testes unitários (`WebhookEventServiceTests`, 6 casos: avanço de status, bloqueio de regressão, evento terminal sobrepõe-se, dispatch já terminal ignora novidade, fallback por `ExternalId`, evento sem correspondência não lança exceção) — suite total **23/23 passam**. Smoke-test real contra a Api a correr: token inválido → 401; SendGrid (`delivered`→`Delivered`, depois `open`→`Read`); Twilio (`delivered`→`Delivered` via `dispatchId` na query); Brevo (`hard_bounce`→`Bounced` via `tags`) — todos confirmados na base de dados.

---

### 4.8 Editor e Preview Visual de Templates

Implementado em 21/09/2026. Escopo decidido explicitamente com o utilizador: **só API** (CRUD + preview), sem UI web própria — o projeto continua 100% backend (Api/Worker), sem nenhum front-end até agora, e o preview via API já é suficiente para validar o conteúdo dos templates (o `Subject`/`HtmlBody`/`TextBody` renderizado pode ser inspecionado via Swagger/Postman ou, no futuro, consumido por uma UI própria sem mudanças no backend).

**Lógica de substituição de variáveis unificada:** `Worker.ReplaceVariables` (usada em produção para gerar as mensagens reais) e o novo endpoint de preview usavam a mesma sintaxe `{{Variavel}}` mas eram implementações separadas — risco de divergirem com o tempo. Extraída para `TemplateVariableRenderer` (`NotifyMessages.Application/Services`), estático, com dois métodos: `Render` (substituição, case-insensitive, idêntica ao comportamento anterior do Worker) e `ExtractVariableNames` (regex `\{\{(\w+)\}\}`, deduplica ignorando case) — usado tanto para listar as variáveis de um template (campo `Variables` no `TemplateDto`) como para calcular `MissingVariables` no preview. O `Worker` foi refatorado para delegar nesta classe partilhada, eliminando a duplicação sem alterar o comportamento de envio real.

**Endpoints** (`NotifyMessages.Api/Controllers/TemplatesController.cs`, `[Authorize]` — protegido pela mesma API Key de Tenant; nota: `Template` é uma entidade global/partilhada entre tenants, não por-tenant, por isso qualquer tenant autenticado pode gerir templates, tal como já acontecia implicitamente via seed/BD direta):
- `GET /api/v1/templates?isActive=` — lista (filtro opcional por ativo/inativo).
- `GET /api/v1/templates/{id}` — detalhe (404 se não existir).
- `POST /api/v1/templates` — cria (`TemplateUpsertDtoValidator`: `Name` obrigatório, `Subject`+`HtmlBody` obrigatórios para `Channel=Email`, `TextBody` obrigatório para `Channel=Sms`).
- `PUT /api/v1/templates/{id}` — atualiza (mesma validação; 404 se não existir).
- `DELETE /api/v1/templates/{id}` — **soft delete** (`IsActive = false`, não remove a linha) — consistente com o comportamento já existente no `Worker` (mensagens para template inativo são canceladas, não falham) e evita quebrar o histórico de `MessageDispatch` que referenciam o `TemplateId` (não há FK de integridade referencial entre `MESSAGE_DISPATCH` e `TEMPLATE` no modelo atual).
- `POST /api/v1/templates/{id}/preview` — recebe `{ "variables": { "Nome": "Wendel", ... } }`, devolve `Subject`/`HtmlBody`/`TextBody` já renderizados e `MissingVariables` (variáveis existentes no template que não foram fornecidas, para o consumidor conseguir sinalizar campos por preencher).

**Testado:**
- Unitários: `TemplateServiceTests` (8 casos: criação com extração de variáveis, filtro por `isActive`, get/update/deactivate com id inexistente, update altera `UpdatedAt`, preview substitui e lista `MissingVariables`, preview com id inexistente) e `TemplateVariableRendererTests` (3 casos: substituição case-insensitive, template nulo devolve vazio, extração deduplica ignorando case) — suite total **35/35 passam**.
- Smoke-test real contra a Api a correr (tenant de teste criado e removido no fim): `GET` lista os 5 templates seedados com `Variables` corretas; `POST /preview` com variável parcial devolveu o texto renderizado e `MissingVariables: ["Empresa"]`; `POST` com `Name` vazio devolveu 400 com mensagens por campo; `POST`/`PUT`/`DELETE` (soft) fluxo completo validado; sem `X-Api-Key` → 401.
- **C4** (ver secção 4.3): este smoke-test expôs que `Register-Tenant.ps1` ainda referenciava `TENANT`/`TENANT_PROVIDER_CONFIG` sem o schema `NotifyMsg` — corrigido.

---

### 4.9 Preparação para o MessageFlow do Smartarena ExtraTools ✅ Implementado

Implementado em 25/09/2026, como **tarefa 1.0** do plano da Fase 1 do Smartarena ExtraTools (`C:\Developer\Smartmove\Smartarena\ExtraTools\Docs\Plano de Trabalho - Fase 1 - MessageFlow (Piloto).md`, secções 7 e 7.2). O MessageFlow é o primeiro cliente real do NotifyMessages: um agente central que gera os envios automáticos (aniversário, aviso de quota) dos clubes e os entrega por esta API, um tenant por clube.

**Cópia de segurança antes das alterações** (o projeto não tem controlo de versões): `C:\Developer\DelCastro\NotifyMessages_backup_2026-09-25_antes-tarefa-1.0.7z`.

| # | Tarefa | Componente | Status |
|---|--------|------------|--------|
| M1 | **Consulta de estado de um envio (lacuna N1)**: o `202` devolvia um `Guid.NewGuid()` sem ligação à BD. Agora `IDispatchService.EnqueueMessageAsync` devolve `long` e o `trackingId` do `202` **é o `MESSAGE_DISPATCH.ID`**, com header `Location`. Novo `GET /api/v1/notifications/{id}` (`DispatchStatusDto`: `status`/`statusName`, `externalId`, `retryCount`, `lastError`, `createdAt`, `processedAt`, `events[]`), **isolado por tenant** (envio de outro tenant → `404`, para não revelar que existe) | Application / Api | ✅ Concluído e validado |
| M2 | **`409` com o Id do envio existente**: nova `DuplicateDispatchException` (herda de `InvalidOperationException`, mantém o prefixo `DUPLICATE_REQUEST`) com `ExistingDispatchId`; o `ExceptionHandlingMiddleware` devolve `dispatchId` no corpo problem+json. O cliente trata o `409` como "já enviado" e guarda esse Id | Application / Api | ✅ Concluído e validado |
| M3 | **Chaves dos provedores por tenant fora da BD (lacuna N6, opção A)**: nova coluna `TENANT_PROVIDER_CONFIG.SECRET_NAME` (migration `AddTenantProviderSecretName`). Com `SECRET_NAME` preenchido, o `ProviderFactory` lê `ProviderSettings:Tenants:<SECRET_NAME>:ApiKey` (e `:AuthToken`) da configuração — User Secrets em dev, variáveis de ambiente/cofre em produção, **na Api e no Worker**. Segredo em falta → o envio **falha** com erro claro no `ERROR_LOG`, **nunca cai para as credenciais globais** (evita enviar pela conta errada). `API_KEY`/`AUTH_TOKEN` passam a legado: só lidos com `SECRET_NAME` vazio, com aviso no log. Com várias configurações ativas do mesmo tenant/provedor, ganha a mais recente (antes a escolha era indefinida). **Trocar uma chave não exige nova versão** (ver README) | Domain / Infrastructure | ✅ Concluído e validado |
| M4 | **Instalação numa BD partilhada**: histórico de migrações em **`NotifyMsg.__EFMigrationsHistory`** (`AppDbContext.ConfigureSqlServer`, usado pela Api e pelo Worker) — o `SMA_EXTRATOOLS` já tem o `dbo.__EFMigrationsHistory` do ExtraTools. O **seed de exemplo passa a opt-in** (`DevelopmentSeed:Enabled`, `false` por omissão): antes, a Api em Development criava 3 tenants e 2 credenciais falsas em qualquer BD sem tenants | Infrastructure / Api | ✅ Concluído |
| M5 | **`Register-Tenant.ps1`**: `-ProviderApiKey`/`-ProviderAuthToken` substituídos por `-ProviderSecretName` (obrigatório com `-ProviderType`); a chave do provedor deixa de passar pelo script e de ir para a BD; no fim imprime os comandos `dotnet user-secrets set` (sem valores) para a Api e o Worker | Scripts | ✅ Concluído e validado |
| M6 | **`README.md`** na raiz: API de envio/consulta, configuração, registo de tenant, **procedimento de troca de chave**, migrações e mudança do histórico numa BD existente | Docs | ✅ Concluído |

Correções encontradas durante este trabalho: **C5** (Worker com `DbContext` partilhado entre envios em paralelo) e **C6** (duplicado em simultâneo → `500`) — ver secção 4.3.

**Instalação no `SMA_EXTRATOOLS` (25/09/2026):** `dotnet ef database update` aplicou as 5 migrations — schema `NotifyMsg` com as 5 tabelas e `NotifyMsg.__EFMigrationsHistory`; `dbo.__EFMigrationsHistory` do ExtraTools intacto; sem seed. Api e Worker apontam para o `SMA_EXTRATOOLS` (User Secrets).

**Tenants registados** (E-goi com conta própria, `SECRET_NAME` preenchido, `API_KEY`/`AUTH_TOKEN` vazios na BD):

| Id | Tenant | Domínio E-goi | SenderId | `SECRET_NAME` |
|---|---|---|---|---|
| 1 | `CLUBE_AAC` (Associação Académica de Coimbra) | `aac.academica-oaf.pt` | 2 | `CLUBE_AAC_Egoi` |
| 2 | `CLUBE_SCC` (Sporting Clube da Covilhã) | `etrs.sportingdacovilha.com` | 1 | `CLUBE_SCC_Egoi` |

**Testado:**
- Unitários: `DispatchServiceTests` +4 (Id devolvido = Id gravado, exceção de duplicado traz o Id existente, `GetStatusAsync` com eventos, isolamento por tenant), `ProviderFactoryTests` +3 (chave lida da configuração via `SECRET_NAME` e não da BD, segredo em falta lança exceção sem cair para o global, várias configs ativas → a mais recente), `WorkerTests` +1 (8 mensagens em paralelo, todas `Sent` com o seu `ExternalId`). `FakeEmailProvider.CallCount` passou a `Interlocked` (chamado em paralelo). Suite total **43/43 passam**.
- Real, contra a Api e o Worker a correr sobre o `SMA_EXTRATOOLS`: templates de teste `Id 1` (AAC) e `Id 2` (SCC) criados via `POST /api/v1/templates` → `201`; `POST /notifications` → `202` com `trackingId` = Id real; pedido repetido → `409` com `dispatchId`; `GET` do envio do SCC com a chave do AAC → `404`.
- **SCC: envio real `Sent`, `ExternalId` E-goi `4`, entrega confirmada pelo destinatário (25/09/2026).**
- **AAC: `Failed` — E-goi `403 Forbidden`** nas 3 tentativas. A chave foi resolvida pela configuração (sem aviso de chave em texto simples no log); causa provável indicada pelo utilizador: **saldo insuficiente na conta E-goi do AAC**. Pendente do lado do cliente; o piloto segue com envios reais só pelo SCC.

**Notas:**
- Estado `Sent` é o máximo que se observa com a Api local: os webhooks da E-goi precisam de uma URL pública (secção 4.7), e o callback transacional da E-goi continua por confirmar.
- As connection strings por omissão no `appsettings.json` da Api (`NotifyMsgDB`) e do Worker (`NotifyMessagesDb`) apontam para BDs diferentes — sem efeito enquanto os User Secrets definem a mesma BD nos dois, mas convém alinhar.

---

### 4.10 Evolução para plataforma de controlo de comunicações (planeado — 25/09/2026)

**Decisão (25/09/2026):** o **controlo das comunicações passa a ser todo do NotifyMessages**, de acordo com a visão do produto (middleware B2B SaaS que automatiza mensagens transacionais para ERPs de clubes, contabilidade, imobiliárias…, com idempotência como diferencial). Até aqui o NotifyMessages só reagia a chamadas `POST /notifications`; o plano da Fase 1 do Smartarena ExtraTools tinha posto gatilhos, fila, supressões e histórico do lado do cliente — foi revertido. O Smartarena é o **primeiro grupo de utilizadores**.

**Divisão:**
- **NotifyMessages (genérico):** tenants, templates por tenant, **gatilhos agendados**, **lotes** (por API ou **ficheiro CSV/Excel**, com pré-visualização e aprovação), fila, idempotência por **chave externa**, supressões, Sandbox, histórico.
- **Cliente:** só "quem recebe e com que dados". Três formas: (1) carregar um ficheiro na UI; (2) o software do cliente chama a API de lotes; (3) um **conector** que lê a BD do software do cliente — **um por software de origem, não por cliente** (ex. o conector Smartarena serve todos os clubes). O conector pergunta ao NotifyMessages que gatilhos estão devidos (*pull*) e entrega lotes.
- **Interface (piloto):** a Area `MessageFlow` do Smartarena ExtraTools é um ecrã sobre a **API de administração** do NotifyMessages; o portal próprio do NotifyMessages fica para depois e reutilizará a mesma API.

| # | Tarefa | Componente | Status |
|---|--------|------------|--------|
| P1 | **Repositório GitHub** (conta própria, diferente da do ExtraTools), `.gitignore` .NET, commit inicial com o estado atual — substitui as cópias `.7z` | Repositório | ✅ 25/09/2026 — `git init` (ramo `main`), identidade local `DelCastroAlgoSystem <delcastrofinancas@gmail.com>`, commit inicial `36e6317`, remoto `origin` = `https://github.com/delcastroalgosystem/notifyMessages.git`. **Fora do repositório** (`.gitignore`): `Docs/RegistroCompletoClubePasso a passo.txt` e `NotifyMessages.Worker/UserSecretsRegister.ps1` (têm **chaves reais**), `.claude/`, `desktop.ini`, `*.user`. `Register-Tenant.ps1`: `-ConnectionString` passa a obrigatória (tinha uma connection string com credenciais por omissão). Última cópia `.7z`: `NotifyMessages_backup_2026-09-25_antes-git.7z`. O `git push` é feito pelo utilizador, com a conta GitHub própria |
| P2 | **`TENANT`**: + `REF_NAME` (único — referência de ligação ao software externo, ex. `CLUBE_AAC`; sugestão do utilizador), `NAME` passa a ser o nome real da entidade (corrigir os 2 tenants atuais); + `SENDING_ENABLED`, `SANDBOX_CONTACT`, `TIME_ZONE` (por omissão `Europe/Lisbon`) | Domain / Migration | ✅ 25/09/2026 — colunas criadas (migration `AddTriggersBatchesSuppressions`, com `SENDING_ENABLED = 1` e `TIME_ZONE = Europe/Lisbon` para os tenants existentes); índice único filtrado `IX_Tenant_RefName`; tenants 1 e 2 corrigidos (`NAME` real, `REF_NAME` `CLUBE_AAC`/`CLUBE_SCC`, `SANDBOX_CONTACT` de teste em DEV); `Register-Tenant.ps1` ganha `-RefName`, `-SandboxContact`, `-TimeZone` |
| P3 | **Templates por tenant**: `TEMPLATE.TENANT_ID` (nulo = partilhado); o `TemplatesController` só mostra/aceita os do tenant e os partilhados | Domain / Api | ✅ 25/09/2026 — `TEMPLATE.TENANT_ID`; `TemplateService`/`TemplatesController` por tenant (vê os seus + partilhados, só altera os seus, os criados ficam seus; `null` = administração, para a 1.D); `POST /notifications` com template de outro tenant → `400`. Templates de teste 1/2 atribuídos ao AAC/SCC |
| P4 | **Chave externa de idempotência**: `MESSAGE_DISPATCH.EXTERNAL_KEY`, única por tenant quando preenchida (ex. `SOCIO:1234:2026`); o hash dos dados continua para quem não a envia | Domain / Application | ✅ 25/09/2026 — `EXTERNAL_KEY` com índice único filtrado `(TENANT_ID, EXTERNAL_KEY)`; `externalKey` opcional em `POST /notifications` (mesma chave = `409` com o id existente, mesmo com dados diferentes; chaves iguais em tenants diferentes não colidem) |
| P5 | **Gatilhos** (`TRIGGER`): tipo (`CODE`), template, agenda (dia/hora na hora local do tenant), data de início, parâmetros JSON, exigir aprovação; `GET /api/v1/triggers/due` para os conectores | Domain / Api | ✅ 25/09/2026 — tabela **`MESSAGE_TRIGGER`** (`TRIGGER` é palavra reservada) e **`GET /api/v1/triggers/due`**: devidos na hora local do tenant (`TenantClock`, IANA com recurso a `Europe/Lisbon`), sem lote de conector para o dia, `FILE` excluído; `?at=` simulado só fora de Produção. CRUD dos gatilhos na API de administração (P9) |
| P6 | **Lotes** (`BATCH`): `POST /api/v1/batches` (itens + excluídos), validação, duplicados pela chave externa, supressões, estado `Held` até à aprovação, contagens; `GET /api/v1/batches/{id}` | Domain / Application / Api | ✅ 25/09/2026 — tabela **`DISPATCH_BATCH`** + FK `MESSAGE_DISPATCH.BATCH_ID` (restrict; migration `AddDispatchBatchForeignKey`) e **`POST` / `GET /api/v1/batches`**: validação por item (rejeitados com motivo), duplicados no lote e contra envios anteriores (mesma chave que `POST /notifications` — `IdempotencyKeys`), supressões, `Held` + `PendingApproval` quando o gatilho exige aprovação, senão `Queued` + `Approved`; `409` com `batchId` para o mesmo gatilho/dia; progresso por estado e passagem automática a `Completed`; detalhes em JSON legível (excluídos e avisos da origem). Aprovar/cancelar na API de administração (P9) |
| P7 | **Supressões** (`SUPPRESSION`, por tenant; origem manual / bounce / unsubscribe / importação), aplicadas ao aceitar um lote | Domain / Application | ✅ 27/09/2026 — tabela `SUPPRESSION`, aplicada em `POST /notifications` e `POST /batches`; gestão na API de administração (`GET`/`POST`/`DELETE /admin/suppressions`, contacto normalizado, idempotente; os envios `Held`/`Queued` do contacto passam logo a `Suppressed`). Alimentação automática pelos bounces dos webhooks: por fazer |
| P8 | **Worker**: Sandbox (por tenant e forçado por configuração fora de Produção, com `SENT_TO`), interruptor de envio por tenant, `SCHEDULED_AT` | Worker | ✅ 25/09/2026 — Worker só pega `Queued` com `SCHEDULED_AT` vencido e de tenants com `SENDING_ENABLED`; Sandbox pelo `SANDBOX_CONTACT` do tenant, **forçado fora de Produção** (`WorkerOptions:ForceSandbox`, nulo → `!IsProduction`): sem endereço de teste a mensagem falha em vez de ir para o destinatário real; `SENT_TO` grava o contacto efetivo; SMS em Sandbox é cancelado |
| P9 | **API de administração**: chave de plataforma (`X-Admin-Key`, só o hash na BD) + `X-Acting-User` para auditoria; tenants, templates, gatilhos, lotes (aprovar/cancelar), supressões, histórico | Api | ✅ 27/09/2026 — esquema `AdminKey` (`X-Admin-Key` contra `AdminApi:KeyHashes`, comparação em tempo constante; `X-Acting-User` obrigatório → auditoria); controllers `/api/v1/admin` para tenants (definições de envio/Sandbox/fuso), templates (todos os tenants + partilhados), gatilhos (CRUD com validações), lotes (lista, itens, **aprovar**, **cancelar**), supressões e histórico de envios (filtros + contagens por estado). `Scripts\New-AdminKey.ps1` gera a chave e grava o hash (Api) e a chave (cliente) nos User Secrets sem a mostrar |
| P10 | **Carga de ficheiro CSV/Excel** contra um gatilho de tipo `FILE` → lote para aprovação, com relatório de linhas inválidas | Application / Api | ✅ 27/09/2026 — `POST /admin/batches/upload` (multipart, até 5 MB): `BatchFileParser` (Infrastructure) lê CSV (`;`, `,`, tab; UTF-8 ou Latin-1; aspas) e Excel `.xlsx` (ClosedXML, primeira folha); colunas reconhecidas por nome (contacto obrigatório, nome e chave opcionais), as restantes viram variáveis; chave gerada para linhas sem chave; lote `File` sempre `PendingApproval` |
| P11 | **Correções da validação ponta a ponta do piloto** (tarefa 1.7 do ExtraTools, 28/09/2026) | Application / Infrastructure / Worker | ✅ 28/09/2026 — (a) **escape HTML dos valores**: `TemplateVariableRenderer.RenderHtml` escapa `&`, `<`, `>`, `"`, `'` nos valores das variáveis do corpo HTML (Worker — e-mail e campanha — e pré-visualização); o HTML do template não é tocado, acentos e `€` ficam como estão; assunto e SMS continuam em texto simples. Antes, um nome com `&` ou `<` vindo da origem entrava no HTML tal como vinha. (b) **CSV/Excel: a coluna do nome também vira variável** — antes era consumida como `RecipientName` e o template ficava sem `{{Nome}}`; contacto e chave continuam fora das variáveis; as chaves geradas (só pelo contacto) não mudam. 110 testes |
**Modelo (schema `NotifyMsg`) — implementado em 25/09/2026** (migration `AddTriggersBatchesSuppressions`, aplicada ao `SMA_EXTRATOOLS`): `TENANT` (+ colunas de P2), `TEMPLATE` (+ `TENANT_ID`), **`MESSAGE_TRIGGER`**, **`DISPATCH_BATCH`**, `MESSAGE_DISPATCH` (+ `BATCH_ID`, `TRIGGER_ID`, `EXTERNAL_KEY`, `SCHEDULED_AT`, `SENT_TO`; estados `Held = 10` e `Suppressed = 101`), **`SUPPRESSION`**.

**Testado — API de administração (27/09/2026):** suite **108/108** (+26: `AdminServicesTests` ×17 — aprovar/cancelar lotes e estados inválidos, lista e itens com filtros, lote de ficheiro com chaves geradas e sem duplicar no mesmo dia, gatilhos com validações e auditoria, supressões normalizadas/idempotentes/que suprimem os envios pendentes, definições do tenant, histórico com contagens; `BatchFileParserTests` ×7 — CSV `;`/`,`/aspas/Latin-1, sem coluna de contacto, formato não suportado, Excel; `AdminKeyTests` ×2). Real, sobre o `SMA_EXTRATOOLS` com a chave gerada pelo `New-AdminKey.ps1`: sem `X-Acting-User` → `401`, chave de tenant na administração → `401`, chave de plataforma num endpoint de tenant → `401`; lote 3 (aniversários SCC) **aprovado** → 3 envios `Sent` em Sandbox para o endereço de teste, lote `Completed`; aprovar outra vez → `400`; lotes 2, 4, 5, 6 e 7 **cancelados** (14 + 555 + 2 569 + 1 098 + 943 envios `Canceled`); CSV com 2 linhas válidas + 1 inválida → lote `File` `PendingApproval` (2 aceites, 1 rejeitado, chaves `FILE:T4:20260927:<contacto>`), o mesmo ficheiro outra vez → 2 duplicados; supressão acrescentada (normalizada, `createdBy` = utilizador), repetida → `200`, procurada e removida; histórico do SCC com contagens por estado.

**Testado — API do conector (25/09/2026):** suite **82/82** (+25: `TriggerServiceTests` ×11 — hora local e fuso do tenant, dia, hora, data de início, inativo, `FILE`, lote já existente, isolamento; `BatchServiceTests` ×14 — Queued/Held, lote repetido, duplicados no lote e anteriores, supressão, rejeitados, detalhes, lote vazio, gatilho/template de outro tenant, `RunDate` em falta, conclusão, isolamento, chave partilhada com o envio individual). Real, sobre o `SMA_EXTRATOOLS`: gatilho de teste do SCC devido às 00:00 locais (invisível ao AAC); lote com 1 item válido + 1 inválido + 1 excluído → `201`, `Approved`, 1 aceite, 1 rejeitado; mesmo gatilho/dia → `409` com o id; o gatilho deixa de estar devido; AAC lê o lote → `404`; o envio (Sandbox) → `Sent` e o lote → `Completed`. Gatilho de teste desativado no fim.

**Testado — modelo (25/09/2026):** suite **57/57** (+14: templates por tenant ×3, `DispatchService` ×6 — template de outro tenant, chave externa ×2, supressão ×2, template próprio —, Worker ×5 — Sandbox pelo tenant, Sandbox forçado sem endereço, envio desligado, `SCHEDULED_AT` futuro/passado). Real, sobre o `SMA_EXTRATOOLS` com a Api e o Worker a correr: o SCC só vê o seu template (o do AAC → `404`); envio com o template do AAC → `400`; envio com `externalKey` → `202`; mesma chave com dados diferentes → `409` com o mesmo id; **envio real pelo SCC em Sandbox**: `recipientContact` fictício, `sentTo` = endereço de teste, `Sent`, `ExternalId` E-goi `5`.

---

## 5. Estrutura do Banco de Dados (Resumo SQL)

As principais tabelas do sistema no SQL Server (`NotifyMsgDB` e, desde 25/09/2026, `SMA_EXTRATOOLS`), todas sob o **schema dedicado `NotifyMsg`** desde 21/09/2026 (ver secção 4.6) — ex: `NotifyMsg.TENANT`, não `dbo.TENANT`. O histórico de migrações também fica em `NotifyMsg.__EFMigrationsHistory` (secção 4.9, M4):

> As tabelas abaixo refletem o estado real confirmado por query direta em 14/09/2026 (migration `InitialCreate` aplicada). Nomes atualizados face à versão anterior deste documento.

- **`TENANT`**: `ID`, `NAME`, `DOCUMENT_ID`, `ACTIVE`, `API_KEY_HASH` (hash SHA-256 da API Key do tenant, índice único filtrado `IX_Tenant_ApiKeyHash`, adicionado na migration `AddTenantApiKeyHash`)
- **`TEMPLATE`**: `ID`, `NAME`, `CHANNEL`, `PROVIDER_TYPE`, `USE_CAMPAIGN_MODE`, `SUBJECT`, `HTML_BODY`, `TEXT_BODY`, `SENDER_ID`, `SENDER_NAME`, `EXTERNAL_TEMPLATE_ID`, `LIST_ID`, `IS_ACTIVE`, `CREATED_AT`, `UPDATED_AT`
- **`TENANT_PROVIDER_CONFIG`**: `ID`, `TENANT_ID`, `PROVIDER_TYPE`, `IS_GLOBAL`, `API_KEY` (legado), `SECRET_NAME` (desde 25/09/2026, migration `AddTenantProviderSecretName` — nome do segredo na configuração; ver secção 4.9), `BASE_URL`, `DOMAIN`, `SENDER_ID`, `SENDER_NAME`, `ACCOUNT_SID`, `AUTH_TOKEN`, `FROM_NUMBER`, `LIST_ID`, `IS_ACTIVE`, `CREATED_AT`, `UPDATED_AT`
- **`MESSAGE_DISPATCH`**: `ID`, `TENANT_ID`, `TEMPLATE_ID`, `IDEMPOTENCY_KEY`, `EXTERNAL_ID`, `RECIPIENT_NAME`, `RECIPIENT_CONTACT`, `CONTEXT_DATA_JSON`, `CURRENT_STATUS`, `CREATED_AT`, `PROCESSED_AT`, `RETRY_COUNT`, `ERROR_LOG`
- **`MESSAGE_DISPATCH_EVENTS`**: `ID`, `DISPATCH_ID` (FK→`MESSAGE_DISPATCH`, `ON DELETE CASCADE`), `EVENT_TYPE`, `EVENT_DATE`, `PROVIDER_RESPONSE`, `CREATED_AT` (ver secção 4.7)
- **Desde 25/09/2026** (migration `AddTriggersBatchesSuppressions`, secção 4.10):
  - `TENANT` + `REF_NAME` (único filtrado), `SENDING_ENABLED`, `SANDBOX_CONTACT`, `TIME_ZONE`; `TEMPLATE` + `TENANT_ID` (nulo = partilhado); `MESSAGE_DISPATCH` + `EXTERNAL_KEY` (único filtrado por tenant), `BATCH_ID`, `TRIGGER_ID`, `SCHEDULED_AT`, `SENT_TO`
  - **`MESSAGE_TRIGGER`**: `ID`, `TENANT_ID`, `CODE`, `NAME` (único por tenant), `TEMPLATE_ID`, `SCHEDULE_DAY` (1–28, `CHECK`), `SCHEDULE_TIME`, `START_DATE`, `PARAMETERS`, `REQUIRES_APPROVAL`, `IS_ACTIVE`, `CREATED_BY/AT`, `UPDATED_BY/AT`
  - **`DISPATCH_BATCH`**: `ID`, `TENANT_ID`, `TRIGGER_ID`, `SOURCE`, `RUN_DATE`, `REFERENCE_DATE`, `STATUS`, `RECEIVED`, `ACCEPTED`, `DUPLICATES`, `SUPPRESSED`, `REJECTED`, `DETAILS`, `FILE_NAME`, `CREATED_BY/AT`, `APPROVED_BY/AT`, `COMPLETED_AT` (único `(TRIGGER_ID, RUN_DATE)` para lotes de conector)
  - **`SUPPRESSION`**: `ID`, `TENANT_ID`, `CONTACT` (único por tenant), `REASON`, `SOURCE`, `CREATED_BY`, `CREATED_AT`

---

## 6. Referências & Documentação das APIs

- [E-goi API v2 (Transacional)](https://developers.e-goi.com/api/v2/)
- [E-goi API v3 (Marketing)](https://developers.e-goi.com/api/v3/)
- [SendGrid API v3](https://docs.sendgrid.com/api-reference/)
- [Twilio SMS API](https://www.twilio.com/docs/sms/api)
- [Brevo API — Send Transactional Email](https://developers.brevo.com/docs/send-a-transactional-email)
- [Brevo API — Send Transactional SMS](https://developers.brevo.com/docs/transactional-sms-endpoints)
