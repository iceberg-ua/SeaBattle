using SeaBattle.Shared;

namespace SeaBattle.Client.Services;

public class GameStateService : IGameStateService
{
    private GameStateClient? _gameState;
    
    public GameStateClient? GameState => _gameState;

    public bool HasState => _gameState != null;

    public event Action<GameStateClient?>? StateChanged;
    
    public void UpdateGameState(GameStateClient? newState)
    {
        _gameState = newState;
        StateChanged?.Invoke(newState);
    }
    
    public void ClearState()
    {
        _gameState = null;
        StateChanged?.Invoke(null);
    }
}