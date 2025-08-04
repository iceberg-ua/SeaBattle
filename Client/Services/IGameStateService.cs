using SeaBattle.Shared;

namespace SeaBattle.Client.Services;

public interface IGameStateService
{
    GameStateClient? GameState { get; }
    bool HasState { get; }
    long StateVersion { get; }
    event Action<GameStateClient?>? StateChanged;
    event Action? StateRefreshRequested;
    
    bool UpdateGameState(GameStateClient? newState);
    void ClearState();
    bool RequestStateRefresh();
    StateValidationResult ValidateCurrentState();
    bool TryRecoverFromCorruption();
}