using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;
using SeaBattle.Shared;
using SeaBattle.Shared.Hub;
using SeaBattle.Shared.Player;

namespace SeaBattle.Client.Pages;

public partial class Index
{
    [CascadingParameter] public HubConnection BattleHub { get; set; } = null!;

    [CascadingParameter] public PlayerInfo Player { get; set; } = null!;

    public GameStateEnum CurrentState { get; set; } = GameStateEnum.Setup;

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

        _field = InitField(Player.FieldSize);

        return base.OnInitializedAsync();
    }

    private bool ClearButtonDisable => _field.All(c => c == CellState.empty);

    private bool FleetComplete { get; set; }

    private bool IsStarted { get; set; }

    private bool WaitingForShot { get; set; }

    private bool WaitingOpponent { get; set; }

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
        _field = InitField(Player.FieldSize);

        await InvokeAsync(StateHasChanged);
    }

    private async Task OnSetReady(bool obj)
    {
        WaitingOpponent = true;
        CurrentState = GameStateEnum.Waiting;

        await InvokeAsync(StateHasChanged);
    }

    private async Task OnGameStarted()
    {
        IsStarted = true;
        _enemyField = InitField(Player.FieldSize);

        await InvokeAsync(StateHasChanged);
    }

    private async Task OnMoveTransition(bool move)
    {
        if (move)
        {
            WaitingForShot = true;
            CurrentState = GameStateEnum.InTurn;
        }
        else
        {
            CurrentState = GameStateEnum.OpponentsTurn;
            WaitingForShot = false;
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task OnGameOver(bool win)
    {
        CurrentState = GameStateEnum.GameOver;

        var resultMsg = win ? "WIN" : "LOST";
        _gameOverString = $"Game over! You {resultMsg}";
        _gameOverClass = resultMsg.ToLower();
        _gameIsOver = true;

        await InvokeAsync(StateHasChanged);
    }

    #endregion
}