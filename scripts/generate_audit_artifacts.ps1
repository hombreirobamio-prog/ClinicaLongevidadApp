param(
    [Parameter(Mandatory=$true)][string]$TestResultsPath,
    [Parameter(Mandatory=$true)][datetime]$RunStartedUtc,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/audit-packages')
)
$ErrorActionPreference = 'Stop'
$trx = Get-Item -LiteralPath $TestResultsPath
if ($trx.LastWriteTimeUtc -lt $RunStartedUtc.ToUniversalTime()) { throw 'Stale test evidence.' }
[xml]$results = Get-Content -LiteralPath $trx.FullName -Raw
$ns = [Xml.XmlNamespaceManager]::new($results.NameTable)
$ns.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
$summary = $results.SelectSingleNode('//t:ResultSummary', $ns)
$counts = $results.SelectSingleNode('//t:ResultSummary/t:Counters', $ns)
$times = $results.SelectSingleNode('//t:Times', $ns)
if (-not $summary -or -not $counts -or -not $times) { throw 'Invalid TRX structure.' }
if ($summary.outcome -ne 'Completed' -or [int]$counts.total -le 0 -or [int]$counts.passed -ne [int]$counts.total -or [int]$counts.executed -ne [int]$counts.total) { throw 'Tests failed, skipped or empty.' }
$cases = @($results.SelectNodes('//t:Results/t:UnitTestResult', $ns))
if ($cases.Count -ne [int]$counts.total -or @($cases | Where-Object { $_.outcome -ne 'Passed' }).Count -gt 0) { throw 'Individual test results do not match the summary.' }
if ([datetime]::Parse($times.start).ToUniversalTime() -lt $RunStartedUtc.ToUniversalTime()) { throw 'TRX belongs to an earlier run.' }
if ([datetime]::Parse($times.finish).ToUniversalTime() -gt [datetime]::UtcNow.AddMinutes(1)) { throw 'TRX finish is in the future.' }
if (-not $env:AUDIT_HMAC_KEY -or -not $env:AUDIT_HMAC_KEY_VERSION) { throw 'Package signing key and version are required.' }
$key = [Convert]::FromBase64String($env:AUDIT_HMAC_KEY)
if ($key.Length -lt 32) { throw 'Package key must contain at least 32 bytes.' }
$id = [guid]::NewGuid().ToString('N')
$output = [IO.Path]::GetFullPath($OutputDirectory)
$stage = Join-Path $output $id
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item -LiteralPath $trx.FullName -Destination (Join-Path $stage 'tests.trx')
$repo = Split-Path $PSScriptRoot -Parent
$commit = & git -C $repo rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source commit.' }
$status = @(& git -C $repo status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify working tree status.' }
$manifest = [ordered]@{
    SchemaVersion = 1
    Scope = 'technical-tests-only'
    RunId = $id
    CreatedUtc = [datetime]::UtcNow.ToString('o')
    RunStartedUtc = $RunStartedUtc.ToUniversalTime().ToString('o')
    Commit = "$commit".Trim()
    WorkingTreeDirty = ($status.Count -gt 0)
    KeyVersion = $env:AUDIT_HMAC_KEY_VERSION
    Files = @(@{ Path='tests.trx'; SHA256=(Get-FileHash (Join-Path $stage 'tests.trx') -Algorithm SHA256).Hash })
}
$manifestPath = Join-Path $stage 'package-manifest.json'
[IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
$hmac = [Security.Cryptography.HMACSHA256]::new($key)
try { $signature = ([BitConverter]::ToString($hmac.ComputeHash([IO.File]::ReadAllBytes($manifestPath))) -replace '-', '') }
finally { $hmac.Dispose() }
[IO.File]::WriteAllText((Join-Path $stage 'package-manifest.hmac'), $signature)
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $output "audit-tests-$id.zip"
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip)
& (Join-Path $PSScriptRoot 'verify_audit_package.ps1') -ZipPath $zip
if (-not $?) { throw 'Package verification failed.' }
Write-Host "Verified technical test package: $zip"
return $zip
