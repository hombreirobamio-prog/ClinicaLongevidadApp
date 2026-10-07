param(
    [string]$BaseDir = (Get-Location).Path,
    [string]$LocalAppData = $env:LOCALAPPDATA,
    [string]$ArtifactsDir = '',
    [switch]$IncludeHmac,
    [switch]$Help
)

if ($Help) {
    Write-Output "Usage: .\scripts\generate_companions.ps1 [-ArtifactsDir <path>] [-IncludeHmac]"
    Write-Output "Generates .sha256 and optional .hmac/.hmac.ver companion files for backups in the active artifacts folder."
    exit 0
}

# Generate companion files (.sha256 and optionally .hmac/.hmac.ver) for backups.
$artifactRoot = if (-not [string]::IsNullOrWhiteSpace($ArtifactsDir)) {
    $ArtifactsDir
} elseif (-not [string]::IsNullOrWhiteSpace($LocalAppData)) {
    Join-Path $LocalAppData 'ClinicaLongevidadAppArtifacts'
} else {
    Join-Path $BaseDir 'artifacts'
}
$localBackups = Join-Path $artifactRoot 'backups'

$paths = @()
if (Test-Path $localBackups) { $paths += $localBackups }

if ($paths.Count -eq 0) {
    Write-Error "No backup path found. Checked: $localBackups"
    exit 2
}

if ($IncludeHmac -and [string]::IsNullOrWhiteSpace($env:AUDIT_HMAC_KEY)) {
    throw 'AUDIT_HMAC_KEY is required when -IncludeHmac is specified.'
}
if ($IncludeHmac -and [string]::IsNullOrWhiteSpace($env:AUDIT_HMAC_KEY_VERSION)) {
    throw 'AUDIT_HMAC_KEY_VERSION is required when -IncludeHmac is specified.'
}
$hmacKeyBytes = $null
if ($IncludeHmac) {
    try {
        $hmacKeyBytes = [Convert]::FromBase64String($env:AUDIT_HMAC_KEY)
    }
    catch {
        throw 'AUDIT_HMAC_KEY must be Base64-encoded when -IncludeHmac is specified.'
    }
    if ($hmacKeyBytes.Length -lt 32) {
        throw 'AUDIT_HMAC_KEY must decode to at least 32 bytes when -IncludeHmac is specified.'
    }
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
            $data = [System.IO.File]::ReadAllBytes($f)
            # Use the typed constructor (avoids PowerShell expanding the byte[] into many arguments)
            $h = [System.Security.Cryptography.HMACSHA256]::new($hmacKeyBytes)
            $mac = $h.ComputeHash($data)
            ([BitConverter]::ToString($mac) -replace '-','').ToLower() | Out-File -FilePath ($f + '.hmac') -Encoding ascii
            $ver = $env:AUDIT_HMAC_KEY_VERSION
            $ver | Out-File -FilePath ($f + '.hmac.ver') -Encoding ascii
            Write-Host "Wrote: $($f + '.hmac') and .hmac.ver"
        }
    }
}

Write-Host "Done."
exit 0
