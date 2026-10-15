#!/usr/bin/env pwsh
# Mounts src/Godot/EternalWars.Godot.Assets into every Godot app as res://shared/.
# Windows: directory junction (no admin rights needed). Linux/macOS: symlink. Safe to re-run.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root 'src/Godot/EternalWars.Godot.Assets'

Get-ChildItem (Join-Path $root 'src/Apps') -Directory |
    Where-Object { Test-Path (Join-Path $_.FullName 'project.godot') } |
    ForEach-Object {
        $link = Join-Path $_.FullName 'shared'
        if (Test-Path $link) {
            Write-Host "ok      $link"
            return
        }

        if ($IsWindows -or $env:OS -eq 'Windows_NT') {
            New-Item -ItemType Junction -Path $link -Target $assets | Out-Null
        }
        else {
            New-Item -ItemType SymbolicLink -Path $link -Target $assets | Out-Null
        }

        Write-Host "linked  $link -> $assets"
    }
