param(
    [string]$BaseDir = (Get-Location).Path,
    [string]$LocalAppData = $env:LOCALAPPDATA,
    [switch]$IncludeHmac
)

# Generate companion files (.sha256 and optionally .hmac/.hmac.ver) for backups
$localBackups = if ([string]::IsNullOrWhiteSpace($LocalAppData)) { Join-Path $BaseDir 'backups' } else { Join-Path $LocalAppData 'ClinicaLongevidadApp\backups' }
$artifactBackups = Join-Path $BaseDir 'artifacts\backups'

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
            # Support either a base64-encoded key or a raw string key. Convert to a byte[] for the HMAC constructor.
            try {
                $kb = [Convert]::FromBase64String($env:AUDIT_HMAC_KEY)
            }
            catch {
                $kb = [System.Text.Encoding]::UTF8.GetBytes($env:AUDIT_HMAC_KEY)
            }
            $data = [System.IO.File]::ReadAllBytes($f)
            # Use the typed constructor (avoids PowerShell expanding the byte[] into many arguments)
            $h = [System.Security.Cryptography.HMACSHA256]::new($kb)
            $mac = $h.ComputeHash($data)
            ([BitConverter]::ToString($mac) -replace '-','').ToLower() | Out-File -FilePath ($f + '.hmac') -Encoding ascii
            ($env:AUDIT_HMAC_KEY_VERSION ?? '1') | Out-File -FilePath ($f + '.hmac.ver') -Encoding ascii
            Write-Host "Wrote: $($f + '.hmac') and .hmac.ver"
        }
    }
}

Write-Host "Done."
exit 0
