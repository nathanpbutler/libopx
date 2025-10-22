<#
.SYNOPSIS
    Strips the high bit from bytes in a binary file by subtracting 128 from bytes >= 128.

.DESCRIPTION
    This script processes binary files by clearing the high bit (bit 7) of each byte.
    Any byte with a value of 128 or greater has 128 subtracted from it, effectively
    stripping the high bit. Bytes with values less than 128 remain unchanged.

    The script uses streaming I/O with a 64KB buffer to efficiently process files of
    any size without loading the entire file into memory.

    By default, only .bin, .raw, .t42, and .rcwt files are accepted. Use the -Force
    parameter to process files with other extensions.

.PARAMETER Path
    The path to the input binary file to process. This parameter is mandatory.

.PARAMETER OutPath
    The path for the output file. If not specified, the output file will be created
    in the same directory as the input file with "_stripped" appended to the filename
    (e.g., "data.bin" becomes "data_stripped.bin").

.PARAMETER Force
    Bypasses the file extension validation, allowing the script to process files with
    extensions other than .bin, .raw, .t42, or .rcwt.

.EXAMPLE
    .\strip-bytes.ps1 data.bin

    Processes data.bin and creates data_stripped.bin in the same directory.

.EXAMPLE
    .\strip-bytes.ps1 input.raw -OutPath output.raw

    Processes input.raw and saves the result to output.raw.

.EXAMPLE
    .\strip-bytes.ps1 recording.t42

    Processes a .t42 file (teletext data) and creates recording_stripped.t42.

.EXAMPLE
    .\strip-bytes.ps1 data.dat -Force

    Processes data.dat by forcing the script to accept a non-standard extension.

.NOTES
    File Name      : strip-bytes.ps1
    Prerequisite   : PowerShell 5.1 or later

    The script modifies bytes in-place within the buffer for optimal performance,
    processing 64KB chunks at a time to minimize memory usage.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateScript({
        if (-not (Test-Path $_)) {
            throw "File not found: $_"
        }
        $true
    })]
    [string]$Path,

    [Parameter(Mandatory = $false)]
    [string]$OutPath,

    [Parameter(Mandatory = $false)]
    [switch]$Force
)

# Resolve the full path
$inputFile = Resolve-Path $Path

# Validate file extension unless -Force is specified
if (-not $Force) {
    $extension = [System.IO.Path]::GetExtension($inputFile).ToLower()
    if ($extension -notin @('.bin', '.raw', '.t42', '.rcwt')) {
        Write-Error "File must have .bin, .raw, .t42, or .rcwt extension. Use -Force to override this check."
        exit 1
    }
}

# Determine output path
if (-not $OutPath) {
    $directory = [System.IO.Path]::GetDirectoryName($inputFile)
    $filename = [System.IO.Path]::GetFileNameWithoutExtension($inputFile)
    $extension = [System.IO.Path]::GetExtension($inputFile)
    $OutPath = Join-Path $directory "$filename`_stripped$extension"
}

Write-Host "Processing file: $inputFile"

# Buffer size for streaming (64KB)
$bufferSize = 65536
$totalBytes = 0
$modifiedCount = 0

try {
    # Open input and output streams
    $inputStream = [System.IO.File]::OpenRead($inputFile)
    $outputStream = [System.IO.File]::Create($OutPath)

    $buffer = New-Object byte[] $bufferSize

    # Process file in chunks
    while (($bytesRead = $inputStream.Read($buffer, 0, $bufferSize)) -gt 0) {
        # Strip bytes >= 128 in the buffer
        for ($i = 0; $i -lt $bytesRead; $i++) {
            if ($buffer[$i] -ge 128) {
                $buffer[$i] = [byte]($buffer[$i] - 128)
                $modifiedCount++
            }
        }

        # Write processed buffer to output
        $outputStream.Write($buffer, 0, $bytesRead)
        $totalBytes += $bytesRead
    }

    Write-Host "Processed $totalBytes bytes"
    Write-Host "Modified $modifiedCount bytes (subtracted 128 from each)"
    Write-Host "Output written to: $OutPath"
}
finally {
    # Ensure streams are closed
    if ($null -ne $inputStream) { $inputStream.Close() }
    if ($null -ne $outputStream) { $outputStream.Close() }
}
