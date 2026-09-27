<#
.SYNOPSIS
    Gera uma chave de plataforma para a API de administração (/api/v1/admin, header X-Admin-Key).

.DESCRIPTION
    A Api do NotifyMessages só guarda o HASH (AdminApi:KeyHashes:<n>); a chave fica em quem chama a API
    (ex. Smartarena.ExtraTools.Web, em NotifyMessages:AdminKey).

    Em dev, com -ApiProject e -ClientProject, grava os dois nos User Secrets e NÃO mostra a chave.
    Sem -ClientProject, mostra a chave uma única vez para a guardar no cliente (variável de ambiente/cofre em produção).

    Para trocar a chave sem cortes: gerar uma nova com outro -Slot (ex. 1), atualizar o cliente, e depois
    apagar o slot antigo (dotnet user-secrets remove "AdminApi:KeyHashes:0").

.EXAMPLE
    .\Scripts\New-AdminKey.ps1 -ApiProject .\NotifyMessages.Api `
        -ClientProject C:\Developer\Smartmove\Smartarena\ExtraTools\App\Smartarena.ExtraTools.Web
#>
param(
    [Parameter(Mandatory = $true)][string]$ApiProject,
    [string]$ClientProject,
    [string]$ClientKeyName = "NotifyMessages:AdminKey",
    [int]$Slot = 0
)

$ErrorActionPreference = "Stop"

# Compatível com Windows PowerShell 5.1 (.NET Framework) e PowerShell 7
$bytes = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
$key = -join ($bytes | ForEach-Object { $_.ToString("X2") })

$sha256 = [System.Security.Cryptography.SHA256]::Create()
try { $hashBytes = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($key)) } finally { $sha256.Dispose() }
$hash = -join ($hashBytes | ForEach-Object { $_.ToString("X2") })

dotnet user-secrets set "AdminApi:KeyHashes:$Slot" $hash --project $ApiProject | Out-Null
Write-Host "Hash gravado em AdminApi:KeyHashes:$Slot ($ApiProject)." -ForegroundColor Green

if ($ClientProject) {
    dotnet user-secrets set $ClientKeyName $key --project $ClientProject | Out-Null
    Write-Host "Chave gravada em $ClientKeyName ($ClientProject). Não foi mostrada." -ForegroundColor Green
}
else {
    Write-Host "Chave de plataforma (X-Admin-Key) - guarde-a agora no cliente, não volta a ser mostrada:" -ForegroundColor Yellow
    Write-Host $key -ForegroundColor Yellow
}
