# This script checks for unused NuGet packages in a .NET solution that uses centralized package management with Directory.Packages.props.
# see: https://developercommunity.visualstudio.com/t/Add-a-remove-unused-packages-command-f/10939558?sort=active

# Path to the central package management file
$centralPropsPath = "Directory.Packages.props"

# Get all package IDs from Directory.Packages.props
[xml]$centralProps = Get-Content $centralPropsPath
$centralPackages = $centralProps.Project.ItemGroup.PackageVersion | ForEach-Object { $_.Include }

# Get all .csproj and .wixproj files in the solution
$projFiles = Get-ChildItem -Recurse -Include *.csproj,*.wixproj

# Extract all used PackageReferences from all projects
$usedPackages = @()
foreach ($file in $projFiles) {
    [xml]$proj = Get-Content $file.FullName
    $proj.Project.ItemGroup.PackageReference | ForEach-Object {
        if ($_.Include) {
            $usedPackages += $_.Include
        }
    }
}

# Remove duplicates
$usedPackages = $usedPackages | Sort-Object -Unique

# Compare and find unused packages
$unusedPackages = $centralPackages | Where-Object { $_ -notin $usedPackages }

# Output results
Write-Host "Unused packages in Directory.Packages.props:`n"
$unusedPackages | ForEach-Object { Write-Host "- $_" }