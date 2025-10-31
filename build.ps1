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
Write-Host "Done!" -ForegroundColor Green