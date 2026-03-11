# Build script for Peak Archipelago mod
echo "Building Peak Archipelago..."

# Build the .NET project
echo "Building DLL..."
dotnet build peak-archipelago.sln --configuration Release

if [ $? -ne 0 ]; then
    echo "Build failed!"
    exit 1
fi

echo "DLL built successfully"

# Create the .apworld package
echo "Creating .apworld package..."

# Check if peak folder exists
if [ ! -d "peak" ]; then
    echo "Error: 'peak' folder not found!"
    exit 1
fi

# Remove old .apworld if it exists
if [ -f "peak.apworld" ]; then
    rm "peak.apworld"
fi

# Create the zip file
zip -r peak.zip peak

# Rename .zip to .apworld
mv peak.zip peak.apworld

echo "Successfully created peak.apworld"

# Copy peak folder to Archipelago-Peak/worlds-link directory
echo "Copying peak folder to Archipelago worlds directory..."

destinationPath="worlds-link/peak"

# Create worlds-link directory if it doesn't exist
if [ ! -d "worlds-link" ]; then
    mkdir -p "worlds-link"
fi

# Remove old peak folder if it exists
if [ -d "$destinationPath" ]; then
    rm -rf "$destinationPath"
fi

# Copy the peak folder
cp -r peak "$destinationPath"

echo "Successfully copied peak folder to $destinationPath"
echo "Done!"
