namespace GUI.ViewModels.EntityViewModels;

/// <summary>
/// Base interface for entity ViewModels that wrap database entities.
/// Provides common functionality for the left panel detail views.
/// </summary>
public interface IEntityViewModel
{
    /// <summary>
    /// Display name for the entity type (e.g., "Faction", "Army").
    /// Used as header in the left panel.
    /// </summary>
    string EntityTypeName { get; }
}
