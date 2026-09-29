<#
.SYNOPSIS
    Gera uma nova API Key (header X-Api-Key) para um tenant existente e grava o hash em NotifyMsg.TENANT.API_KEY_HASH.

.DESCRIPTION
    Para rodar a chave de um tenant (ex. em Produção, trocar a que se usou em DEV). A chave anterior deixa de
    funcionar no momento em que o hash é gravado: atualizar logo o cliente (ex. o Smartarena.MessageFlow.Agent,
    em NotifyMessages:TenantApiKeys:<RefName>). Correr como Administrador. Compatível com Windows PowerShell 5.1.

    - BD: -ApiSettingsFile (o appsettings.Production.json da Api; lê ConnectionStrings:DefaultConnection) ou
      -ConnectionString (numa variável, nunca escrita no script).
    - Com -ClientSettingsFile (o appsettings.Production.json do cliente), grava a chave nesse ficheiro e NÃO a mostra;
      as permissões do ficheiro mantêm-se. Sem ele, mostra a chave uma única vez para a colar no cliente.

.EXAMPLE
    .\Scripts\New-TenantApiKey.ps1 -RefName CLUBE_SCC -ApiSettingsFile C:\inetpub\NotifyMessages\appsettings.Production.json `
        -ClientSettingsFile C:\Services\SmartarenaMessageFlowAgent\appsettings.Production.json
#>
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[A-Za-z0-9_.-]{1,50}$')][string]$RefName,
    [string]$ApiSettingsFile,
    [string]$ConnectionString,
    [string]$ClientSettingsFile,
    [string]$ClientKeyName
)

$ErrorActionPreference = "Stop"
if (-not $ClientKeyName) { $ClientKeyName = "NotifyMessages:TenantApiKeys:$RefName" }
foreach ($f in @($ApiSettingsFile, $ClientSettingsFile)) { if ($f -and -not (Test-Path $f)) { throw "Ficheiro não encontrado: $f" } }
if (-not $ConnectionString) {
    if (-not $ApiSettingsFile) { throw "Indique -ApiSettingsFile (appsettings.Production.json da Api) ou -ConnectionString." }
    $ConnectionString = (Get-Content $ApiSettingsFile -Raw | ConvertFrom-Json).ConnectionStrings.DefaultConnection
    if (-not $ConnectionString) { throw "ConnectionStrings:DefaultConnection vazia em $ApiSettingsFile." }
}

# Grava um valor num appsettings JSON pelo caminho "A:B:C" (cria as secções que faltam).
function Set-JsonValue([string]$File, [string]$KeyPath, [string]$Value) {
    $root = Get-Content $File -Raw | ConvertFrom-Json
    $node = $root
    $parts = $KeyPath.Split(':')
    for ($i = 0; $i -lt $parts.Length - 1; $i++) {
        $p = $parts[$i]
        if (-not ($node.PSObject.Properties.Name -contains $p) -or $null -eq $node.$p) {
            $node | Add-Member -NotePropertyName $p -NotePropertyValue (New-Object PSObject) -Force
        }
        $node = $node.$p
    }
    $last = $parts[-1]
    if ($node.PSObject.Properties.Name -contains $last) { $node.$last = $Value } else { $node | Add-Member -NotePropertyName $last -NotePropertyValue $Value }
    # Escrever o conteúdo mantém as permissões (ACL) do ficheiro
    [System.IO.File]::WriteAllText($File, ($root | ConvertTo-Json -Depth 10), (New-Object System.Text.UTF8Encoding($false)))
}

# Mesma geração e hash do ApiKeyHasher (32 bytes aleatórios em hexadecimal; SHA-256 em hexadecimal maiúsculo)
$bytes = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
$key = -join ($bytes | ForEach-Object { $_.ToString("X2") })
$sha256 = [System.Security.Cryptography.SHA256]::Create()
try { $hashBytes = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($key)) } finally { $sha256.Dispose() }
$hash = -join ($hashBytes | ForEach-Object { $_.ToString("X2") })

Add-Type -AssemblyName "System.Data"
$connection = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
$connection.Open()
try {
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = "UPDATE NotifyMsg.TENANT SET API_KEY_HASH = @hash WHERE REF_NAME = @ref"
    $cmd.Parameters.AddWithValue("@hash", $hash) | Out-Null
    $cmd.Parameters.AddWithValue("@ref", $RefName) | Out-Null
    $linhas = $cmd.ExecuteNonQuery()
}
finally { $connection.Close() }
if ($linhas -ne 1) { throw "Nenhum tenant com REF_NAME = '$RefName' (linhas alteradas: $linhas). Nada foi mudado." }
Write-Host "Nova chave do tenant $RefName gravada (hash em NotifyMsg.TENANT). A chave anterior já não funciona." -ForegroundColor Green

if ($ClientSettingsFile) {
    Set-JsonValue $ClientSettingsFile $ClientKeyName $key
    Write-Host "Chave gravada em $ClientKeyName ($ClientSettingsFile). Não foi mostrada." -ForegroundColor Green
}
else {
    Write-Host "API Key do tenant (X-Api-Key) - cole-a agora em $ClientKeyName do cliente, não volta a ser mostrada:" -ForegroundColor Yellow
    Write-Host $key -ForegroundColor Yellow
}
