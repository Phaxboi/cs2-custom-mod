# CS2 Custom Mod

CounterStrikeSharp (C#, .NET 8) plugin for a private CS2 dedicated server. Must work on any map, including Workshop maps.

## Layout

- Repo: `~/servers/cs2/cs2-custom-mod/` (plugin source in `src/<PluginName>/`)
- Server: `~/servers/cs2/server/` (managed by SteamCMD, do not edit)
- Plugins dir: `~/servers/cs2/server/game/csgo/addons/counterstrikesharp/plugins/`

## Commands

- Build + deploy: `./scripts/deploy.sh` (hot-reloads the plugin, no restart needed)
- Server console: `cs2-rcon "<command>"` (e.g. `cs2-rcon "css_plugins list"`)
- Restart server: `cs2-restart` (only needed if hot reload fails)
- Server log: `journalctl --user -u cs2 --since "-5min" --no-pager`

## Rules

- Only work inside `~/servers/cs2/`. Never touch other game servers in `~/servers/`.
- Never push to `main`. Work on a branch, push it, open a PR with `gh pr create`.
- Ask before adding NuGet packages, third-party plugins or downloaded scripts.
- Never commit secrets (`~/servers/cs2/server.env` stays outside the repo).
- Reference `CounterStrikeSharp.API` with `ExcludeAssets="runtime"` so its DLL is not deployed.
- After deploying, check the log to confirm the plugin loaded without errors.
