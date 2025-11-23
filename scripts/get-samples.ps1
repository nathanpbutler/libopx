#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Downloads sample files from libopx GitHub releases.
.DESCRIPTION
    Fetches input.* test files (bin, mxf, t42, ts, vbi, vbid) from the specified
    GitHub release and saves them to ../samples relative to this script.
#>

[CmdletBinding()]
param(
    [string]$Version = "v1.0.0"
)

$ErrorActionPreference = "Stop"

# Determine sample directory relative to script location
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$samplesDir = Join-Path $scriptDir ".." "samples" | Resolve-Path

# Create samples directory if it doesn't exist
if (-not (Test-Path $samplesDir)) {
    New-Item -ItemType Directory -Path $samplesDir | Out-Null
    Write-Verbose "Created directory: $samplesDir"
}

# Define files to download
$baseUrl = "https://github.com/nathanpbutler/libopx/releases/download/$Version"
$files = @(
    "input.bin",
    "input.mxf",
    "input.t42",
    "input.ts",
    "input.vbi",
    "input.vbid"
)

Write-Host "Downloading sample files from release $Version..." -ForegroundColor Cyan

foreach ($file in $files) {
    $url = "$baseUrl/$file"
    $outputPath = Join-Path $samplesDir $file
    
    try {
        Write-Host "  Downloading $file..." -NoNewline
        Invoke-WebRequest -Uri $url -OutFile $outputPath -UseBasicParsing
        Write-Host " OK" -ForegroundColor Green
    }
    catch {
        Write-Host " FAILED" -ForegroundColor Red
        Write-Warning "Failed to download $file : $_"
    }
}

Write-Host "`nDownload complete. Files saved to: $samplesDir" -ForegroundColor Green