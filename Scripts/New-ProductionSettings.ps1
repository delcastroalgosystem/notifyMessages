<#
.SYNOPSIS
    Cria o appsettings.Production.json da Api ou do Worker com os campos de segredos vazios e acesso restrito.

.DESCRIPTION
    Segredos em Produção (opção A, decidida em 28/09/2026): um appsettings.Production.json na pasta publicada de cada
    componente, fora do repositório e nunca publicado, legível só pela conta que corre o componente e pelos
    Administradores. Correr como Administrador no servidor, depois de publicar. Compatível com Windows PowerShell 5.1.

    - Não escreve segredos: cria o ficheiro com os campos vazios, para preencher à mão (ex. no Bloco de Notas como
      Administrador). Se o ficheiro já existir, não lhe toca (só volta a aplicar as permissões).
    - -Account: a identidade do pool do IIS (ex. "IIS AppPool\NotifyMessages") para a Api, ou a conta do serviço
      (ex. "NT SERVICE\NotifyMessagesWorker") para o Worker.
    - Cuidado com publicações que apagam ficheiros a mais no destino (Web Deploy "Remove additional files"):
      excluir este ficheiro ou desligar essa opção.

.EXAMPLE
    .\Scripts\New-ProductionSettings.ps1 -Component Api -Path 'C:\inetpub\NotifyMessages' -Account 'IIS AppPool\NotifyMessages' -TenantSecretNames CLUBE_SCC_Egoi
    .\Scripts\New-ProductionSettings.ps1 -Component Worker -Path 'C:\Services\NotifyMessagesWorker' -Account 'NT SERVICE\NotifyMessagesWorker' -TenantSecretNames CLUBE_SCC_Egoi -PublicBaseUrl 'https://sma-apis.smartarena.pt/NotifyMessages'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Api', 'Worker')][string]$Component,
    [Parameter(Mandatory)][string]$Path,
    [Parameter(Mandatory)][string]$Account,
    # SECRET_NAME de TENANT_PROVIDER_CONFIG de cada tenant com conta própria (ex. CLUBE_SCC_Egoi)
    [string[]]$TenantSecretNames = @(),
    # Só Worker: URL pública da Api (callbacks do Twilio)
    [string]$PublicBaseUrl = ''
)
$ErrorActionPreference = 'Stop'

$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) { throw 'Corra este script numa consola de PowerShell como Administrador.' }
if (-not (Test-Path $Path)) { throw "Pasta não encontrada: $Path" }

$ficheiro = Join-Path $Path 'appsettings.Production.json'
if (Test-Path $ficheiro) {
    Write-Host "$ficheiro já existe: não é alterado (só as permissões)." -ForegroundColor Yellow
} else {
    $tenants = [ordered]@{}
    foreach ($n in $TenantSecretNames) { $tenants[$n] = [ordered]@{ ApiKey = '' } }
    $cfg = [ordered]@{
        ConnectionStrings = [ordered]@{ DefaultConnection = '' }
        ProviderSettings  = [ordered]@{ Tenants = $tenants }
    }
    if ($Component -eq 'Api') {
        $cfg.AdminApi = [ordered]@{ KeyHashes = @('') }
        $cfg.WebhookSettings = [ordered]@{ SharedSecret = '' }
    } else {
        $cfg.WebhookSettings = [ordered]@{ PublicBaseUrl = $PublicBaseUrl; SharedSecret = '' }
    }
    $json = $cfg | ConvertTo-Json -Depth 6
    [System.IO.File]::WriteAllText($ficheiro, $json, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "Criado $ficheiro (campos vazios). Preencha-o antes de arrancar o $Component." -ForegroundColor Green
}

# Só a conta do componente (leitura) e os Administradores/SYSTEM (controlo total); sem herança da pasta
& icacls $ficheiro /inheritance:r /grant:r "${Account}:R" "*S-1-5-32-544:F" "*S-1-5-18:F" /Q | Out-Null
if ($LASTEXITCODE -ne 0) { throw "icacls falhou ($LASTEXITCODE): confirme o nome da conta '$Account'." }
Write-Host "Permissões: leitura para $Account; controlo total para Administradores e SYSTEM."
