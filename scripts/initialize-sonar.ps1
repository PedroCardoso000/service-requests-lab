# Inicializa SOMENTE a nova instância local deste laboratório com admin/admin.
# Credenciais são protegidas pelo DPAPI do Windows e não entram no Git.
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    if (Test-Path .local/sonar-token.clixml) { Write-Host 'Token local já configurado.'; return }
    $url = 'http://localhost:9000'
    if ((Invoke-RestMethod "$url/api/system/status" -TimeoutSec 10).status -ne 'UP') { throw 'Aguarde o SonarQube ficar UP.' }
    New-Item -ItemType Directory -Force .local | Out-Null
    if (Test-Path .local/sonar-admin.clixml) {
        $credential = Import-Clixml .local/sonar-admin.clixml
    } else {
        $password = 'Lab!a9-' + [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
        $initial = @{Authorization='Basic ' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('admin:admin'))}
        Invoke-RestMethod "$url/api/users/change_password" -Method Post -Headers $initial -Body @{login='admin'; previousPassword='admin'; password=$password} | Out-Null
        $credential = [PSCredential]::new('admin', (ConvertTo-SecureString $password -AsPlainText -Force))
        $credential | Export-Clixml .local/sonar-admin.clixml
    }
    $auth = @{Authorization='Basic ' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('admin:' + $credential.GetNetworkCredential().Password))}
    $projects = Invoke-RestMethod "$url/api/projects/search?projects=service-requests-lab" -Headers $auth
    if ($projects.components.Count -eq 0) {
        Invoke-RestMethod "$url/api/projects/create" -Method Post -Headers $auth -Body @{project='service-requests-lab';name='Service Requests Lab';visibility='private'} | Out-Null
    }
    $token = Invoke-RestMethod "$url/api/user_tokens/generate" -Method Post -Headers $auth -Body @{name=('local-analysis-' + [Guid]::NewGuid().ToString('N'));type='PROJECT_ANALYSIS_TOKEN';projectKey='service-requests-lab';expirationDate=(Get-Date).AddDays(30).ToString('yyyy-MM-dd')}
    ConvertTo-SecureString $token.token -AsPlainText -Force | Export-Clixml .local/sonar-token.clixml
    Write-Host 'Projeto criado e token de análise local configurado (validade: 30 dias).'
} finally { Pop-Location }
