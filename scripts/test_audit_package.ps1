$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot ('../artifacts/package-tests/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root -Force | Out-Null
$root = (Resolve-Path $root).Path
$oldKey = $env:AUDIT_HMAC_KEY
$oldVersion = $env:AUDIT_HMAC_KEY_VERSION
$env:AUDIT_HMAC_KEY = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('0123456789abcdef0123456789abcdef'))
$env:AUDIT_HMAC_KEY_VERSION = 'synthetic-test'
$tests = @()
try {
    $start = [datetime]::UtcNow.AddSeconds(-2)
    $now = [datetime]::UtcNow.ToString('o')
    $trx = Join-Path $root 'input.trx'
    $valid = "<TestRun xmlns='http://microsoft.com/schemas/VisualStudio/TeamTest/2010'><Times start='$now' finish='$now'/><Results><UnitTestResult outcome='Passed'/></Results><ResultSummary outcome='Completed'><Counters total='1' passed='1' executed='1'/></ResultSummary></TestRun>"
    Set-Content $trx $valid
    $zip = & "$PSScriptRoot/generate_audit_artifacts.ps1" -TestResultsPath $trx -RunStartedUtc $start -OutputDirectory $root
    $tests += 'valid: PASS'
    $portable = Join-Path $root 'moved.zip'
    Copy-Item -LiteralPath $zip -Destination $portable
    & "$PSScriptRoot/verify_audit_package.ps1" -ZipPath $portable
    $tests += 'portable: PASS'
    function Assert-Rejected([string]$name, [scriptblock]$action) {
        $rejected = $false
        try { & $action | Out-Null } catch { $rejected = $true }
        if (-not $rejected) { throw "Unexpected acceptance: $name" }
        return "$name`: PASS"
    }
    $tests += Assert-Rejected 'stale' { & "$PSScriptRoot/generate_audit_artifacts.ps1" -TestResultsPath $trx -RunStartedUtc ([datetime]::UtcNow.AddMinutes(1)) -OutputDirectory $root }
    Set-Content $trx ($valid.Replace("passed='1'", "passed='0'"))
    $tests += Assert-Rejected 'failed-tests' { & "$PSScriptRoot/generate_audit_artifacts.ps1" -TestResultsPath $trx -RunStartedUtc $start -OutputDirectory $root }
    Set-Content $trx $valid
    $savedKey = $env:AUDIT_HMAC_KEY
    $env:AUDIT_HMAC_KEY = $null
    $tests += Assert-Rejected 'missing-key' { & "$PSScriptRoot/verify_audit_package.ps1" -ZipPath $zip }
    $env:AUDIT_HMAC_KEY = $savedKey
    $archive = [IO.Compression.ZipFile]::Open($portable, [IO.Compression.ZipArchiveMode]::Update)
    try {
        $entry = $archive.GetEntry('tests.trx')
        $entry.Delete()
        $writer = [IO.StreamWriter]::new($archive.CreateEntry('tests.trx').Open())
        try { $writer.Write('modified') } finally { $writer.Dispose() }
    } finally { $archive.Dispose() }
    $tests += Assert-Rejected 'tampered-evidence' { & "$PSScriptRoot/verify_audit_package.ps1" -ZipPath $portable }
    Copy-Item -LiteralPath $zip -Destination $portable -Force
    $archive = [IO.Compression.ZipFile]::Open($portable, [IO.Compression.ZipArchiveMode]::Update)
    try { $null = $archive.CreateEntry('../unexpected.txt') } finally { $archive.Dispose() }
    $tests += Assert-Rejected 'unexpected-entry' { & "$PSScriptRoot/verify_audit_package.ps1" -ZipPath $portable }
    $tests | Set-Content (Join-Path $root 'results.txt')
    $tests | Write-Host
}
finally { $env:AUDIT_HMAC_KEY = $oldKey; $env:AUDIT_HMAC_KEY_VERSION = $oldVersion }

exit 0
