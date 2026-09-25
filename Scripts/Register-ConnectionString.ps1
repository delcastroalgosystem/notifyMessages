cd C:\Developer\DelCastro\NotifyMessages
$cs = (Get-Content "$env:APPDATA\Microsoft\UserSecrets\dotnet-Smartarena.ExtraTools.Web-b6f2a1d4-3c9e-4b7a-9e2d-7a1f5c8e6d3b\secrets.json" -Raw | ConvertFrom-Json).'ConnectionStrings:DefaultConnection'
dotnet user-secrets set "ConnectionStrings:DefaultConnection" $cs --project NotifyMessages.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" $cs --project NotifyMessages.Worker
