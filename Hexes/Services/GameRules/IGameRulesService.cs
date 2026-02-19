namespace MechanicalCataphract.Services;

public interface IGameRulesService
{
    GameRulesData Rules { get; }
    void Reload();
}
