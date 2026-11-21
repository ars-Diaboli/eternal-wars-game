#!/usr/bin/env pwsh
# The map editor's timing (ADR-278): a 256 x 256 two-level map generated from the shipped eight-player template, then opened in the editor,
# painted with the Terrain tool and validated, against the budgets of the editor plan (open under 3 s, paint at 60 fps, no frame over 50 ms
# while the map is validated). Fails when a budget is missed.
#
#   ./tools/perf-editor.ps1 -Godot "C:\path\Godot_v4.7.2-stable_mono_win64_console.exe"
#
# Runs in real time in a window (not under Movie Maker) with vsync off, so it needs a GPU and a display, and the frame times are this
# computer's. The map and the report go to .perf/; -Runs repeats the timing and keeps every report.

param(
    [string]$Godot = $env:GODOT,
    [string]$Out = (Join-Path (Split-Path -Parent $PSScriptRoot) '.perf'),
    # Skip the build (the solution is built already).
    [switch]$NoBuild,
    # Time this map instead of the generated one.
    [string]$Map,
    # How many times the editor is timed (the first run also pays for Godot's shader cache).
    [int]$Runs = 1,
    [int]$Size = 256,
    [ulong]$Seed = 1,
    # How long one run may take before the editor is stopped and the run fails.
    [int]$TimeoutSeconds = 300
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Godot -or -not (Test-Path $Godot)) {
    throw 'Pass -Godot <path to the Godot .NET console executable> or set $env:GODOT.'
}

& (Join-Path $PSScriptRoot 'link-shared-assets.ps1') | Out-Null
if (-not $NoBuild) {
    dotnet build (Join-Path $root 'EternalWars.sln') --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}

New-Item -ItemType Directory -Force $Out | Out-Null
$Out = (Resolve-Path $Out).Path
if (-not $Map) {
    $Map = Join-Path $Out "generated-$Size-seed$Seed.ewmap.json"
    if (-not (Test-Path $Map)) {
        # ffa-8 is the shipped template with an underground: two levels.
        dotnet run --project (Join-Path $PSScriptRoot 'EternalWars.Tools.Sim') --no-build -- generate ffa-8 --size $Size --seed $Seed --out $Map
        if ($LASTEXITCODE -ne 0) { throw 'Generating the map failed.' }
    }
}

$failed = 0
for ($run = 1; $run -le $Runs; $run++) {
    $dir = Join-Path $Out "run$run"
    Remove-Item $dir -Recurse -ErrorAction SilentlyContinue # the default settings and layout, no autosaves to offer
    New-Item -ItemType Directory -Force $dir | Out-Null
    $report = Join-Path $dir 'report.txt'
    # Started as a process of its own (its arguments quoted one by one), so a run that hangs is stopped after the timeout.
    $start = [Diagnostics.ProcessStartInfo]::new($Godot)
    foreach ($argument in @('--path', (Join-Path $root 'src/Apps/EternalWars.MapEditor'), '--disable-vsync', '--',
            "--settings=$(Join-Path $dir 'settings.json')", "--map=$Map", "--perf=$report")) { $start.ArgumentList.Add($argument) }
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        $process.Kill($true)
        $process.WaitForExit()
        Set-Content (Join-Path $dir 'godot.log') ($stdout.Result + $stderr.Result)
        throw "Run $run took longer than $TimeoutSeconds s and was stopped; see $dir/godot.log."
    }

    $log = $stdout.Result + $stderr.Result
    $exit = $process.ExitCode
    Set-Content (Join-Path $dir 'godot.log') $log
    Write-Host "run $run"
    if (-not (Test-Path $report)) {
        Write-Host $log
        throw "The editor wrote no report (exit $exit); see $dir/godot.log."
    }

    Get-Content $report | ForEach-Object { Write-Host "  $_" }
    $errors = @($log -split "`n" | Where-Object { $_ -match 'ERROR|SCRIPT ERROR|Unhandled exception|Exception:' })
    $errors | ForEach-Object { Write-Host "  $_" }
    if ($exit -ne 0 -or $errors) { $failed++ }
}

if ($failed) { throw "$failed of $Runs run(s) missed a budget or reported an error." }
