IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [MESSAGE_DISPATCH] (
    [ID] bigint NOT NULL IDENTITY,
    [TENANT_ID] int NOT NULL,
    [TEMPLATE_ID] int NOT NULL,
    [IDEMPOTENCY_KEY] nvarchar(100) NOT NULL,
    [EXTERNAL_ID] nvarchar(100) NULL,
    [RECIPIENT_NAME] nvarchar(100) NOT NULL,
    [RECIPIENT_CONTACT] nvarchar(100) NOT NULL,
    [CONTEXT_DATA_JSON] nvarchar(max) NOT NULL,
    [CURRENT_STATUS] int NOT NULL,
    [CREATED_AT] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    [PROCESSED_AT] datetime2 NULL,
    [RETRY_COUNT] int NOT NULL,
    [ERROR_LOG] nvarchar(max) NULL,
    CONSTRAINT [PK_MESSAGE_DISPATCH] PRIMARY KEY ([ID])
);

CREATE TABLE [TEMPLATE] (
    [ID] int NOT NULL IDENTITY,
    [NAME] nvarchar(100) NOT NULL,
    [CHANNEL] int NOT NULL,
    [PROVIDER_TYPE] int NOT NULL,
    [USE_CAMPAIGN_MODE] bit NOT NULL,
    [SUBJECT] nvarchar(200) NULL,
    [HTML_BODY] nvarchar(max) NULL,
    [TEXT_BODY] nvarchar(max) NULL,
    [SENDER_ID] nvarchar(100) NULL,
    [SENDER_NAME] nvarchar(100) NULL,
    [EXTERNAL_TEMPLATE_ID] int NULL,
    [LIST_ID] int NULL,
    [IS_ACTIVE] bit NOT NULL,
    [CREATED_AT] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    [UPDATED_AT] datetime2 NULL,
    CONSTRAINT [PK_TEMPLATE] PRIMARY KEY ([ID])
);

CREATE TABLE [TENANT_PROVIDER_CONFIG] (
    [ID] int NOT NULL IDENTITY,
    [TENANT_ID] int NOT NULL,
    [PROVIDER_TYPE] int NOT NULL,
    [IS_GLOBAL] bit NOT NULL,
    [API_KEY] nvarchar(max) NULL,
    [BASE_URL] nvarchar(200) NULL,
    [DOMAIN] nvarchar(100) NULL,
    [SENDER_ID] nvarchar(100) NULL,
    [SENDER_NAME] nvarchar(100) NULL,
    [ACCOUNT_SID] nvarchar(100) NULL,
    [AUTH_TOKEN] nvarchar(max) NULL,
    [FROM_NUMBER] nvarchar(20) NULL,
    [LIST_ID] int NULL,
    [IS_ACTIVE] bit NOT NULL,
    [CREATED_AT] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    [UPDATED_AT] datetime2 NULL,
    CONSTRAINT [PK_TENANT_PROVIDER_CONFIG] PRIMARY KEY ([ID])
);

CREATE UNIQUE INDEX [IX_MessageDispatch_IdempotencyKey] ON [MESSAGE_DISPATCH] ([IDEMPOTENCY_KEY]);

CREATE INDEX [IX_MessageDispatch_QueueProcessing] ON [MESSAGE_DISPATCH] ([CURRENT_STATUS], [CREATED_AT]);

CREATE INDEX [IX_Template_Name] ON [TEMPLATE] ([NAME]);

CREATE INDEX [IX_Tenant_Name] ON [TENANT] ([NAME]);

CREATE INDEX [IX_TenantProviderConfig_Tenant_Provider] ON [TENANT_PROVIDER_CONFIG] ([TENANT_ID], [PROVIDER_TYPE]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260725165429_InitialCreate', N'9.0.12');

COMMIT;
GO

