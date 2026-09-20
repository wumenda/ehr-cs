# Build-time check: validates the JS embedded in HtmlTemplate.cs (web panel script).
# Prevents regressions like string literals broken across lines that silently kill the panel.
# Exits 0 when node is not installed (check skipped); exits 2 on syntax error (build fails).
param([string]$OutDir = "obj")

# cmd.exe mangles a trailing backslash inside the quoted arg ('\') into a literal quote
$OutDir = $OutDir -replace '["\\]+$', ''

$ErrorActionPreference = 'Stop'
$srcPath = Join-Path $PSScriptRoot 'HtmlTemplate.cs'
# Windows PowerShell 5.1 reads no-BOM files as ANSI; force UTF-8 (the .cs is UTF-8)
$src = Get-Content -Raw -Encoding UTF8 -LiteralPath $srcPath
$m = [regex]::Match($src, '(?s)<script>\s*(.*?)\s*</script>')
if (-not $m.Success) {
    Write-Host 'panel-js-check: FAIL - no <script> block found in HtmlTemplate.cs'
    exit 1
}

$jsFile = Join-Path $OutDir 'panel_check.js'
$dir = Split-Path -Parent $jsFile
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
[IO.File]::WriteAllText($jsFile, $m.Groups[1].Value, [Text.UTF8Encoding]::new($false))

$node = Get-Command node -ErrorAction SilentlyContinue
if (-not $node) {
    Write-Host 'panel-js-check: node not found on PATH, skipped'
    exit 0
}

& node --check $jsFile
if ($LASTEXITCODE -ne 0) {
    Write-Host 'panel-js-check: FAILED - embedded panel JS has syntax errors (web panel would be broken)'
    exit 2
}
Write-Host 'panel-js-check: OK'
