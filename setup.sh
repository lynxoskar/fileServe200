#!/bin/bash

set -e

echo "Setting up .NET 10 for fileServe200 project..."

# Create local dotnet directory
DOTNET_DIR="$PWD/.dotnet"
SETUP_DIR="$PWD/setup"

# Function to check if .NET 10 is already installed
check_dotnet10() {
    if [ -f "$DOTNET_DIR/dotnet" ]; then
        echo "Checking existing .NET installation..."
        local version=$("$DOTNET_DIR/dotnet" --version 2>/dev/null || echo "")
        if [[ "$version" == 10.* ]]; then
            echo "✅ .NET 10 is already installed (version: $version)"
            return 0
        else
            echo "Found .NET version $version, but need .NET 10"
            return 1
        fi
    else
        echo ".NET not found in local directory"
        return 1
    fi
}

# Check if .NET 10 is already installed
if check_dotnet10; then
    echo "Skipping installation - .NET 10 already available"
else
    echo "Installing .NET 10..."
    
    mkdir -p "$DOTNET_DIR"
    
    # Use the provided installer script
    if [ -f "$SETUP_DIR/dotnet-install.sh" ]; then
        echo "Using provided .NET installer script"
        chmod +x "$SETUP_DIR/dotnet-install.sh"
        
        # Install .NET 10 SDK and runtimes locally
        echo "Installing .NET 10 SDK to $DOTNET_DIR..."
        "$SETUP_DIR/dotnet-install.sh" --version latest --channel 10.0 --install-dir "$DOTNET_DIR" --runtime dotnet
        "$SETUP_DIR/dotnet-install.sh" --version latest --channel 10.0 --install-dir "$DOTNET_DIR" --runtime aspnetcore
    else
        echo "Error: dotnet-install.sh not found in setup directory"
        exit 1
    fi
fi

# Create environment setup script
cat > set-dotnet-env.sh << 'EOF'
#!/bin/bash
export DOTNET_ROOT="$(pwd)/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
echo "Local .NET environment configured"
echo "DOTNET_ROOT: $DOTNET_ROOT"
echo "Run 'source set-dotnet-env.sh' in new shells to use local .NET"
EOF

chmod +x set-dotnet-env.sh

# Set environment for current session
export DOTNET_ROOT="$DOTNET_DIR"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

# Verify installation
echo "Verifying .NET installation..."
"$DOTNET_DIR/dotnet" --version
"$DOTNET_DIR/dotnet" --list-sdks
"$DOTNET_DIR/dotnet" --list-runtimes

echo ""
echo "✅ .NET 10 setup complete!"
echo ""
echo "To use .NET in new terminal sessions, run:"
echo "  source set-dotnet-env.sh"
echo ""
echo "To create the project as specified in systemspec.md, run:"
echo "  source set-dotnet-env.sh"
echo "  dotnet new web -o FileServer"
echo "  cd FileServer"