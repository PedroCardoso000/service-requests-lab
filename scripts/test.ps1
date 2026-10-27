param([switch]$Coverage)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    $arguments = @('test', 'tests/ServiceRequests.Tests/ServiceRequests.Tests.csproj', '--logger', 'trx;LogFileName=tests.trx', '--results-directory', 'artifacts/test-results')
    if ($Coverage) {
        $arguments += @('/p:CollectCoverage=true', '/p:CoverletOutput=../../artifacts/coverage/', '/p:CoverletOutputFormat=opencover', '/p:ExcludeByFile=**/Data/Migrations/*.cs')
    }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Testes falharam. Consulte artifacts/test-results/tests.trx.' }
} finally { Pop-Location }
