using SeaBattle.Shared;
using SeaBattle.Shared.Player;
using Microsoft.Extensions.Logging;

namespace SeaBattle.Server.Services;

/// <summary>
/// Service responsible for all game logic and business rules.
/// Separates business logic from SignalR communication concerns.
/// </summary>
public class GameLogicService
{
    private readonly ILogger<GameLogicService> _logger;

    public GameLogicService(ILogger<GameLogicService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Processes a shot and returns the complete result including all state changes.
    /// </summary>
    /// <param name="game">The game state</param>
    /// <param name="playerId">The player making the shot</param>
    /// <param name="x">X coordinate of the shot</param>
    /// <param name="y">Y coordinate of the shot</param>
    /// <returns>Shot processing result with all necessary information</returns>
    public ShotProcessingResult ProcessShot(GameState game, Guid playerId, int x, int y)
    {
        try
        {
            // Validate turn
            var turnValidation = ValidatePlayerTurn(game, playerId);
            if (!turnValidation.IsValid)
            {
                return ShotProcessingResult.InvalidTurn(turnValidation.ErrorMessage);
            }

            // Validate coordinates and duplicate shots
            var shotValidation = ValidateShot(game, playerId, x, y);
            if (!shotValidation.IsValid)
            {
                return ShotProcessingResult.InvalidShot(shotValidation.ErrorMessage);
            }

            // Find opponent
            var opponent = GetOpponent(game, playerId);
            if (opponent == null)
            {
                _logger.LogError("Opponent not found for player {PlayerId} in game {GameId}", playerId, game.Id);
                return ShotProcessingResult.Error("Opponent not found");
            }

            // Process the actual shot
            var playerState = game.Players[playerId];
            var opponentState = opponent;
            var shotResult = opponentState.CheckShotResult(x, y);

            if (shotResult == null)
            {
                return ShotProcessingResult.InvalidShot("Invalid shot coordinates");
            }

            // Record the shot result
            RecordShotResult(playerState, shotResult, x, y);

            // Check for game over
            if (IsGameOver(opponentState))
            {
                return ProcessGameOver(game, playerId, opponent.PlayerId);
            }

            // Handle turn switching based on shot result
            var turnResult = ProcessTurnLogic(game, playerId, opponent.PlayerId, shotResult);

            return ShotProcessingResult.Success(shotResult, turnResult.NextPlayerId, turnResult.GameContinues);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing shot for player {PlayerId} at ({X}, {Y})", playerId, x, y);
            return ShotProcessingResult.Error("Failed to process shot");
        }
    }

    /// <summary>
    /// Validates if it's the player's turn to make a shot.
    /// </summary>
    private ValidationResult ValidatePlayerTurn(GameState game, Guid playerId)
    {
        if (!game.InProgress)
        {
            return ValidationResult.Invalid("Game is not in progress");
        }

        if (!game.Players.ContainsKey(playerId))
        {
            return ValidationResult.Invalid("Player not found in game");
        }

        var playerState = game.Players[playerId];
        if (playerState.State != PlayerStateEnum.InTurn)
        {
            return ValidationResult.Invalid("Not your turn");
        }

        return ValidationResult.Valid();
    }

    /// <summary>
    /// Validates shot coordinates and checks for duplicate shots.
    /// </summary>
    private ValidationResult ValidateShot(GameState game, Guid playerId, int x, int y)
    {
        // Validate coordinates
        if (x < 0 || x >= game.Size || y < 0 || y >= game.Size)
        {
            return ValidationResult.Invalid("Invalid coordinates");
        }

        var playerState = game.Players[playerId];
        
        // Check for duplicate shot using optimized method
        if (playerState.HasShotAt(x, y))
        {
            return ValidationResult.Invalid("Already shot at this location");
        }

        return ValidationResult.Valid();
    }

    /// <summary>
    /// Gets the opponent player for the given player ID.
    /// </summary>
    private PlayerState? GetOpponent(GameState game, Guid playerId)
    {
        return game.Players.Values.FirstOrDefault(p => p.PlayerId != playerId);
    }

    /// <summary>
    /// Records the shot result in the player's optimized tracking structures.
    /// </summary>
    private void RecordShotResult(PlayerState playerState, Dictionary<int, CellState> shotResult, int x, int y)
    {
        if (shotResult.Count == 1)
        {
            // Single shot result
            var singleResult = shotResult.First();
            var shotX = singleResult.Key / playerState.FieldSize;
            var shotY = singleResult.Key % playerState.FieldSize;
            playerState.RecordShotResult(shotX, shotY, singleResult.Value);
        }
        else
        {
            // Multiple results (ship destroyed + adjacent cells)
            playerState.RecordMultipleShotResults(shotResult, x, y);
        }
    }

    /// <summary>
    /// Checks if the game is over (opponent has no ships left).
    /// </summary>
    private bool IsGameOver(PlayerState opponentState)
    {
        return opponentState.Fleet.Ships.Count == 0;
    }

    /// <summary>
    /// Processes game over logic and state transitions.
    /// </summary>
    private ShotProcessingResult ProcessGameOver(GameState game, Guid winnerId, Guid loserId)
    {
        game.Stage = GameStageEnum.GameOver;
        
        var winner = game.Players[winnerId];
        var loser = game.Players[loserId];
        
        winner.SetWon();
        loser.SetLost();

        _logger.LogInformation("Game {GameId} ended - Winner: {WinnerId}, Loser: {LoserId}", 
            game.Id, winnerId, loserId);

        return ShotProcessingResult.GameOver(winnerId, loserId);
    }

    /// <summary>
    /// Handles turn switching logic based on shot results.
    /// </summary>
    private TurnResult ProcessTurnLogic(GameState game, Guid currentPlayerId, Guid opponentId, Dictionary<int, CellState> shotResult)
    {
        var currentPlayer = game.Players[currentPlayerId];
        var opponent = game.Players[opponentId];

        // If shot was a miss, switch turns
        if (!shotResult.Any(s => s.Value == CellState.hit))
        {
            opponent.SetInTurn();
            currentPlayer.SetWaitingForTurn();
            return new TurnResult(opponentId, true);
        }

        // If hit but not game over, current player continues
        return new TurnResult(currentPlayerId, true);
    }
}

/// <summary>
/// Result of shot processing containing all necessary information for the hub.
/// </summary>
public class ShotProcessingResult
{
    public bool IsSuccess { get; private set; }
    public bool IsGameOver { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Dictionary<int, CellState>? ShotResult { get; private set; }
    public Guid? NextPlayerId { get; private set; }
    public Guid? WinnerId { get; private set; }
    public Guid? LoserId { get; private set; }
    public bool GameContinues { get; private set; }

    private ShotProcessingResult() { }

    public static ShotProcessingResult Success(Dictionary<int, CellState> shotResult, Guid nextPlayerId, bool gameContinues)
    {
        return new ShotProcessingResult
        {
            IsSuccess = true,
            IsGameOver = false,
            ShotResult = shotResult,
            NextPlayerId = nextPlayerId,
            GameContinues = gameContinues
        };
    }

    public static ShotProcessingResult GameOver(Guid winnerId, Guid loserId)
    {
        return new ShotProcessingResult
        {
            IsSuccess = true,
            IsGameOver = true,
            WinnerId = winnerId,
            LoserId = loserId,
            GameContinues = false
        };
    }

    public static ShotProcessingResult InvalidTurn(string errorMessage)
    {
        return new ShotProcessingResult
        {
            IsSuccess = false,
            IsGameOver = false,
            ErrorMessage = errorMessage
        };
    }

    public static ShotProcessingResult InvalidShot(string errorMessage)
    {
        return new ShotProcessingResult
        {
            IsSuccess = false,
            IsGameOver = false,
            ErrorMessage = errorMessage
        };
    }

    public static ShotProcessingResult Error(string errorMessage)
    {
        return new ShotProcessingResult
        {
            IsSuccess = false,
            IsGameOver = false,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// Result of input validation.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; private set; }
    public string ErrorMessage { get; private set; } = string.Empty;

    private ValidationResult() { }

    public static ValidationResult Valid() => new ValidationResult { IsValid = true };
    
    public static ValidationResult Invalid(string errorMessage) => new ValidationResult 
    { 
        IsValid = false, 
        ErrorMessage = errorMessage 
    };
}

/// <summary>
/// Result of turn processing logic.
/// </summary>
public class TurnResult
{
    public Guid NextPlayerId { get; }
    public bool GameContinues { get; }

    public TurnResult(Guid nextPlayerId, bool gameContinues)
    {
        NextPlayerId = nextPlayerId;
        GameContinues = gameContinues;
    }
}