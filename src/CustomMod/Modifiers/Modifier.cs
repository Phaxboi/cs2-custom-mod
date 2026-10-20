using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace CustomMod.Modifiers;

/// <summary>
/// A per-round modifier. Subclass this (with a parameterless constructor) and the
/// engine picks it up automatically; no registration needed.
/// </summary>
public abstract class Modifier
{
    /// <summary>Identifier used by the css_modifier command. Defaults to the class name.</summary>
    public virtual string Id => GetType().Name;

    /// <summary>Shown to players when the round starts.</summary>
    public abstract string Name { get; }

    /// <summary>One short line shown under the name.</summary>
    public abstract string Description { get; }

    /// <summary>Hex colour of the name in the center-screen announcement.</summary>
    public virtual string Color => "#ffcc00";

    /// <summary>Colour of the name in chat. Chat only supports the ChatColors palette.</summary>
    public virtual char ChatColor => ChatColors.Gold;

    /// <summary>Optional image shown above the name in the center-screen announcement.</summary>
    public virtual string? IconUrl => null;

    /// <summary>The plugin instance, for timers, listeners and event handlers.</summary>
    protected BasePlugin Plugin { get; private set; } = null!;

    internal void Init(BasePlugin plugin) => Plugin = plugin;

    /// <summary>Called at round start when this modifier is picked.</summary>
    public abstract void Apply();

    /// <summary>Called at the next round start (or unload / map change). Must undo everything Apply did.</summary>
    public abstract void Revert();
}
