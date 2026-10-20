using System.Reflection;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;

namespace CustomMod.Modifiers;

/// <summary>Discovers modifiers, picks one per round and tracks the active one.</summary>
public class ModifierEngine
{
    private readonly ILogger _logger;
    private Modifier? _lastPicked;

    public IReadOnlyList<Modifier> Modifiers { get; }
    public Modifier? Active { get; private set; }

    public ModifierEngine(BasePlugin plugin, ILogger logger)
    {
        _logger = logger;
        Modifiers = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Modifier)) && !t.IsAbstract)
            .Select(t => (Modifier)Activator.CreateInstance(t)!)
            .OrderBy(m => m.Id)
            .ToList();

        foreach (var modifier in Modifiers)
            modifier.Init(plugin);

        _logger.LogInformation("Loaded {Count} modifiers: {Ids}", Modifiers.Count, string.Join(", ", Modifiers.Select(m => m.Id)));
    }

    public Modifier? Find(string id) =>
        Modifiers.FirstOrDefault(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Random modifier, never the same one twice in a row (when there is more than one).</summary>
    public Modifier? PickRandom()
    {
        var candidates = Modifiers.Where(m => m != _lastPicked || Modifiers.Count == 1).ToList();
        return candidates.Count == 0 ? null : candidates[Random.Shared.Next(candidates.Count)];
    }

    public void Activate(Modifier modifier)
    {
        Deactivate();
        try
        {
            modifier.Apply();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to apply modifier {Id}", modifier.Id);
            TryRevert(modifier);
            return;
        }

        Active = modifier;
        _lastPicked = modifier;
        _logger.LogInformation("Modifier active: {Id}", modifier.Id);
    }

    public void Deactivate()
    {
        if (Active == null)
            return;

        var modifier = Active;
        Active = null;
        TryRevert(modifier);
        _logger.LogInformation("Modifier reverted: {Id}", modifier.Id);
    }

    private void TryRevert(Modifier modifier)
    {
        try
        {
            modifier.Revert();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to revert modifier {Id}", modifier.Id);
        }
    }
}
