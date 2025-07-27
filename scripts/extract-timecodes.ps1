# Get all .raw files in the current directory
$rawFiles = Get-ChildItem -Filter "*_s.raw"

if ($rawFiles.Count -eq 0) {
    Write-Host "No .raw files found in the current directory."
    exit
}

# Same as above but make a hashtable
$csvHashtable = @{}
foreach ($rawFile in $rawFiles) {
    $timecodes = @()
    $reader = [System.IO.BinaryReader]::new([System.IO.File]::OpenRead($rawFile.FullName))
    do {
        # Seek 41 bytes forward to skip the header
        $reader.BaseStream.Position += 41
        $bytes = $reader.ReadBytes(4)
        # Convert the byte array to a timecode string
        $tcString = "0x{0:x2} 0x{1:x2} 0x{2:x2} 0x{3:x2}" -f $bytes[0], $bytes[1], $bytes[2], $bytes[3]
        $timecodes += $tcString
        $reader.BaseStream.Position += 12 # Skip to the next line
    } until ($reader.BaseStream.Position -eq $reader.BaseStream.Length)
    $reader.Close()
    # Add the timecodes to the hashtable
    $csvHashtable[$rawFile.Name] = $timecodes
}

# Output the CSV content to a file as
# file1,file2,file3...
# timecode1,timecode1,timecode1
# timecode2,timecode2,timecode2
# ...

# Get all unique file names (column headers)
$fileNames = $csvHashtable.Keys | Sort-Object

# Create the header row
$csvContent = @()
$csvContent += ($fileNames -join ",")

# Find the maximum number of timecodes across all files
$maxTimecodes = ($csvHashtable.Values | ForEach-Object { $_.Count } | Measure-Object -Maximum).Maximum

# Create data rows
for ($i = 0; $i -lt $maxTimecodes; $i++) {
    $row = @()
    foreach ($fileName in $fileNames) {
        if ($i -lt $csvHashtable[$fileName].Count) {
            $row += $csvHashtable[$fileName][$i]
        } else {
            $row += ""  # Empty cell if this file has fewer timecodes
        }
    }
    $csvContent += ($row -join ",")
}

# Write to CSV file
$csvContent | Out-File -FilePath "timecodes.csv" -Encoding UTF8
Write-Host "Timecodes extracted to timecodes.csv"
