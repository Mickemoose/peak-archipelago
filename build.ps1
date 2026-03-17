# Build script for Peak Archipelago mod
Write-Host "Building Peak Archipelago..." -ForegroundColor Green

# Build the .NET project
Write-Host "Building DLL..." -ForegroundColor Yellow
dotnet build peak-archipelago.sln --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "DLL built successfully" -ForegroundColor Green

# Create the .apworld package
Write-Host "Creating .apworld package..." -ForegroundColor Yellow

# Check if peak folder exists
if (-not (Test-Path "peak")) {
    Write-Host "Error: 'peak' folder not found!" -ForegroundColor Red
    exit 1
}

# Remove old .apworld if it exists
if (Test-Path "peak.apworld") {
    Remove-Item "peak.apworld"
}

# Create the zip file with forward slashes for Linux compatibility
Add-Type -Assembly System.IO.Compression.FileSystem
$zipPath = [System.IO.Path]::GetFullPath("peak.apworld")
$peakDir = [System.IO.Path]::GetFullPath("peak")
$zip = [System.IO.Compression.ZipFile]::Open($zipPath, 'Create')
Get-ChildItem -Path "peak" -Recurse -File | ForEach-Object {
    $relativePath = $_.FullName.Substring($peakDir.Length + 1).Replace('\', '/')
    $entryName = "peak/" + $relativePath
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $entryName) | Out-Null
}
$zip.Dispose()

Write-Host "Successfully created peak.apworld" -ForegroundColor Green

# Copy peak folder to Archipelago-Peak/worlds-link directory
Write-Host "Copying peak folder to Archipelago worlds directory..." -ForegroundColor Yellow

$destinationPath = "worlds-link\peak"

# Create worlds-link directory if it doesn't exist
if (-not (Test-Path "worlds-link")) {
    New-Item -Path "worlds-link" -ItemType Directory
}

# Remove old peak folder if it exists
if (Test-Path $destinationPath) {
    Remove-Item -Path $destinationPath -Recurse -Force
}

# Copy the peak folder
Copy-Item -Path "peak" -Destination $destinationPath -Recurse

Write-Host "Successfully copied peak folder to $destinationPath" -ForegroundColor Green
Write-Host "Done!" -ForegroundColor Green