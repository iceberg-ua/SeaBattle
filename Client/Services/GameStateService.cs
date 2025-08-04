using SeaBattle.Shared;
using Microsoft.Extensions.Logging;

namespace SeaBattle.Client.Services;

public class GameStateService : IGameStateService
{
    private readonly object _stateLock = new();
    private readonly ILogger<GameStateService> _logger;
    private GameStateClient? _gameState;
    private long _stateVersion = 0;
    private DateTime _lastUpdateTime = DateTime.UtcNow;
    
    public GameStateService(ILogger<GameStateService> logger)
    {
        _logger = logger;
    }
    
    public GameStateClient? GameState 
    { 
        get 
        { 
            lock (_stateLock) 
            { 
                return _gameState; 
            } 
        } 
    }

    public bool HasState 
    { 
        get 
        { 
            lock (_stateLock) 
            { 
                return _gameState != null; 
            } 
        } 
    }

    public long StateVersion 
    { 
        get 
        { 
            lock (_stateLock) 
            { 
                return _stateVersion; 
            } 
        } 
    }

    public event Action<GameStateClient?>? StateChanged;
    
    public bool UpdateGameState(GameStateClient? newState)
    {
        lock (_stateLock)
        {
            // Validate the new state before applying it
            if (newState != null && !ValidateStateUpdate(newState))
            {
                _logger.LogWarning("Invalid state update rejected. Current stage: {CurrentStage}, New stage: {NewStage}", 
                    _gameState?.Stage, newState.Stage);
                return false;
            }

            // Check for version conflicts if both states have version info
            if (_gameState != null && newState != null && 
                newState.StateVersion > 0 && _gameState.StateVersion > 0 && 
                newState.StateVersion <= _gameState.StateVersion)
            {
                _logger.LogWarning("State version conflict. Current: {CurrentVersion}, New: {NewVersion}", 
                    _gameState.StateVersion, newState.StateVersion);
                return false;
            }

            var previousState = _gameState;
            _gameState = newState;
            
            // Update our local version tracking
            _stateVersion++;
            _lastUpdateTime = DateTime.UtcNow;
            
            // Update the state's metadata
            _gameState?.UpdateStateMetadata();

            _logger.LogDebug("State updated to version {LocalVersion} (Server version: {ServerVersion}). Stage: {Stage}, Player: {PlayerId}", 
                _stateVersion, newState?.StateVersion, newState?.Stage, newState?.Player?.Id);

            // Invoke state changed event outside the lock to prevent deadlocks
            Task.Run(() => StateChanged?.Invoke(newState));
            
            return true;
        }
    }
    
    public void ClearState()
    {
        lock (_stateLock)
        {
            _gameState = null;
            _stateVersion++;
            _lastUpdateTime = DateTime.UtcNow;
            
            _logger.LogDebug("State cleared. Version: {Version}", _stateVersion);
            
            // Invoke state changed event outside the lock to prevent deadlocks
            Task.Run(() => StateChanged?.Invoke(null));
        }
    }

    public event Action? StateRefreshRequested;

    public bool RequestStateRefresh()
    {
        lock (_stateLock)
        {
            _logger.LogInformation("State refresh requested. Current version: {Version}", _stateVersion);
            
            // Trigger the refresh request event for components to handle
            Task.Run(() => StateRefreshRequested?.Invoke());
            
            return true;
        }
    }

    public bool TryRecoverFromCorruption()
    {
        lock (_stateLock)
        {
            var validationResult = ValidateCurrentState();
            if (validationResult.IsValid)
            {
                _logger.LogDebug("State validation passed, no recovery needed");
                return true;
            }

            _logger.LogWarning("State corruption detected: {Issues}. Attempting recovery.", validationResult.Message);

            // Attempt basic recovery
            if (_gameState != null)
            {
                bool recovered = false;

                // Try to fix field arrays if they're corrupted
                if (_gameState.OwnField?.Length != _gameState.FieldSize * _gameState.FieldSize)
                {
                    _logger.LogInformation("Recovering corrupted OwnField array");
                    _gameState.InitializeFields();
                    recovered = true;
                }

                if (_gameState.EnemyField?.Length != _gameState.FieldSize * _gameState.FieldSize)
                {
                    _logger.LogInformation("Recovering corrupted EnemyField array");
                    if (_gameState.OwnField == null)
                    {
                        _gameState.InitializeFields();
                    }
                    else
                    {
                        _gameState.EnemyField = new CellState[_gameState.FieldSize * _gameState.FieldSize];
                    }
                    recovered = true;
                }

                if (recovered)
                {
                    _gameState.UpdateStateMetadata();
                    _logger.LogInformation("State recovery completed successfully");
                    Task.Run(() => StateChanged?.Invoke(_gameState));
                    return true;
                }
            }

            // If we can't recover, request fresh state from server
            _logger.LogWarning("Unable to recover corrupted state locally. Requesting fresh state from server.");
            RequestStateRefresh();
            return false;
        }
    }

    public StateValidationResult ValidateCurrentState()
    {
        lock (_stateLock)
        {
            if (_gameState == null)
            {
                return new StateValidationResult(true, "No state to validate");
            }

            var issues = new List<string>();
            
            // Validate field arrays
            if (_gameState.OwnField == null)
            {
                issues.Add("OwnField is null");
            }
            else if (_gameState.OwnField.Length != _gameState.FieldSize * _gameState.FieldSize)
            {
                issues.Add($"OwnField size mismatch. Expected: {_gameState.FieldSize * _gameState.FieldSize}, Actual: {_gameState.OwnField.Length}");
            }

            if (_gameState.EnemyField == null)
            {
                issues.Add("EnemyField is null");
            }
            else if (_gameState.EnemyField.Length != _gameState.FieldSize * _gameState.FieldSize)
            {
                issues.Add($"EnemyField size mismatch. Expected: {_gameState.FieldSize * _gameState.FieldSize}, Actual: {_gameState.EnemyField.Length}");
            }

            // Validate player state
            if (_gameState.Player == null)
            {
                issues.Add("Player is null");
            }

            // Validate stage transitions
            if (!IsValidStage(_gameState.Stage))
            {
                issues.Add($"Invalid stage: {_gameState.Stage}");
            }

            bool isValid = issues.Count == 0;
            string message = isValid ? "State is valid" : string.Join("; ", issues);
            
            if (!isValid)
            {
                _logger.LogWarning("State validation failed: {Issues}", message);
            }

            return new StateValidationResult(isValid, message);
        }
    }

    private bool ValidateStateUpdate(GameStateClient newState)
    {
        // If no current state, any valid state is acceptable
        if (_gameState == null)
        {
            return ValidateNewState(newState);
        }

        // Validate state transition
        if (!IsValidStateTransition(_gameState.Stage, newState.Stage))
        {
            return false;
        }

        // Validate that essential data doesn't regress
        if (_gameState.Player?.Id != newState.Player?.Id)
        {
            _logger.LogWarning("Player ID changed from {OldId} to {NewId}", _gameState.Player?.Id, newState.Player?.Id);
            return false;
        }

        // Validate field sizes haven't changed
        if (_gameState.FieldSize != newState.FieldSize)
        {
            _logger.LogWarning("Field size changed from {OldSize} to {NewSize}", _gameState.FieldSize, newState.FieldSize);
            return false;
        }

        return ValidateNewState(newState);
    }

    private bool ValidateNewState(GameStateClient state)
    {
        // Basic null checks
        if (state.Player == null)
        {
            _logger.LogWarning("State validation failed: Player is null");
            return false;
        }

        // Field size validation
        if (state.FieldSize <= 0 || state.FieldSize > 20)
        {
            _logger.LogWarning("State validation failed: Invalid field size {Size}", state.FieldSize);
            return false;
        }

        // Array validation
        var expectedLength = state.FieldSize * state.FieldSize;
        if (state.OwnField?.Length != expectedLength)
        {
            _logger.LogWarning("State validation failed: OwnField length {Length} doesn't match expected {Expected}", 
                state.OwnField?.Length, expectedLength);
            return false;
        }

        if (state.EnemyField?.Length != expectedLength)
        {
            _logger.LogWarning("State validation failed: EnemyField length {Length} doesn't match expected {Expected}", 
                state.EnemyField?.Length, expectedLength);
            return false;
        }

        return true;
    }

    private bool IsValidStateTransition(GameStageEnum from, GameStageEnum to)
    {
        // Allow same stage (state updates within same stage)
        if (from == to) return true;

        // Valid transitions
        return (from, to) switch
        {
            (GameStageEnum.Setup, GameStageEnum.Game) => true,
            (GameStageEnum.Game, GameStageEnum.GameOver) => true,
            (GameStageEnum.GameOver, GameStageEnum.Setup) => true, // New game
            _ => false
        };
    }

    private bool IsValidStage(GameStageEnum stage)
    {
        return Enum.IsDefined(typeof(GameStageEnum), stage);
    }
}

public record StateValidationResult(bool IsValid, string Message);