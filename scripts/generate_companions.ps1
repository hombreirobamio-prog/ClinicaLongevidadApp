param(
    [switch]$IncludeHmac
)

# Generate companion files (.sha256 and optionally .hmac/.hmac.ver) for backups
$localBackups = Join-Path $env:LOCALAPPDATA 'ClinicaLongevidadApp\backups'
$artifactBackups = Join-Path (Get-Location) 'artifacts\backups'

$paths = @()
if (Test-Path $localBackups) { $paths += $localBackups }
if (Test-Path $artifactBackups) { $paths += $artifactBackups }

if ($paths.Count -eq 0) {
    Write-Error "No backup paths found. Checked: $localBackups and $artifactBackups"
    exit 2
}

foreach ($p in $paths) {
    Write-Host "Scanning: $p"
    Get-ChildItem -Path $p -Filter '*.db' -File | ForEach-Object {
        $f = $_.FullName
        try {
            # Prefer Get-FileHash when available
            $hash = (Get-FileHash -Path $f -Algorithm SHA256 -ErrorAction Stop).Hash
        }
        catch {
            # Fallback to .NET streaming hash
            $sha = [System.Security.Cryptography.SHA256]::Create()
            $fs = [System.IO.File]::Open($f, 'Open', 'Read', 'Read')
            try {
                $raw = $sha.ComputeHash($fs)
                $hash = [System.BitConverter]::ToString($raw) -replace '-',''
            }
            finally { $fs.Close() }
        }
        $hash = $hash.ToLower()
        $hash | Out-File -FilePath ($f + '.sha256') -Encoding ascii
        Write-Host "Wrote: $($f + '.sha256')"

        if ($IncludeHmac) {
            if (-not $env:AUDIT_HMAC_KEY) { Write-Host "AUDIT_HMAC_KEY not set; skipping HMAC for $f"; continue }
            $kb = [System.Text.Encoding]::UTF8.GetBytes($env:AUDIT_HMAC_KEY)
            $data = [System.IO.File]::ReadAllBytes($f)
            $mac = (New-Object System.Security.Cryptography.HMACSHA256 $kb).ComputeHash($data)
            ([BitConverter]::ToString($mac) -replace '-','').ToLower() | Out-File -FilePath ($f + '.hmac') -Encoding ascii
            ($env:AUDIT_HMAC_KEY_VERSION ?? '1') | Out-File -FilePath ($f + '.hmac.ver') -Encoding ascii
            Write-Host "Wrote: $($f + '.hmac') and .hmac.ver"
        }
    }
}

Write-Host "Done."
exit 0
