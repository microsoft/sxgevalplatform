# Script to sanitize requirements.txt for Docker builds
# This removes any BOM, normalizes line endings, and ensures pure ASCII

$requirementsPath = Join-Path $PSScriptRoot "requirements.txt"

Write-Host "Sanitizing requirements.txt for Docker build..."

# Read the file content
$content = Get-Content -Path $requirementsPath -Raw

# Remove BOM if present
$content = $content -replace "^\xEF\xBB\xBF", ""

# Normalize line endings to LF (Unix style)
$content = $content -replace "`r`n", "`n"

# Remove trailing whitespace
$content = $content -replace " +`n", "`n"

# Remove empty lines at the end
$content = $content.TrimEnd()

# Ensure file ends with a single newline
$content = $content + "`n"

# Write back with UTF-8 without BOM
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($requirementsPath, $content, $utf8NoBom)

Write-Host "? requirements.txt sanitized successfully"
Write-Host "  - Removed BOM (if present)"
Write-Host "  - Normalized line endings to LF"
Write-Host "  - Removed trailing whitespace"
Write-Host "  - Saved as UTF-8 without BOM"
