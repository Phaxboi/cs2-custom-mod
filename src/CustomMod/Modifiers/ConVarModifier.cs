using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;

namespace CustomMod.Modifiers;

/// <summary>
/// A modifier that only changes server ConVars. Values are restored on revert.
/// Values are set directly rather than through a server command, so this also
/// works for cheat-protected ConVars without enabling sv_cheats.
/// </summary>
public abstract class ConVarModifier : Modifier
{
    /// <summary>ConVar name and the value to set while this modifier is active.</summary>
    protected abstract IReadOnlyDictionary<string, string> ConVars { get; }

    private readonly List<(ConVar ConVar, string OriginalValue)> _overridden = new();

    public override void Apply()
    {
        foreach (var (name, value) in ConVars)
        {
            var conVar = ConVar.Find(name);
            if (conVar == null)
                throw new InvalidOperationException($"ConVar '{name}' not found");

            _overridden.Add((conVar, NativeAPI.GetConvarValueAsString(conVar.AccessIndex)));
            NativeAPI.SetConvarValueAsString(conVar.AccessIndex, value);
        }
    }

    public override void Revert()
    {
        foreach (var (conVar, originalValue) in _overridden)
            NativeAPI.SetConvarValueAsString(conVar.AccessIndex, originalValue);
        _overridden.Clear();
    }
}
