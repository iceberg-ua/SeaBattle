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
    public HubConnection BattleHub { get; set; } = default!;

    [CascadingParameter]
    public PlayerInfo Player { get; set; } = default!;

    public CellState[] _field = default!;
    public CellState[] _enemyField = default!;

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            BattleHub.On<PlayerInfo>(nameof(IGameHub.JoinedGame), OnJoinedGame);
            BattleHub.On<Dictionary<int, CellState>, bool>(nameof(IGameHub.UpdateCellState), OnUpdateField);
            BattleHub.On(nameof(IGameHub.ClearField), OnClearField);
            BattleHub.On<bool>(nameof(IGameHub.SetReady), OnSetReady);
            BattleHub.On(nameof(IGameHub.GameStarted), OnGameStarted);
            BattleHub.On(nameof(IGameHub.MoveTransition), OnMoveTransition);
        }

        base.OnAfterRender(firstRender);
    }

    private bool ClearButtonDisable => _field.All(c => c == CellState.empty);

    private bool FleetComplete { get; set; } = false;

    private bool IsStarted { get; set; } = false;

    private bool WaitingForShot { get; set; } = false;

    private string OwnFieldState { get; set; } = "";

    private string EnemyFieldState { get; set; } = "";

    private bool WaitingOpponent { get; set; } = false;

    private CellState[] InitField(int size) => new CellState[size * size];

    private void SetOwnFieldState(bool enabled)
    {
        OwnFieldState = enabled ? "" : "hover-disabled";
    }

    private void SetEnemyFieldState(bool enabled)
    {
        EnemyFieldState = enabled ? "" : "hover-disabled";
    }

    private void EnableEnemyField() => SetEnemyFieldState(true);

    private void DisableEnemyField() => SetEnemyFieldState(false);

    #region Events

    private async Task JoinButtonClicked(string userName)
    {
        await BattleHub?.SendAsync(nameof(IGameHub.JoinGame), Guid.Empty, userName)!;
    }

    private async void FieldCellClicked((int x, int y) cell)
    {
        await BattleHub?.SendAsync(nameof(IGameHub.CellClicked), Player.Id, cell.x, cell.y)!;

        if (IsStarted)
        {
            DisableEnemyField();
            WaitingForShot = false;
        }
    }

    private async void ClearButtonClicked(MouseEventArgs e)
    {
        if(!ClearButtonDisable)
            await BattleHub?.SendAsync(nameof(IGameHub.ClearField), Player.Id)!;
    }

    private async Task OnReadyButtonClick()
    {
        if(FleetComplete)
            await BattleHub?.SendAsync(nameof(IGameHub.PlayerReady), Player.Id)!;
    }

    #endregion

    #region Hub API

    private async Task OnJoinedGame(PlayerInfo player)
    {
        Player = player;
        _field = InitField(Player.FieldSize);

        await LocalStorage.SetItemAsync("player", player.Id);
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnUpdateField(Dictionary<int, CellState> cells, bool complete)
    {
        foreach (var shot in cells)
        {
            _field[shot.Key] = shot.Value;
        }

        FleetComplete = complete;

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
        SetOwnFieldState(false);

        await InvokeAsync(StateHasChanged);
    }

    private async Task OnGameStarted()
    {
        IsStarted = true;
        _enemyField = InitField(Player.FieldSize);
        SetOwnFieldState(false);
        DisableEnemyField();

        await InvokeAsync(StateHasChanged);
    }

    private async Task OnMoveTransition()
    {
        WaitingForShot = true;
        EnableEnemyField();

        await InvokeAsync(StateHasChanged);
    }

    #endregion
}