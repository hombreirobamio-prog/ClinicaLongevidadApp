param(
    [string]$ManifestPath
)

function Write-Usage {
    Write-Host "Usage: powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify_audit_manifest.ps1 [-ManifestPath <path>]" -ForegroundColor Yellow
}

if (-not $ManifestPath) {
    $candidates = Get-ChildItem -Path (Get-Location) -Filter 'audit_manifest_*.txt' -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTime
    if (-not $candidates -or $candidates.Count -eq 0) {
        Write-Error "No manifest specified and no 'audit_manifest_*.txt' found in current directory.";
        Write-Usage; exit 2
    }
    $ManifestPath = $candidates[-1].FullName
}

if (-not (Test-Path $ManifestPath)) {
    Write-Error "Manifest file not found: $ManifestPath"; exit 2
}

Write-Host "Verifying manifest: $ManifestPath"

$lines = Get-Content -Path $ManifestPath -ErrorAction Stop

$entries = @()
$current = $null
foreach ($line in $lines) {
    # trim whitespace both ends so lines with indentation like '  SHA256: ...' match correctly
    $trim = $line.Trim()
    if ($trim -match '^File:\s*(.+)$') {
        if ($current -ne $null) { $entries += $current }
        $current = [PSCustomObject]@{
            File = $matches[1].Trim()
            ExpectedSHA = $null
            HMAC = $null
            HMACVersion = $null
        }
        continue
    }
    if ($current -ne $null) {
        if ($trim -match '^SHA256:\s*([0-9a-fA-F]+)') { $current.ExpectedSHA = $matches[1].ToLower(); continue }
        if ($trim -match '^HMAC:\s*([0-9a-fA-F]+)') { $current.HMAC = $matches[1].ToLower(); continue }
        if ($trim -match '^HMAC.Version:\s*(.+)') { $current.HMACVersion = $matches[1].Trim(); continue }
    }
}
if ($current -ne $null) { $entries += $current }

if ($entries.Count -eq 0) {
    Write-Warning "No file entries found in manifest."; exit 2
}

$errors = @()
$warnings = @()
$checked = 0

foreach ($e in $entries) {
    $checked++
    $filePath = $e.File
    Write-Host "\nChecking: $filePath"
    if (-not (Test-Path $filePath)) {
        $errors += "MISSING: $filePath"
        Write-Host "  -> File not found" -ForegroundColor Red
        continue
    }
    try {
        $hash = (Get-FileHash -Path $filePath -Algorithm SHA256 -ErrorAction Stop).Hash.ToLower()
    } catch {
        $errors += "HASH-ERROR: $filePath -> $_"
        Write-Host "  -> Error computing hash: $_" -ForegroundColor Red
        continue
    }
    if ($e.ExpectedSHA) {
        if ($hash -eq $e.ExpectedSHA) {
            Write-Host "  SHA256 matches: $hash" -ForegroundColor Green
        } else {
            $errors += "SHA-MISMATCH: $filePath (expected: $($e.ExpectedSHA), actual: $hash)"
            Write-Host "  SHA256 MISMATCH! expected: $($e.ExpectedSHA) actual: $hash" -ForegroundColor Red
        }
    } else {
        $warnings += "NO-SHA-IN-MANIFEST: $filePath"
        Write-Host "  No SHA value present in manifest for this file. Computed: $hash" -ForegroundColor Yellow
    }

    # check companion .sha256 file
    $shaFile = "$filePath.sha256"
    if (Test-Path $shaFile) {
        try { $shaContent = (Get-Content $shaFile -ErrorAction Stop) -join ""; $shaContent = $shaContent.Trim() } catch { $shaContent = $null }
        if ($shaContent) {
            if ($shaContent.ToLower() -ne $hash) {
                $errors += "SHAFILE-MISMATCH: $shaFile (content: $shaContent, actual: $hash)"
                Write-Host "  Companion .sha256 content mismatch: $shaContent" -ForegroundColor Red
            } else {
                Write-Host "  Companion .sha256 matches." -ForegroundColor Green
            }
        }
    }

    # check presence of hmac and hmac.ver
    $hmacFile = "$filePath.hmac"
    $hmacVerFile = "$filePath.hmac.ver"
    if ($e.HMAC) {
        if (Test-Path $hmacFile) {
            $hmacContent = (Get-Content $hmacFile -ErrorAction SilentlyContinue) -join ""; $hmacContent = $hmacContent.Trim()
            if ($hmacContent.ToLower() -ne $e.HMAC) {
                $warnings += "HMAC-MISMATCH: $hmacFile (manifest: $($e.HMAC), file: $hmacContent)"
                Write-Host "  HMAC in manifest differs from companion .hmac" -ForegroundColor Yellow
            } else { Write-Host "  HMAC present and matches manifest." -ForegroundColor Green }
        } else {
            $warnings += "HMAC-MISSING-FILE: $hmacFile"
            Write-Host "  HMAC expected in manifest but companion .hmac file not found." -ForegroundColor Yellow
        }
    } else {
        if (Test-Path $hmacFile) { Write-Host "  Companion .hmac exists but manifest has no HMAC entry." -ForegroundColor Yellow }
    }
    if ($e.HMACVersion) {
        if (Test-Path $hmacVerFile) {
            $ver = (Get-Content $hmacVerFile -ErrorAction SilentlyContinue) -join ""; $ver = $ver.Trim()
            if ($ver -ne $e.HMACVersion) { $warnings += "HMACVER-MISMATCH: $hmacVerFile (manifest: $($e.HMACVersion), file: $ver)"; Write-Host "  HMAC.version mismatch" -ForegroundColor Yellow } else { Write-Host "  HMAC.version present and matches." -ForegroundColor Green }
        } else { $warnings += "HMACVER-MISSING-FILE: $hmacVerFile"; Write-Host "  HMAC.version expected but file not found." -ForegroundColor Yellow }
    }
}

Write-Host "\nVerification summary:" -ForegroundColor Cyan
Write-Host "  Files checked: $checked"
Write-Host "  Errors: $($errors.Count)"
Write-Host "  Warnings: $($warnings.Count)"

if ($errors.Count -gt 0) {
    Write-Host "\nErrors detail:" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host " - $_" }
}
if ($warnings.Count -gt 0) {
    Write-Host "\nWarnings detail:" -ForegroundColor Yellow
    $warnings | ForEach-Object { Write-Host " - $_" }
}

if ($errors.Count -gt 0) { exit 1 } else { exit 0 }
