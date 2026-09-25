<#
.SYNOPSIS
    Regista um novo Tenant real e, opcionalmente, uma credencial de provedor
    (TENANT_PROVIDER_CONFIG) para ele, gerando a API Key do NotifyMessages
    (usada no header X-Api-Key) da mesma forma que o DbSeeder faz para os
    tenants de desenvolvimento.

.EXAMPLE
    .\Scripts\Register-Tenant.ps1 `
        -TenantName "Associação Académica de Coimbra" `
        -RefName "CLUBE_AAC" `
        -SandboxContact "teste@empresa.pt" `
        -ConnectionString $cs `
        -DocumentId "PT123456789" `
        -ProviderType Egoi `
        -ProviderSecretName "CLUBE_AAC_Egoi" `
        -ProviderDomain "dominio-do-cliente.com" `
        -ProviderSenderId "5"

.NOTES
    A chave do provedor NAO passa por este script nem fica na BD: a BD guarda
    so o nome do segredo (SECRET_NAME) e o valor vive na configuracao do
    NotifyMessages, em ProviderSettings:Tenants:<ProviderSecretName>:ApiKey
    (e :AuthToken no Twilio). No fim, o script imprime os comandos
    'dotnet user-secrets set' a correr (sem valores) para a Api e o Worker.
    So o resultado (Id do Tenant + API Key gerada para autenticar no
    NotifyMessages) e impresso no teu terminal.
#>
param(
    # Nome real da entidade (TENANT.NAME).
    [Parameter(Mandatory = $true)][string]$TenantName,
    # Referência de ligação ao software externo do cliente (TENANT.REF_NAME, única), ex. "CLUBE_AAC".
    [ValidatePattern('^[A-Za-z0-9_.-]{1,50}$')]
    [string]$RefName,
    # Endereço de teste: com ele, todos os e-mails do tenant vão para aqui (Sandbox).
    [string]$SandboxContact,
    # Fuso horário IANA das agendas dos gatilhos.
    [string]$TimeZone = "Europe/Lisbon",
    [string]$DocumentId,
    [switch]$Inactive,

    [ValidateSet("Egoi", "EgoiCampaign", "SendGrid", "Twilio")]
    [string]$ProviderType,

    [ValidatePattern('^[A-Za-z0-9_.-]{1,100}$')]
    [string]$ProviderSecretName,
    [string]$ProviderBaseUrl,
    [string]$ProviderDomain,
    [string]$ProviderSenderId,
    [string]$ProviderSenderName,
    [string]$ProviderAccountSid,
    [string]$ProviderFromNumber,
    [int]$ProviderListId,

    # Obrigatória: nunca guardar uma connection string com credenciais neste ficheiro (vai para o repositório).
    # Ex.: -ConnectionString $cs, com $cs lida dos User Secrets (ver README).
    [Parameter(Mandatory = $true)][string]$ConnectionString
)

$ErrorActionPreference = "Stop"

function ConvertTo-HexStringUpper([byte[]]$bytes) {
    return -join ($bytes | ForEach-Object { $_.ToString("X2") })
}

function New-ApiKeyPlainText {
    $bytes = [byte[]]::new(32)
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($bytes)
    }
    finally {
        $rng.Dispose()
    }
    return ConvertTo-HexStringUpper $bytes
}

function Get-Sha256Hex([string]$text) {
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($text))
    }
    finally {
        $sha256.Dispose()
    }
    return ConvertTo-HexStringUpper $bytes
}

$providerTypeMap = @{ Egoi = 1; EgoiCampaign = 2; SendGrid = 3; Twilio = 4 }

if ($ProviderType -and -not $ProviderSecretName) {
    throw "Com -ProviderType e obrigatorio -ProviderSecretName (nome do segredo na configuracao do NotifyMessages)."
}

Add-Type -AssemblyName "System.Data"

$plainApiKey = New-ApiKeyPlainText
$apiKeyHash = Get-Sha256Hex $plainApiKey

$connection = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
$connection.Open()
$transaction = $connection.BeginTransaction()

try {
    $tenantCmd = $connection.CreateCommand()
    $tenantCmd.Transaction = $transaction
    $tenantCmd.CommandText = @"
INSERT INTO NotifyMsg.TENANT (NAME, REF_NAME, DOCUMENT_ID, ACTIVE, API_KEY_HASH, SENDING_ENABLED, SANDBOX_CONTACT, TIME_ZONE)
OUTPUT INSERTED.ID
VALUES (@name, @refName, @documentId, @active, @apiKeyHash, 1, @sandboxContact, @timeZone)
"@
    $tenantCmd.Parameters.AddWithValue("@name", $TenantName) | Out-Null
    $tenantCmd.Parameters.AddWithValue("@documentId", [object]($(if ($DocumentId) { $DocumentId } else { [DBNull]::Value }))) | Out-Null
    $tenantCmd.Parameters.AddWithValue("@active", (-not $Inactive.IsPresent)) | Out-Null
    $tenantCmd.Parameters.AddWithValue("@apiKeyHash", $apiKeyHash) | Out-Null
    $tenantCmd.Parameters.AddWithValue("@refName", [object]($(if ($RefName) { $RefName } else { [DBNull]::Value }))) | Out-Null
    $tenantCmd.Parameters.AddWithValue("@sandboxContact", [object]($(if ($SandboxContact) { $SandboxContact } else { [DBNull]::Value }))) | Out-Null
    $tenantCmd.Parameters.AddWithValue("@timeZone", $TimeZone) | Out-Null

    $tenantId = $tenantCmd.ExecuteScalar()

    if ($ProviderType) {
        $configCmd = $connection.CreateCommand()
        $configCmd.Transaction = $transaction
        $configCmd.CommandText = @"
INSERT INTO NotifyMsg.TENANT_PROVIDER_CONFIG
    (TENANT_ID, PROVIDER_TYPE, IS_GLOBAL, IS_ACTIVE, SECRET_NAME, BASE_URL, DOMAIN, SENDER_ID, SENDER_NAME, ACCOUNT_SID, FROM_NUMBER, LIST_ID, CREATED_AT)
VALUES
    (@tenantId, @providerType, 0, 1, @secretName, @baseUrl, @domain, @senderId, @senderName, @accountSid, @fromNumber, @listId, GETUTCDATE())
"@
        $configCmd.Parameters.AddWithValue("@tenantId", $tenantId) | Out-Null
        $configCmd.Parameters.AddWithValue("@providerType", $providerTypeMap[$ProviderType]) | Out-Null
        $configCmd.Parameters.AddWithValue("@secretName", $ProviderSecretName) | Out-Null
        $configCmd.Parameters.AddWithValue("@baseUrl", [object]($(if ($ProviderBaseUrl) { $ProviderBaseUrl } else { [DBNull]::Value }))) | Out-Null
        $configCmd.Parameters.AddWithValue("@domain", [object]($(if ($ProviderDomain) { $ProviderDomain } else { [DBNull]::Value }))) | Out-Null
        $configCmd.Parameters.AddWithValue("@senderId", [object]($(if ($ProviderSenderId) { $ProviderSenderId } else { [DBNull]::Value }))) | Out-Null
        $configCmd.Parameters.AddWithValue("@senderName", [object]($(if ($ProviderSenderName) { $ProviderSenderName } else { [DBNull]::Value }))) | Out-Null
        $configCmd.Parameters.AddWithValue("@accountSid", [object]($(if ($ProviderAccountSid) { $ProviderAccountSid } else { [DBNull]::Value }))) | Out-Null
        $configCmd.Parameters.AddWithValue("@fromNumber", [object]($(if ($ProviderFromNumber) { $ProviderFromNumber } else { [DBNull]::Value }))) | Out-Null
        $configCmd.Parameters.AddWithValue("@listId", [object]($(if ($ProviderListId -gt 0) { $ProviderListId } else { [DBNull]::Value }))) | Out-Null

        $configCmd.ExecuteNonQuery() | Out-Null
    }

    $transaction.Commit()
}
catch {
    $transaction.Rollback()
    throw
}
finally {
    $connection.Close()
}

Write-Host ""
Write-Host "Tenant '$TenantName' criado com Id $tenantId" -ForegroundColor Green
if ($ProviderType) {
    Write-Host "Configuracao '$ProviderType' registada para este tenant (TENANT_PROVIDER_CONFIG, SECRET_NAME = '$ProviderSecretName')." -ForegroundColor Green
    Write-Host ""
    Write-Host "Falta o segredo do provedor. Em dev, corre no teu terminal (substitui <valor>; nao o partilhes):" -ForegroundColor Cyan
    $secretKey = if ($ProviderType -eq "Twilio") { "AuthToken" } else { "ApiKey" }
    foreach ($project in @("NotifyMessages.Api", "NotifyMessages.Worker")) {
        Write-Host "  dotnet user-secrets set `"ProviderSettings:Tenants:${ProviderSecretName}:$secretKey`" `"<valor>`" --project $project" -ForegroundColor Cyan
    }
    Write-Host "Em producao: variavel de ambiente ProviderSettings__Tenants__${ProviderSecretName}__$secretKey (ou cofre) em ambos os servicos." -ForegroundColor Cyan
}
Write-Host ""
Write-Host "API Key para autenticar no NotifyMessages (header X-Api-Key) - guarda-a agora, so o hash fica gravado:" -ForegroundColor Yellow
Write-Host $plainApiKey -ForegroundColor Yellow
Write-Host ""
