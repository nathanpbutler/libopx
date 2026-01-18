#!/bin/bash
# install_dotnet.sh - SessionStart hook for Claude Code on the Web
# Installs .NET SDK versions 8.0, 9.0, and 10.0

set -e

# Only run in remote (Claude Code on the Web) environments
if [ "$CLAUDE_CODE_REMOTE" != "true" ]; then
    echo "Skipping .NET install - not in remote environment"
    exit 0
fi

echo "=== Installing .NET SDKs ==="

# Define .NET installation directory
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"

# Create the directory
mkdir -p "$DOTNET_ROOT"

# Download and run the official .NET install script
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh

# Install .NET 8.0 LTS
echo "Installing .NET 8.0 LTS..."
/tmp/dotnet-install.sh --channel 8.0 --install-dir "$DOTNET_ROOT"

# Install .NET 9.0 STS
echo "Installing .NET 9.0 STS..."
/tmp/dotnet-install.sh --channel 9.0 --install-dir "$DOTNET_ROOT"

# Install .NET 10.0 (Preview/Latest)
echo "Installing .NET 10.0..."
/tmp/dotnet-install.sh --channel 10.0 --install-dir "$DOTNET_ROOT"

# Clean up
rm -f /tmp/dotnet-install.sh

# Verify installations
echo ""
echo "=== Installed .NET SDKs ==="
"$DOTNET_ROOT/dotnet" --list-sdks

echo ""
echo "=== Default .NET version ==="
"$DOTNET_ROOT/dotnet" --version

# Persist environment variables for the session
if [ -n "$CLAUDE_ENV_FILE" ]; then
    echo "Persisting environment variables..."
    cat >> "$CLAUDE_ENV_FILE" << 'EOF'
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER=0
export DOTNET_EnableWriteXorExecute=0
export DOTNET_DEFAULT_VERSION=10.0
EOF
fi

echo ""
echo "=== .NET installation complete ==="
exit 0