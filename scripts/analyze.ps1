param([string]$SonarUrl = 'http://localhost:9000')
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $token = $env:SONAR_TOKEN
    if (!$token -and (Test-Path .local/sonar-token.clixml)) {
        $token = [PSCredential]::new('token', (Import-Clixml .local/sonar-token.clixml)).GetNetworkCredential().Password
    }
    if (!$token) { throw 'Execute initialize-sonar.ps1 na instância local ou defina SONAR_TOKEN.' }
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao restaurar ferramentas.' }
    dotnet sonarscanner begin /k:service-requests-lab "/d:sonar.host.url=$SonarUrl" "/d:sonar.token=$token" /d:sonar.scanner.scanAll=false /d:sonar.cs.opencover.reportsPaths=artifacts/coverage/coverage.opencover.xml /d:sonar.cs.vstest.reportsPaths=artifacts/test-results/*.trx '/d:sonar.exclusions=**/Data/Migrations/**' /d:sonar.qualitygate.wait=true
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao iniciar scanner.' }
    dotnet build tests/ServiceRequests.Tests/ServiceRequests.Tests.csproj --no-incremental
    if ($LASTEXITCODE -ne 0) { throw 'Falha na compilação durante análise.' }
    & ./scripts/test.ps1 -Coverage
    dotnet sonarscanner end "/d:sonar.token=$token"
    if ($LASTEXITCODE -ne 0) { throw 'Análise falhou ou Quality Gate reprovou. Consulte a saída e o painel do SonarQube.' }
} finally { $token = $null; Pop-Location }
