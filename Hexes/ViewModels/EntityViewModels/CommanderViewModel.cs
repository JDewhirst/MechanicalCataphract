using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;

namespace GUI.ViewModels.EntityViewModels;

/// <summary>
/// ViewModel wrapper for Commander entity with auto-save on property change.
/// </summary>
public partial class CommanderViewModel : ObservableObject, IEntityViewModel
{
    private readonly Commander _commander;
    private readonly ICommanderService _service;

    public string EntityTypeName => "Commander";

    /// <summary>
    /// The underlying entity (for bindings that need direct access).
    /// </summary>
    public Commander Entity => _commander;

    public int Id => _commander.Id;

    public string Name
    {
        get => _commander.Name;
        set { if (_commander.Name != value) { _commander.Name = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public int? Age
    {
        get => _commander.Age;
        set { if (_commander.Age != value) { _commander.Age = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public string? DiscordHandle
    {
        get => _commander.DiscordHandle;
        set { if (_commander.DiscordHandle != value) { _commander.DiscordHandle = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }

    public int? LocationQ
    {
        get => _commander.LocationQ;
        set { if (_commander.LocationQ != value) { _commander.LocationQ = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }
    public int? LocationR
    {
        get => _commander.LocationR;
        set { if (_commander.LocationR != value) { _commander.LocationR = value; OnPropertyChanged(); _ = SaveAsync(); } }
    }
    public Faction? Faction => _commander.Faction;

    private async Task SaveAsync()
    {
        await _service.UpdateAsync(_commander);
    }

    public CommanderViewModel(Commander commander, ICommanderService service)
    {
        _commander = commander;
        _service = service;
    }
}
