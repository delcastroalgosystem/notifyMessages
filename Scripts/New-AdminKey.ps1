<#
.SYNOPSIS
    Gera uma chave de plataforma para a API de administração (/api/v1/admin, header X-Admin-Key).

.DESCRIPTION
    A Api do NotifyMessages só guarda o HASH (AdminApi:KeyHashes:<n>); a chave fica em quem chama a API
    (ex. Smartarena.ExtraTools.Web, em NotifyMessages:AdminKey).

    Em dev, com -ApiProject e -ClientProject, grava os dois nos User Secrets e NÃO mostra a chave.
    Em produção, com -ApiSettingsFile e -ClientSettingsFile (os appsettings.Production.json da Api e do cliente,
    criados por New-ProductionSettings.ps1 / New-ConfiguracaoProducaoWeb.ps1), grava os dois nesses ficheiros e NÃO
    mostra a chave; as permissões dos ficheiros mantêm-se. Correr como Administrador.
    Sem cliente (-ClientProject / -ClientSettingsFile), mostra a chave uma única vez para a guardar no cliente.

    Para trocar a chave sem cortes: gerar uma nova com outro -Slot (ex. 1), atualizar o cliente, e depois
    apagar o slot antigo.

.EXAMPLE
    .\Scripts\New-AdminKey.ps1 -ApiProject .\NotifyMessages.Api `
        -ClientProject C:\Developer\Smartmove\Smartarena\ExtraTools\App\Smartarena.ExtraTools.Web

.EXAMPLE
    .\Scripts\New-AdminKey.ps1 -ApiSettingsFile C:\inetpub\NotifyMessages\appsettings.Production.json `
        -ClientSettingsFile C:\inetpub\ExtraTools\appsettings.Production.json
#>
param(
    [string]$ApiProject,
    [string]$ClientProject,
    [string]$ApiSettingsFile,
    [string]$ClientSettingsFile,
    [string]$ClientKeyName = "NotifyMessages:AdminKey",
    [int]$Slot = 0
)

$ErrorActionPreference = "Stop"
if (-not $ApiProject -and -not $ApiSettingsFile) { throw "Indique -ApiProject (dev, User Secrets) ou -ApiSettingsFile (produção)." }
foreach ($f in @($ApiSettingsFile, $ClientSettingsFile)) { if ($f -and -not (Test-Path $f)) { throw "Ficheiro não encontrado: $f" } }

# Grava um valor num appsettings JSON pelo caminho "A:B:C" (cria as secções que faltam); "A:B:0" = posição num array.
function Set-JsonValue([string]$File, [string]$KeyPath, [string]$Value) {
    $root = Get-Content $File -Raw | ConvertFrom-Json
    $parts = $KeyPath.Split(':')
    $node = $root
    for ($i = 0; $i -lt $parts.Length - 1; $i++) {
        $p = $parts[$i]; $next = $parts[$i + 1]
        if (-not ($node.PSObject.Properties.Name -contains $p) -or $null -eq $node.$p) {
            $node | Add-Member -NotePropertyName $p -NotePropertyValue ($(if ($next -match '^\d+$') { @() } else { New-Object PSObject })) -Force
        }
        if ($next -match '^\d+$' -and $i -eq $parts.Length - 2) {
            $arr = @($node.$p); $idx = [int]$next
            while ($arr.Count -le $idx) { $arr += '' }
            $arr[$idx] = $Value
            $node.$p = $arr
            [System.IO.File]::WriteAllText($File, ($root | ConvertTo-Json -Depth 10), (New-Object System.Text.UTF8Encoding($false)))
            return
        }
        $node = $node.$p
    }
    $last = $parts[-1]
    if ($node.PSObject.Properties.Name -contains $last) { $node.$last = $Value } else { $node | Add-Member -NotePropertyName $last -NotePropertyValue $Value }
    # Escrever o conteúdo mantém as permissões (ACL) do ficheiro
    [System.IO.File]::WriteAllText($File, ($root | ConvertTo-Json -Depth 10), (New-Object System.Text.UTF8Encoding($false)))
}

# Compatível com Windows PowerShell 5.1 (.NET Framework) e PowerShell 7
$bytes = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
$key = -join ($bytes | ForEach-Object { $_.ToString("X2") })

$sha256 = [System.Security.Cryptography.SHA256]::Create()
try { $hashBytes = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($key)) } finally { $sha256.Dispose() }
$hash = -join ($hashBytes | ForEach-Object { $_.ToString("X2") })

if ($ApiSettingsFile) {
    Set-JsonValue $ApiSettingsFile "AdminApi:KeyHashes:$Slot" $hash
    Write-Host "Hash gravado em AdminApi:KeyHashes:$Slot ($ApiSettingsFile)." -ForegroundColor Green
} else {
    dotnet user-secrets set "AdminApi:KeyHashes:$Slot" $hash --project $ApiProject | Out-Null
    Write-Host "Hash gravado em AdminApi:KeyHashes:$Slot ($ApiProject)." -ForegroundColor Green
}

if ($ClientSettingsFile) {
    Set-JsonValue $ClientSettingsFile $ClientKeyName $key
    Write-Host "Chave gravada em $ClientKeyName ($ClientSettingsFile). Não foi mostrada." -ForegroundColor Green
}
elseif ($ClientProject) {
    dotnet user-secrets set $ClientKeyName $key --project $ClientProject | Out-Null
    Write-Host "Chave gravada em $ClientKeyName ($ClientProject). Não foi mostrada." -ForegroundColor Green
}
else {
    Write-Host "Chave de plataforma (X-Admin-Key) - guarde-a agora no cliente, não volta a ser mostrada:" -ForegroundColor Yellow
    Write-Host $key -ForegroundColor Yellow
}
