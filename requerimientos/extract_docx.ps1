# Copy the file first to avoid lock, then extract
$docxPath = "c:\Users\juanm\Documentos\Documents\PROGRAMMING\InfraScan\requerimientos\Informe_Servidores.docx"
$copyPath = "c:\Users\juanm\Documentos\Documents\PROGRAMMING\InfraScan\requerimientos\Informe_copy.docx"
$tempDir = "c:\Users\juanm\Documentos\Documents\PROGRAMMING\InfraScan\requerimientos\docx_extracted"

# Copy file to unlock
Copy-Item $docxPath $copyPath -Force

# Clean up and extract
if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($copyPath, $tempDir)

# Read document.xml with raw regex parsing instead of namespace management
$xml = Get-Content "$tempDir\word\document.xml" -Raw -Encoding UTF8

# Extract all text from paragraphs using regex
Write-Host "=== FULL TEXT CONTENT ==="
$matches2 = [regex]::Matches($xml, '<w:t[^>]*>(.*?)</w:t>')
$allText = ""
$currentPara = ""
$prevEnd = 0

# Simple approach: read the raw XML for paragraphs
$paraMatches = [regex]::Matches($xml, '<w:p[\s>].*?</w:p>', [System.Text.RegularExpressions.RegexOptions]::Singleline)
$pi = 0
foreach ($pm in $paraMatches) {
    $texts = [regex]::Matches($pm.Value, '<w:t[^>]*>(.*?)</w:t>')
    $paraText = ($texts | ForEach-Object { $_.Groups[1].Value }) -join ""
    
    # Check for style
    $styleMatch = [regex]::Match($pm.Value, '<w:pStyle\s+w:val="([^"]+)"')
    $style = if ($styleMatch.Success) { $styleMatch.Groups[1].Value } else { "Normal" }
    
    if ($paraText.Trim()) {
        Write-Host "[$pi] $style -> $paraText"
    }
    $pi++
}

# Extract tables
Write-Host "`n=== TABLES ==="
$tableMatches = [regex]::Matches($xml, '<w:tbl>.*?</w:tbl>', [System.Text.RegularExpressions.RegexOptions]::Singleline)
$ti = 0
foreach ($tm in $tableMatches) {
    $rowMatches = [regex]::Matches($tm.Value, '<w:tr[\s>].*?</w:tr>', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    Write-Host "`n--- Table $ti ($($rowMatches.Count) rows) ---"
    $ri = 0
    foreach ($rm in $rowMatches) {
        $cellMatches = [regex]::Matches($rm.Value, '<w:tc[\s>].*?</w:tc>', [System.Text.RegularExpressions.RegexOptions]::Singleline)
        $cellTexts = @()
        foreach ($cm in $cellMatches) {
            $texts = [regex]::Matches($cm.Value, '<w:t[^>]*>(.*?)</w:t>')
            $cellText = ($texts | ForEach-Object { $_.Groups[1].Value }) -join " "
            $cellTexts += $cellText
        }
        $joined = $cellTexts -join ' || '
        Write-Host "  Row ${ri}: $joined"
        $ri++
    }
    $ti++
}

# List images
Write-Host "`n=== IMAGES ==="
Get-ChildItem "$tempDir\word\media" -ErrorAction SilentlyContinue | ForEach-Object { 
    Write-Host "$($_.Name) - $([math]::Round($_.Length/1024, 1)) KB"
}

# Image relationships
Write-Host "`n=== IMAGE RELATIONSHIPS ==="
if (Test-Path "$tempDir\word\_rels\document.xml.rels") {
    $relsContent = Get-Content "$tempDir\word\_rels\document.xml.rels" -Raw
    $relMatches = [regex]::Matches($relsContent, 'Id="([^"]+)"[^>]*Target="([^"]+)"[^>]*Type="[^"]*image[^"]*"')
    if ($relMatches.Count -eq 0) {
        $relMatches = [regex]::Matches($relsContent, 'Type="[^"]*image[^"]*"[^>]*Target="([^"]+)"[^>]*Id="([^"]+)"')
    }
    foreach ($rm in $relMatches) {
        Write-Host "Id=$($rm.Groups[1].Value) Target=$($rm.Groups[2].Value)"
    }
    # Also try a more flexible approach
    $allRels = [regex]::Matches($relsContent, '<Relationship[^>]+/>')
    foreach ($r in $allRels) {
        if ($r.Value -match 'image') {
            Write-Host $r.Value
        }
    }
}

# Clean up
Remove-Item $copyPath -Force -ErrorAction SilentlyContinue
