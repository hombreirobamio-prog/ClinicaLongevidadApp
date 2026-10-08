param(
    [string]$ArtifactsDir = '',
    [string]$DbPath = '',
    [int]$BatchSize = 100,
    [switch]$Help
)

if ($Help) {
    Write-Output "Usage: .\scripts\run_backfill.ps1 [-ArtifactsDir <path>] [-DbPath <path>] [-BatchSize <n>]"
    Write-Output "Runs a preview dry-run and (optionally) applies backfill batches against an explicitly selected isolated audit DB copy."
    exit 0
}

# Backfill can alter audit records. Never select the active database implicitly.
if ([string]::IsNullOrWhiteSpace($DbPath)) {
    Write-Error 'DbPath is required. Provide the path to an isolated copy of the audit database; the active database is not selected automatically.'
    exit 2
}

# Script to preview, dry-run and apply backfill using tools/RotateKeys
# Usage: powershell -ExecutionPolicy Bypass -File .\scripts\run_backfill.ps1 -DbPath "C:\path\to\ClinicaLongevidad.db" -BatchSize 100

function Run-Command($args) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "dotnet"
    $psi.Arguments = $args
    $psi.WorkingDirectory = (Resolve-Path "$(Split-Path -Path $PSScriptRoot -Parent)")
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true

    $p = [System.Diagnostics.Process]::Start($psi)
    $out = $p.StandardOutput.ReadToEnd()
    $err = $p.StandardError.ReadToEnd()
    $p.WaitForExit()
    return @{ ExitCode = $p.ExitCode; StdOut = $out; StdErr = $err }
}

Write-Host "Backfill helper script"
Write-Host "DB: $DbPath`nBatch size: $BatchSize`n"

if (-not (Test-Path $DbPath)) {
    Write-Error "Database file not found: $DbPath"
    exit 2
}

# 1) Preview
Write-Host "Running preview..."
$res = Run-Command "run --project tools/RotateKeys -- backfill preview --db \"$DbPath\""
Write-Host $res.StdOut
if ($res.ExitCode -ne 0) { Write-Error "Preview failed: $($res.StdErr)"; exit $res.ExitCode }

# Parse preview count
$match = [regex]::Match($res.StdOut, 'Backfill preview: (\d+) rows would be processed', 'IgnoreCase')
if (-not $match.Success) {
    Write-Error "Could not parse preview output. Aborting."
    exit 3
}
$total = [int]$match.Groups[1].Value
Write-Host "Total rows to process: $total"

if ($total -eq 0) {
    Write-Host "Nothing to do."; exit 0
}

# 2) Dry-run full by batches
$processedTotal = 0
$batch = [int]$BatchSize
Write-Host "Starting dry-run in batches of $batch..."
while ($processedTotal -lt $total) {
    Write-Host "Dry-run batch starting. Processed so far: $processedTotal / $total"
    $res = Run-Command "run --project tools/RotateKeys -- backfill apply --db \"$DbPath\" --batch $batch --dryrun"
    Write-Host $res.StdOut
    if ($res.ExitCode -ne 0) { Write-Error "Dry-run failed: $($res.StdErr)"; exit $res.ExitCode }

    # parse processed
    $m = [regex]::Match($res.StdOut, 'Backfill processed=(\d+) created=(\d+) skipped=(\d+) \(dryRun=(True|False)\)', 'IgnoreCase')
    if ($m.Success) {
        $proc = [int]$m.Groups[1].Value
        $processedTotal += $proc
        Write-Host "Batch simulated: $proc rows (cumulative simulated: $processedTotal)"
    }
    else {
        Write-Warning "Could not parse dry-run output for batch; stopping."
        break
    }

    if ($processedTotal -ge $total) { break }
}

Write-Host "Dry-run completed. Total simulated processed: $processedTotal / $total"

# Confirm apply
$confirm = Read-Host "Apply backfill for real now? Type 'YES' to proceed"
if ($confirm -ne 'YES') {
    Write-Host "Aborting apply. You can re-run this script to apply later."; exit 0
}

# 3) Apply for real by batches
$processedTotal = 0
Write-Host "Applying backfill by batches of $batch..."
while ($processedTotal -lt $total) {
    Write-Host "Apply batch starting. Applied so far: $processedTotal / $total"
    $res = Run-Command "run --project tools/RotateKeys -- backfill apply --db \"$DbPath\" --batch $batch --force"
    Write-Host $res.StdOut
    if ($res.ExitCode -ne 0) { Write-Error "Apply failed: $($res.StdErr)"; exit $res.ExitCode }

    $m = [regex]::Match($res.StdOut, 'Backfill processed=(\d+) created=(\d+) skipped=(\d+) \(dryRun=(True|False)\)', 'IgnoreCase')
    if ($m.Success) {
        $proc = [int]$m.Groups[1].Value
        $created = [int]$m.Groups[2].Value
        $skipped = [int]$m.Groups[3].Value
        $processedTotal += $proc
        Write-Host "Batch applied: processed=$proc created=$created skipped=$skipped (cumulative processed: $processedTotal)"
    }
    else {
        Write-Warning "Could not parse apply output for batch; stopping."
        break
    }
}

Write-Host "Backfill apply completed (or stopped). Cumulative processed: $processedTotal / $total"
Write-Host "Done. Please run the integrity diagnostic again to verify results."
