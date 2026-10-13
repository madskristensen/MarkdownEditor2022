<#
.SYNOPSIS
    Downloads the latest Mermaid.js release from the npm registry and copies
    the required files into src\Margin\.

.DESCRIPTION
    Mermaid.js (https://mermaid.js.org/) is used by Markdown Editor 2022 to
    render diagrams in the preview pane.  This script is the only step needed
    to update the bundled copy of the library – no Node.js or npm installation
    is required.

    The script:
      1. Queries the npm registry REST API for the latest published version of
         the 'mermaid' package.
      2. Downloads three files from the unpkg CDN (https://unpkg.com), which
         mirrors every file that is included in a published npm package:
           • mermaid.min.js          – the minified UMD bundle
           • mermaid.min.js.map      – the corresponding source-map
           • LICENSE                 – saved as mermaid.min.js.LICENSE.txt
      3. Writes the files to  <repo-root>\src\Margin\.

.USAGE
    Run from any directory – the script resolves all paths relative to its own
    location (which is expected to be <repo-root>\scripts\):

        .\scripts\download-latest-mermaidjs.ps1

.PREREQUISITES
    • PowerShell 5.1 or later  (ships with Windows 10 / Windows Server 2016+)
    • Internet access to registry.npmjs.org and unpkg.com

.NOTES
    Tested against Mermaid 11.x.  The file layout on unpkg is stable across
    major versions, but if Mermaid ever restructures its dist/ output you may
    need to adjust the $FilesToDownload map below.
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

$NpmRegistryUrl = 'https://registry.npmjs.org/mermaid/latest'
$UnpkgBase      = 'https://unpkg.com/mermaid@{0}/dist'
$UnpkgLicense   = 'https://unpkg.com/mermaid@{0}/LICENSE'

# Map of  source filename (on unpkg)  →  destination filename (in src\Margin)
$FilesToDownload = [ordered]@{
    'mermaid.min.js'     = 'mermaid.min.js'
    'mermaid.min.js.map' = 'mermaid.min.js.map'
}

# ---------------------------------------------------------------------------
# Resolve paths
# ---------------------------------------------------------------------------

$RepoRoot  = Split-Path -Parent $PSScriptRoot          # scripts\ lives one level below root
$MarginDir = Join-Path $RepoRoot 'src\Margin'

if (-not (Test-Path $MarginDir)) {
    Write-Error "Target directory not found: $MarginDir"
}

# ---------------------------------------------------------------------------
# Determine the version to download
# ---------------------------------------------------------------------------

Write-Host "Querying npm registry for the latest mermaid version..."
try {
    $Response = Invoke-RestMethod -Uri $NpmRegistryUrl -UseBasicParsing
    $Version  = $Response.version
} catch {
    Write-Error "Failed to query npm registry: $_"
}
Write-Host "Latest version: $Version"

# ---------------------------------------------------------------------------
# Download dist files
# ---------------------------------------------------------------------------

$BaseUrl = $UnpkgBase -f $Version

foreach ($Entry in $FilesToDownload.GetEnumerator()) {
    $Url  = "$BaseUrl/$($Entry.Key)"
    $Dest = Join-Path $MarginDir $Entry.Value

    Write-Host "Downloading $($Entry.Key)..."
    try {
        Invoke-WebRequest -Uri $Url -OutFile $Dest -UseBasicParsing
    } catch {
        Write-Error "Failed to download ${Url}: $_"
    }
}

# ---------------------------------------------------------------------------
# Download LICENSE  (stored next to the JS file for attribution purposes)
# ---------------------------------------------------------------------------

$LicenseUrl  = $UnpkgLicense -f $Version
$LicenseDest = Join-Path $MarginDir 'mermaid.min.js.LICENSE.txt'

Write-Host "Downloading LICENSE..."
try {
    Invoke-WebRequest -Uri $LicenseUrl -OutFile $LicenseDest -UseBasicParsing
} catch {
    Write-Warning "Could not download LICENSE file: $_"
}

# ---------------------------------------------------------------------------
# Done
# ---------------------------------------------------------------------------

Write-Host ''
Write-Host "Mermaid $Version successfully downloaded to src\Margin\" -ForegroundColor Green
