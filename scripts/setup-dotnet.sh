#!/bin/bash
# Only run in remote (web) environments
if [ "$CLAUDE_CODE_REMOTE" != "true" ]; then
    exit 0
fi

# Install .NET 8.0, 9.0, and 10.0 side-by-side
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh

/tmp/dotnet-install.sh --channel 8.0 --install-dir ~/.dotnet
/tmp/dotnet-install.sh --channel 9.0 --install-dir ~/.dotnet --skip-non-versioned-files
/tmp/dotnet-install.sh --channel 10.0 --install-dir ~/.dotnet --skip-non-versioned-files

# Persist environment for all subsequent Bash commands
if [ -n "$CLAUDE_ENV_FILE" ]; then
    echo 'export DOTNET_ROOT=$HOME/.dotnet' >> "$CLAUDE_ENV_FILE"
    echo 'export PATH=$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH' >> "$CLAUDE_ENV_FILE"
    echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1' >> "$CLAUDE_ENV_FILE"
    echo 'export DOTNET_NOLOGO=true' >> "$CLAUDE_ENV_FILE"
fi

exit 0