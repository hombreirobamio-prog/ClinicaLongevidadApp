param([Parameter(Mandatory=$true)][string]$ZipPath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ZipPath).Path)
try {
    # Verify in the archive: never extract untrusted paths.
    $names = @($zip.Entries | ForEach-Object { $_.FullName })
    if ($names.Count -ne 3 -or @($names | Sort-Object -Unique).Count -ne 3) { throw 'Unexpected or duplicate archive entries.' }
    foreach ($name in @('package-manifest.json','package-manifest.hmac','tests.trx')) {
        if ($names -cnotcontains $name) { throw 'Archive inventory mismatch.' }
    }
    function Read-Entry([string]$name) {
        $entry = $zip.GetEntry($name)
        if ($entry.Length -gt 32MB) { throw 'Evidence entry exceeds size limit.' }
        $stream = $entry.Open()
        $memory = [IO.MemoryStream]::new()
        try { $stream.CopyTo($memory); return ,$memory.ToArray() }
        finally { $stream.Dispose(); $memory.Dispose() }
    }
    $raw = Read-Entry 'package-manifest.json'
    $manifest = [Text.Encoding]::UTF8.GetString($raw) | ConvertFrom-Json
    if ($manifest.SchemaVersion -ne 1 -or $manifest.Scope -cne 'technical-tests-only') { throw 'Unsupported package schema/scope.' }
    if (-not $env:AUDIT_HMAC_KEY_VERSION -or $manifest.KeyVersion -cne $env:AUDIT_HMAC_KEY_VERSION) { throw 'Exact package key version unavailable.' }
    $key = [Convert]::FromBase64String($env:AUDIT_HMAC_KEY)
    if ($key.Length -lt 32) { throw 'Invalid package key.' }
    $mac = [Security.Cryptography.HMACSHA256]::new($key)
    try { $actual = ([BitConverter]::ToString($mac.ComputeHash($raw)) -replace '-', '') }
    finally { $mac.Dispose() }
    $expected = [Text.Encoding]::UTF8.GetString((Read-Entry 'package-manifest.hmac'))
    if ($expected -notmatch '^[0-9A-Fa-f]{64}$' -or $actual -cne $expected) { throw 'Package authentication failed.' }
    if (@($manifest.Files).Count -ne 1 -or $manifest.Files[0].Path -cne 'tests.trx') { throw 'Manifest inventory mismatch.' }
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $hash = ([BitConverter]::ToString($sha.ComputeHash((Read-Entry 'tests.trx'))) -replace '-', '') }
    finally { $sha.Dispose() }
    if ($hash -cne $manifest.Files[0].SHA256) { throw 'Evidence hash mismatch.' }
    Write-Host "Authenticated package $($manifest.RunId); scope: technical tests only."
}
finally { $zip.Dispose() }
