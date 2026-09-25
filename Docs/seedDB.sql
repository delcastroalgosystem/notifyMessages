-- ==========================================
-- SCRIPT DE SEED / CARGA INICIAL DE DADOS
-- Banco de Dados: NotifyMsgDB
-- ==========================================

USE [NotifyMsgDB];
GO

-- 1. Inserir Tenant de Teste (se não existir)
IF NOT EXISTS (SELECT 1 FROM [dbo].[TENANT] WHERE [ID] = 1)
BEGIN
    SET IDENTITY_INSERT [dbo].[TENANT] ON;
    INSERT INTO [dbo].[TENANT] ([ID], [NAME], [DOCUMENT_ID], [ACTIVE])
    VALUES (1, 'Clube Esportivo de Teste', '12345678000199', 1);
    SET IDENTITY_INSERT [dbo].[TENANT] OFF;
    PRINT 'Tenant de teste inserido com ID 1.';
END
ELSE
BEGIN
    PRINT 'Tenant ID 1 já existe.';
END
GO

-- 2. Inserir Configurações de Provedores de Teste para o Tenant 1
IF NOT EXISTS (SELECT 1 FROM [dbo].[TENANT_PROVIDER_CONFIG] WHERE [TENANT_ID] = 1 AND [PROVIDER_TYPE] = 'Egoi')
BEGIN
    INSERT INTO [dbo].[TENANT_PROVIDER_CONFIG] ([TENANT_ID], [PROVIDER_TYPE], [API_KEY], [API_SECRET], [IS_ACTIVE], [IS_GLOBAL], [CREATED_AT])
    VALUES (1, 'Egoi', 'SUA_EGOI_API_KEY_AQUI', NULL, 1, 0, GETUTCDATE());
    PRINT 'Configuração E-goi para Tenant 1 inserida.';
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[TENANT_PROVIDER_CONFIG] WHERE [TENANT_ID] = 1 AND [PROVIDER_TYPE] = 'SendGrid')
BEGIN
    INSERT INTO [dbo].[TENANT_PROVIDER_CONFIG] ([TENANT_ID], [PROVIDER_TYPE], [API_KEY], [API_SECRET], [IS_ACTIVE], [IS_GLOBAL], [CREATED_AT])
    VALUES (1, 'SendGrid', 'SG.SUA_SENDGRID_KEY_AQUI', NULL, 1, 0, GETUTCDATE());
    PRINT 'Configuração SendGrid para Tenant 1 inserida.';
END
GO

-- 3. Inserir Templates de Teste para o Tenant 1
IF NOT EXISTS (SELECT 1 FROM [dbo].[TEMPLATE] WHERE [ID] = 1)
BEGIN
    SET IDENTITY_INSERT [dbo].[TEMPLATE] ON;
    INSERT INTO [dbo].[TEMPLATE] (
        [ID], 
        [NAME],
        [CHANNEL], 
        [PROVIDER_TYPE],
        [USE_CAMPAIGN_MODE],
        [SUBJECT], 
        [HTML_BODY],
        [TEXT_BODY],
        [SENDER_NAME],
        [IS_ACTIVE],
        [CREATED_AT]
    )
    VALUES (
        1, 
        'Aviso de Cobrança Email',
        0, -- ChannelType.Email
        0, -- ProviderType.Egoi
        0, -- UseCampaignMode: false
        'Aviso de Cobrança - {{Nome}}', 
        'Olá {{Nome}}, informamos que a sua fatura no valor de {{Valor}} vence em {{Vencimento}}.', 
        'Olá {{Nome}}, informamos que a sua fatura no valor de {{Valor}} vence em {{Vencimento}}.',
        'NotifyMessages',
        1,
        GETUTCDATE()
    );
    SET IDENTITY_INSERT [dbo].[TEMPLATE] OFF;
    PRINT 'Template de Email (ID 1) inserido.';
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[TEMPLATE] WHERE [ID] = 2)
BEGIN
    SET IDENTITY_INSERT [dbo].[TEMPLATE] ON;
    INSERT INTO [dbo].[TEMPLATE] (
        [ID], 
        [NAME],
        [CHANNEL], 
        [PROVIDER_TYPE],
        [USE_CAMPAIGN_MODE],
        [SUBJECT], 
        [TEXT_BODY],
        [SENDER_NAME],
        [IS_ACTIVE],
        [CREATED_AT]
    )
    VALUES (
        2, 
        'Notificação SMS',
        1, -- ChannelType.Sms
        3, -- ProviderType.Twilio
        0, -- UseCampaignMode: false
        NULL, 
        'Notificacao: Prezado {{Nome}}, seu codigo de acesso e {{Codigo}}.', 
        'NotifyMessages',
        1,
        GETUTCDATE()
    );
    SET IDENTITY_INSERT [dbo].[TEMPLATE] OFF;
    PRINT 'Template de SMS (ID 2) inserido.';
END
GO
