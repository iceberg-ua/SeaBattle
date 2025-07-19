using SeaBattle.Shared;

namespace SeaBattle.Client.Services;

public interface IGameStateService
{
    GameStateClient? GameState { get; }
    bool HasState { get; }
    event Action<GameStateClient?>? StateChanged;
    
    void UpdateGameState(GameStateClient? newState);
    void ClearState();
}