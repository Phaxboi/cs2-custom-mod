# cs2-custom-mod

CounterStrikeSharp plugin for a private CS2 server. Each round (outside warmup) a random
modifier is applied and announced in chat and on screen. It lasts through the round-end delay
and is swapped for the next one when players respawn.
The same modifier is never picked twice in a row.

## Adding a modifier

Add a class in `src/CustomMod/Modifiers/`. Any non-abstract `Modifier` subclass is picked up
automatically.

Modifiers that only change ConVars can derive from `ConVarModifier`. Original values are
restored on revert, and cheat-protected ConVars work without `sv_cheats`:

```csharp
public class LowGravity : ConVarModifier
{
    public override string Name => "Low Gravity";
    public override string Description => "Jump higher, fall slower";

    protected override IReadOnlyDictionary<string, string> ConVars { get; } = new Dictionary<string, string>
    {
        ["sv_gravity"] = "300",
    };
}
```

Optionally override `Color` (hex, center-screen title), `ChatColor` (a `ChatColors` value) and
`IconUrl` (image shown above the title) to style the announcement.

Anything else derives from `Modifier` and implements `Apply()` and `Revert()`. `Revert()` must
undo everything `Apply()` did. `Plugin` gives access to timers, listeners and event handlers.

## Testing

Both commands are open to every player, in chat (`!modifier`) or the console (`css_modifier`):

- `!modifier` lists modifiers and shows the active one
- `!modifier <Id>` applies one now. Id is the class name, case-insensitive; a unique prefix
  works too (`!modifier slip`)
- `!modifier off` reverts the active one
- `!skip` ends the current round as a draw, or ends warmup during warmup
