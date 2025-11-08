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

# Create the zip file
Compress-Archive -Path "peak" -DestinationPath "peak.zip"

# Rename .zip to .apworld
Rename-Item -Path "peak.zip" -NewName "peak.apworld"

Write-Host "Successfully created peak.apworld" -ForegroundColor Green

# Copy peak folder to Archipelago-Peak/worlds directory
Write-Host "Copying peak folder to Archipelago worlds directory..." -ForegroundColor Yellow

$destinationPath = "F:\Game Dev Stuff\Archipelago-Peak\worlds\peak"

# Create worlds directory if it doesn't exist
if (-not (Test-Path "F:\Game Dev Stuff\Archipelago-Peak\worlds")) {
    New-Item -Path "F:\Game Dev Stuff\Archipelago-Peak\worlds" -ItemType Directory
}


$sourcePkmns = "pkmns"
$pkmnsPath = "F:\Game Dev Stuff\peak-archipelago\peakpelago\plugins\PeakArchipelagoPluginDLL\pkmns"

if (Test-Path $sourcePkmns) {
    # Only delete and copy if source exists
    if (Test-Path $pkmnsPath) {
        Remove-Item -Path $pkmnsPath -Recurse -Force
    }
    Copy-Item -Path $sourcePkmns -Destination "F:\Game Dev Stuff\peak-archipelago\peakpelago\plugins\PeakArchipelagoPluginDLL" -Recurse
    Write-Host "Copied pkmns folder" -ForegroundColor Green
} else {
    Write-Host "Warning: pkmns folder not found, skipping..." -ForegroundColor Yellow
}


# Remove old peak folder if it exists
if (Test-Path $destinationPath) {
    Remove-Item -Path $destinationPath -Recurse -Force
}

# Copy the peak folder
Copy-Item -Path "peak" -Destination $destinationPath -Recurse

Write-Host "Successfully copied peak folder to $destinationPath" -ForegroundColor Green
Write-Host "Done!" -ForegroundColor Green