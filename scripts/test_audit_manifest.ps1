param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/audit-manifest-tests'))
$ErrorActionPreference = 'Stop'
$previousKey = $env:AUDIT_HMAC_KEY
$previousVersion = $env:AUDIT_HMAC_KEY_VERSION
$root = Join-Path $OutputDirectory ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root -Force | Out-Null
$root = (Resolve-Path $root).Path
$file = Join-Path $root 'synthetic.db'
$manifest = Join-Path $root 'manifest.txt'
$key = [Text.Encoding]::UTF8.GetBytes('synthetic-regression-key-no-real-data')
$results = @()
try {
    $env:AUDIT_HMAC_KEY = [Convert]::ToBase64String($key)
    $env:AUDIT_HMAC_KEY_VERSION = 'test-v1'
    function Reset-Fixture {
        [IO.File]::WriteAllText($file, 'Synthetic backup fixture')
        $sha = (Get-FileHash $file -Algorithm SHA256).Hash
        $hmac = [Security.Cryptography.HMACSHA256]::new($key)
        try { $mac = ([BitConverter]::ToString($hmac.ComputeHash([IO.File]::ReadAllBytes($file))) -replace '-', '') }
        finally { $hmac.Dispose() }
        Set-Content "$file.sha256" $sha
        Set-Content "$file.hmac" $mac
        Set-Content "$file.hmac.ver" 'test-v1'
        Set-Content $manifest @('File: synthetic.db', "SHA256: $sha", "HMAC: $mac", 'HMAC.Version: test-v1')
        $env:AUDIT_HMAC_KEY = [Convert]::ToBase64String($key)
        $env:AUDIT_HMAC_KEY_VERSION = 'test-v1'
    }
    $cases = @(
        @{ Name='valid'; Expected=0; Change={} },
        @{ Name='forged-hmac'; Expected=1; Change={
            Set-Content "$file.hmac" ('0'*64)
            (Get-Content $manifest) -replace '^HMAC:.*$', ('HMAC: '+('0'*64)) | Set-Content $manifest
        } },
        @{ Name='missing-version'; Expected=1; Change={
            Remove-Item -LiteralPath "$file.hmac.ver"
            Get-Content $manifest | Where-Object { $_ -notmatch '^HMAC.Version:' } | Set-Content (Join-Path $root 'without-version.txt')
            Copy-Item (Join-Path $root 'without-version.txt') $manifest -Force
        } },
        @{ Name='missing-key'; Expected=1; Change={ $env:AUDIT_HMAC_KEY = $null } },
        @{ Name='wrong-version'; Expected=1; Change={ $env:AUDIT_HMAC_KEY_VERSION = 'test-v2' } },
        @{ Name='tampered-backup'; Expected=1; Change={ [IO.File]::AppendAllText($file, 'tampered') } },
        @{ Name='missing-sha'; Expected=1; Change={ Remove-Item -LiteralPath "$file.sha256" } },
        @{ Name='wrong-key'; Expected=1; Change={ $env:AUDIT_HMAC_KEY = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('different-synthetic-key')) } }
    )
    foreach ($case in $cases) {
        Reset-Fixture
        & $case.Change
        $log = Join-Path $root ($case.Name + '.txt')
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'verify_audit_manifest.ps1') -ManifestPath $manifest -RequireHmac *> $log
        $actual = $LASTEXITCODE
        $results += [pscustomobject]@{ Test=$case.Name; Expected=$case.Expected; Actual=$actual; Passed=($actual -eq $case.Expected) }
    }
    $results | Export-Csv (Join-Path $root 'results.csv') -NoTypeInformation
    $results | Format-Table
    if ($results.Passed -contains $false) { throw "Manifest regression failed. Evidence: $root" }
    Write-Host "All manifest tests passed. Evidence: $root"
}
finally {
    $env:AUDIT_HMAC_KEY = $previousKey
    $env:AUDIT_HMAC_KEY_VERSION = $previousVersion
}

exit 0
