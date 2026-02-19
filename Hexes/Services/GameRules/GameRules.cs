using System;

namespace MechanicalCataphract.Services;

/// <summary>
/// Static accessor for game rules. Used by entity computed properties (Army, Brigade, etc.)
/// that have no DI access. GameRulesService sets Current at construction time.
/// </summary>
public static class GameRules
{
    private static GameRulesData? _current;

    public static GameRulesData Current
    {
        get => _current ?? throw new InvalidOperationException(
            "GameRules.Current has not been set. Ensure IGameRulesService is resolved before accessing entity computed properties.");
        set => _current = value;
    }

    /// <summary>
    /// Override rules for unit testing. Call in [SetUp] to avoid InvalidOperationException.
    /// </summary>
    public static void SetForTesting(GameRulesData rules) => _current = rules;
}
