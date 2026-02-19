using CommunityToolkit.Mvvm.ComponentModel;
using MechanicalCataphract.Data.Entities;
using MechanicalCataphract.Services;
using System.Threading.Tasks;

namespace GUI.ViewModels.EntityViewModels;

/// <summary>
/// Thin ViewModel wrapper around FactionRule with auto-save on property change.
/// </summary>
public partial class FactionRuleViewModel : ObservableObject
{
    private readonly FactionRule _rule;
    private readonly IFactionRuleService _service;

    public int Id => _rule.Id;
    public FactionRule Entity => _rule;

    public string RuleKey
    {
        get => _rule.RuleKey;
        set
        {
            if (_rule.RuleKey != value)
            {
                _rule.RuleKey = value;
                OnPropertyChanged();
                _ = SaveAsync();
            }
        }
    }

    public double Value
    {
        get => _rule.Value;
        set
        {
            if (_rule.Value != value)
            {
                _rule.Value = value;
                OnPropertyChanged();
                _ = SaveAsync();
            }
        }
    }

    private async Task SaveAsync()
    {
        await _service.UpdateRuleAsync(_rule);
    }

    public FactionRuleViewModel(FactionRule rule, IFactionRuleService service)
    {
        _rule = rule;
        _service = service;
    }
}
