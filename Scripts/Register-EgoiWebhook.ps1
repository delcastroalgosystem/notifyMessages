<#
.SYNOPSIS
    Regista, lista ou remove o webhook transacional (e-mail) da E-goi que chama o NotifyMessages.

.DESCRIPTION
    O painel da E-goi não tem esta opção: faz-se pela API Slingshot v2 (POST/GET/DELETE /v2/webhooks, header ApiKey).
    Correr como Administrador no servidor da Api. Compatível com Windows PowerShell 5.1.

    - A chave da conta E-goi (ProviderSettings:Tenants:<SecretName>:ApiKey) e o WebhookSettings:SharedSecret são
      lidos do appsettings.Production.json da Api e NUNCA são mostrados: nas listagens, o token do endereço aparece
      como ****.
    - Endereço registado: <PublicUrl>/api/v1/webhooks/egoi?token=<SharedSecret>.
    - Ações registadas por omissão: sent, failed, canceled, bounce, abuse, remove, view, click (ver
      EgoiWebhookParser: sent = entregue ao servidor de destino; view/click = lido; bounce/abuse = devolvido).
    - Um webhook por conta E-goi: com vários tenants na mesma conta, regista-se uma vez.

.EXAMPLE
    $api = 'C:\SmartArena360\Install\Inetpub\WAN\APIs\SMA_ExtraTools\NotifyMessages\appsettings.Production.json'
    .\Register-EgoiWebhook.ps1 -ApiSettingsFile $api -Acao Listar
    .\Register-EgoiWebhook.ps1 -ApiSettingsFile $api -Acao Registar
    .\Register-EgoiWebhook.ps1 -ApiSettingsFile $api -Acao Remover
#>
param(
    [Parameter(Mandatory = $true)][string]$ApiSettingsFile,
    [ValidateSet('Listar', 'Registar', 'Remover')][string]$Acao = 'Listar',
    # SECRET_NAME do tenant em TENANT_PROVIDER_CONFIG (a conta E-goi a configurar)
    [string]$SecretName = 'CLUBE_SCC_Egoi',
    [string]$PublicUrl = 'https://extratools.smartarena.pt/NotifyMessages',
    [string[]]$Acoes = @('sent', 'failed', 'canceled', 'bounce', 'abuse', 'remove', 'view', 'click'),
    [string]$EgoiApi = 'https://slingshot.egoiapp.com/api/v2'
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
if (-not (Test-Path $ApiSettingsFile)) { throw "Ficheiro não encontrado: $ApiSettingsFile" }

$cfg = Get-Content $ApiSettingsFile -Raw | ConvertFrom-Json
$tenant = $cfg.ProviderSettings.Tenants.$SecretName
$apiKey = if ($tenant) { $tenant.ApiKey } else { $null }
$segredo = $cfg.WebhookSettings.SharedSecret
if (-not $apiKey) { throw "ProviderSettings:Tenants:${SecretName}:ApiKey vazio em $ApiSettingsFile." }
if (-not $segredo) { throw "WebhookSettings:SharedSecret vazio em $ApiSettingsFile." }

$base = $PublicUrl.TrimEnd('/') + '/api/v1/webhooks/egoi'
$url = $base + '?token=' + [Uri]::EscapeDataString($segredo)
$headers = @{ ApiKey = $apiKey }

function Ocultar([string]$texto) {
    if (-not $texto) { return $texto }
    return ($texto -replace '(token=)[^&\s"]+', '$1****')
}

function Listar {
    $lista = @(Invoke-RestMethod -Method Get -Uri "$EgoiApi/webhooks?channel=email" -Headers $headers -TimeoutSec 30)
    if ($lista.Count -eq 0 -or ($lista.Count -eq 1 -and $null -eq $lista[0])) {
        Write-Host 'Nenhum webhook de e-mail registado nesta conta E-goi.' -ForegroundColor Yellow
        return @()
    }
    foreach ($w in $lista) {
        $meu = if ($w.callbackUrl -and $w.callbackUrl.StartsWith($base, [StringComparison]::OrdinalIgnoreCase)) { '  <- NotifyMessages' } else { '' }
        Write-Host ("{0}  {1}  [{2}]{3}" -f $w.channel, (Ocultar $w.callbackUrl), (($w.actions) -join ', '), $meu)
    }
    return $lista
}

try {
    switch ($Acao) {
        'Listar' { Listar | Out-Null }
        'Registar' {
            $existentes = Listar
            if ($existentes | Where-Object { $_.callbackUrl -and $_.callbackUrl.StartsWith($base, [StringComparison]::OrdinalIgnoreCase) }) {
                Write-Host "Já existe um webhook para $base. Para o trocar: -Acao Remover e depois -Acao Registar." -ForegroundColor Yellow
                return
            }
            $corpo = @{ channel = 'email'; url = $url; actions = $Acoes } | ConvertTo-Json -Compress
            $r = Invoke-RestMethod -Method Post -Uri "$EgoiApi/webhooks" -Headers $headers -ContentType 'application/json' -Body $corpo -TimeoutSec 30
            Write-Host "Webhook registado (id $($r.webhookId)) para $base?token=****" -ForegroundColor Green
            Write-Host "Ações: $($Acoes -join ', ')"
        }
        'Remover' {
            $existentes = @(Listar | Where-Object { $_.callbackUrl -and $_.callbackUrl.StartsWith($base, [StringComparison]::OrdinalIgnoreCase) })
            if ($existentes.Count -eq 0) { Write-Host "Nenhum webhook para $base a remover." -ForegroundColor Yellow; return }
            foreach ($w in $existentes) {
                $alvo = [Uri]::EscapeDataString($w.callbackUrl)
                Invoke-RestMethod -Method Delete -Uri "$EgoiApi/webhooks?url=$alvo&channel=email" -Headers $headers -TimeoutSec 30 | Out-Null
            }
            Write-Host "Removido(s) $($existentes.Count) webhook(s) para $base." -ForegroundColor Green
        }
    }
}
catch {
    # A mensagem da E-goi não traz a chave; o endereço é ocultado por precaução.
    $detalhe = $_.ErrorDetails.Message
    if (-not $detalhe) { $detalhe = $_.Exception.Message }
    throw ("Pedido à E-goi falhou: " + (Ocultar $detalhe))
}
