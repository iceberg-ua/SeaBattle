using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;
using SeaBattle.Shared;
using SeaBattle.Shared.Hub;
using SeaBattle.Shared.Player;

namespace SeaBattle.Client.Pages;

public partial class Index
{
    [CascadingParameter]
    public HubConnection BattleHub { get; set; } = null!;

    [CascadingParameter]
    public required GameStateClient GameState { get; set; }

    public PlayerInfo Player => GameState.Player!;

    public CellState[] _field = default!;
    public CellState[] _enemyField = default!;

    public bool _gameIsOver = false;
    public string _gameOverString = string.Empty;
    public string _gameOverClass = string.Empty;

    public enum GameStateEnum
    {
        Setup,
        Waiting,
        InTurn,
        OpponentsTurn,
        GameOver
    }

    protected override Task OnInitializedAsync()
    {
        BattleHub.On<Dictionary<int, CellState>, bool>(nameof(IGameHub.UpdateCellState), OnUpdateField);
        BattleHub.On<Dictionary<int, CellState>>(nameof(IGameHub.UpdateEnemyCellState), OnUpdateEnemyField);
        BattleHub.On(nameof(IGameHub.ClearField), OnClearField);
        BattleHub.On<bool>(nameof(IGameHub.SetReady), OnSetReady);
        BattleHub.On(nameof(IGameHub.GameStarted), OnGameStarted);
        BattleHub.On<bool>(nameof(IGameHub.MoveTransition), OnMoveTransition);
        BattleHub.On<bool>(nameof(IGameHub.GameOver), OnGameOver);

        _field = InitField(GameState.FieldSize);

        return base.OnInitializedAsync();
    }

    private bool ClearButtonDisable => _field.All(c => c == CellState.empty);

    private bool FleetComplete { get; set; }

    private bool IsStarted => GameState.Stage == GameStageEnum.Game;

    private bool IsReady => GameState.Player.State == PlayerStateEnum.Ready;

    private static CellState[] InitField(int size) => new CellState[size * size];

    #region Events

    private async void FieldCellClicked((int x, int y) cell)
    {
        await BattleHub?.SendAsync(nameof(IGameHub.CellClicked), Player.Id, cell.x, cell.y)!;
    }

    private async void ClearButtonClicked(MouseEventArgs e)
    {
        if (!ClearButtonDisable)
            await BattleHub?.SendAsync(nameof(IGameHub.ClearField), Player.Id)!;
    }

    private async Task OnReadyButtonClick()
    {
        if (FleetComplete)
            await BattleHub?.SendAsync(nameof(IGameHub.PlayerReady), Player.Id)!;
    }

    #endregion

    #region Hub API

    private async Task OnUpdateField(Dictionary<int, CellState> cells, bool complete)
    {
        foreach (var shot in cells)
        {
            _field[shot.Key] = shot.Value;
        }

        if (!IsStarted)
            FleetComplete = complete;

        await InvokeAsync(StateHasChanged);
    }

    private async Task OnUpdateEnemyField(Dictionary<int, CellState> cells)
    {
        foreach (var shot in cells)
        {
            _enemyField[shot.Key] = shot.Value;
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task OnClearField()
    {
        _field = InitField(GameState.FieldSize);

        await InvokeAsync(StateHasChanged);
    }

    private async Task OnSetReady(bool obj)
    {
        await UpdatePlayerState(PlayerStateEnum.Ready);
    }

    private async ValueTask OnGameStarted()
    {
        _enemyField = InitField(GameState.FieldSize);
        await UpdateGameStage(GameStageEnum.Game);
    }

    private async Task OnMoveTransition(bool move)
    {
        await UpdatePlayerState(move ? PlayerStateEnum.InTurn : PlayerStateEnum.WaitingForTurn);
    }

    private async Task OnGameOver(bool win)
    {
        var resultMsg = win ? "WIN" : "LOST";
        _gameOverString = $"Game over! You {resultMsg}";
        _gameOverClass = resultMsg.ToLower();
        _gameIsOver = true;

        await UpdatePlayerState(win ? PlayerStateEnum.Won : PlayerStateEnum.Lost);
        await UpdateGameStage(GameStageEnum.GameOver);
    }

    #endregion

    private async ValueTask UpdateGameStage(GameStageEnum newStage)
    {
        GameState.Stage = newStage;
        await InvokeAsync(StateHasChanged);
    }

    private async ValueTask UpdatePlayerState(PlayerStateEnum newState)
    {
        GameState.Player = GameState.Player with { State = newState };
        await InvokeAsync(StateHasChanged);
    }
}