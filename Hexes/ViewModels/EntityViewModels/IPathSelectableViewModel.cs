namespace GUI.ViewModels.EntityViewModels;

/// <summary>
/// Interface for ViewModels that support path selection mode.
/// Eliminates type-checking chains in HexMapViewModel.
/// </summary>
public interface IPathSelectableViewModel
{
    bool IsPathSelectionActive { get; set; }
    int PathSelectionCount { get; set; }
}
