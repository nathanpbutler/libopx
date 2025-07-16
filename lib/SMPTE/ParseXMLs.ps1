function Convert-UrnToHex {
    param (
        [string]$urn
    )

    # Remove the 'urn:smpte:ul:' prefix
    $urn = $urn -replace '^urn:smpte:ul:', ''
    $urn = $urn -replace '\.', ''

    $hexValues = @()

    # Loop through the string two characters at a time
    for ($i = 0; $i -lt $urn.Length; $i+=2) {
        # Get the next two characters
        $hexPair = $urn.Substring($i, 2)

        # Convert to an integer and then to a hex string
        $hexValue = [Convert]::ToInt32($hexPair, 16)

        # Add the hex value to the array
        $hexValues += "0x{0:X2}" -f $hexValue
    }

    # Join the hex values into a comma-separated string
    $result = $hexValues -join ', '

    return $result
}

Get-ChildItem -Path ./*.xml | ForEach-Object {
    $name = $_.BaseName
    Write-Host "Processing $name.xml"
    [xml]$content = Get-Content $_.FullName -Raw
    $sb = New-Object System.Text.StringBuilder
    $sb.AppendLine("namespace nathanbutlerDEV.libopx.SMPTE;") | Out-Null
    $sb.AppendLine("public class $name") | Out-Null
    $sb.AppendLine("{") | Out-Null
    $entries = $content.SelectNodes("//*[local-name()='Entries']/*[local-name()='Entry']")
    
    # Group entries by symbol to handle duplicates
    $symbolGroups = $entries | Group-Object -Property Symbol
    
    foreach ($group in $symbolGroups) {
        $symbol = $group.Name
        if ($symbol -eq "event") {
            $symbol = "_event"
        }
        # Handle case where symbol matches the class name
        if ($symbol -eq $name) {
            $symbol = "_$symbol"
        }
        
        # If there's only one entry with this symbol, generate as normal
        if ($group.Count -eq 1) {
            $entry = $group.Group[0]
            $hex = Convert-UrnToHex -urn $entry.UL
            $definition = $entry.Definition
            if ($definition -eq $null) {
                $definition = "No definition provided."
            }
            # Fix for newlines in definition
            $definition = $definition -replace "`r`n", "`n" -replace "`n", ""
            $sb.AppendLine("    /// <summary>`n    /// $($entry.Name)`n    /// </summary>`n    /// <remarks>`n    /// $($definition.Trim())`n    /// </remarks>`n    private static readonly byte[] $($symbol) = [$($hex)];") | Out-Null
        }
        else {
            # Multiple entries with same symbol - create unique names
            $index = 1
            foreach ($entry in $group.Group) {
                $hex = Convert-UrnToHex -urn $entry.UL
                $definition = $entry.Definition
                if ($definition -eq $null) {
                    $definition = "No definition provided."
                }
                # Fix for newlines in definition
                $definition = $definition -replace "`r`n", "`n" -replace "`n", ""
                
                # Create a unique symbol name using namespace or index
                $uniqueSymbol = $symbol
                
                # Try to extract a distinguishing part from the namespace
                if ($entry.NamespaceName) {
                    $nsParts = $entry.NamespaceName -split '/'
                    $distinguisher = $nsParts[-1]
                    if ($distinguisher -and $distinguisher -ne "" -and $distinguisher -ne "2012") {
                        $uniqueSymbol = "${symbol}_${distinguisher}"
                    }
                    else {
                        $uniqueSymbol = "${symbol}_$index"
                    }
                }
                else {
                    $uniqueSymbol = "${symbol}_$index"
                }
                
                # Clean up the symbol name
                $uniqueSymbol = $uniqueSymbol -replace '[^a-zA-Z0-9_]', '_'
                
                $sb.AppendLine("    /// <summary>`n    /// $($entry.Name)`n    /// </summary>`n    /// <remarks>`n    /// $($definition.Trim())`n    /// Namespace: $($entry.NamespaceName)`n    /// </remarks>`n    private static readonly byte[] $($uniqueSymbol) = [$($hex)];") | Out-Null
                $index++
            }
        }
    }
    
    $sb.AppendLine("}") | Out-Null
    $sb.ToString() | Set-Content -Path "./$name.cs" -Encoding utf8
}