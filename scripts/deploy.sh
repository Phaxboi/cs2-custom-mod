#!/bin/bash
# Build the plugin and deploy it to the CS2 server.
# CounterStrikeSharp hot-reloads the plugin when its DLL changes.
#
# Usage: ./scripts/deploy.sh [PluginName]   (default: CustomMod)
set -euo pipefail

PLUGIN_NAME="${1:-CustomMod}"
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$REPO_ROOT/src/$PLUGIN_NAME/$PLUGIN_NAME.csproj"
BUILD_DIR="$REPO_ROOT/build/$PLUGIN_NAME"
PLUGINS_DIR="$HOME/servers/cs2/server/game/csgo/addons/counterstrikesharp/plugins"
TARGET_DIR="$PLUGINS_DIR/$PLUGIN_NAME"

if [ ! -f "$PROJECT" ]; then
  echo "Project not found: $PROJECT" >&2
  exit 1
fi

echo "==> Building $PLUGIN_NAME"
rm -rf "$BUILD_DIR"
# .NET 10 creates its lock dir via mkdtemp in $XDG_RUNTIME_DIR, which is read-only
# in the Claude Code sandbox. Redirect it for the build only; systemd tools still
# need the real XDG_RUNTIME_DIR.
XDG_RUNTIME_DIR="${TMPDIR:-/tmp}" dotnet build "$PROJECT" -c Release -o "$BUILD_DIR" --nologo -v quiet

echo "==> Deploying to $TARGET_DIR"
mkdir -p "$TARGET_DIR"
# Copy everything except the CounterStrikeSharp API (the server provides it)
find "$BUILD_DIR" -maxdepth 1 -type f \
  ! -name 'CounterStrikeSharp.API.dll' \
  ! -name '*.pdb' \
  -exec cp {} "$TARGET_DIR/" \;

echo "==> Waiting for hot reload"
sleep 5
journalctl --user -u cs2 --since "-30s" --no-pager | grep -i -E "$PLUGIN_NAME|error|exception" || \
  echo "(no matching log lines - check: cs2-rcon \"css_plugins list\")"
