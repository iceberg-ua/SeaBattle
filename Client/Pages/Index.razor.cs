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
            BattleHub.On<PlayerInfo>(nameof(IGameHub.JoinedGame), JoinedGame);
            BattleHub.On<Dictionary<int, CellState>, bool>(nameof(IGameHub.UpdateCellState), UpdateField);
            BattleHub.On(nameof(IGameHub.ClearField), ClearField);
            BattleHub.On<bool>(nameof(IGameHub.SetReady), SetReady);
            BattleHub.On<Guid>(nameof(IGameHub.GameStarted), GameStarted);
        }

        base.OnAfterRender(firstRender);
    }

    private bool ClearButtonDisable => _field.All(c => c == CellState.empty);

    private bool FleetComplete { get; set; } = false;

    private string OwnFieldState { get; set; } = "";

    private string EnemyFieldState { get; set; } = "";

    private bool Waiting { get; set; } = false;

    private CellState[] InitField(int size) => new CellState[size * size];

    private void SetOwnFieldState(bool enabled)
    {
        OwnFieldState = enabled ? "" : "hover-disabled";
    }

    private void SetEnemyFieldState(bool enabled)
    {
        EnemyFieldState = enabled ? "" : "hover-disabled";
    }

    #region Events

    private async Task JoinButtonClicked(string userName)
    {
        await BattleHub?.SendAsync(nameof(IGameHub.JoinGame), Guid.Empty, userName)!;
    }

    private async void FieldCellClicked((int x, int y) cell)
    {
        await BattleHub?.SendAsync(nameof(IGameHub.CellClicked), Player.Id, cell.x, cell.y)!;
    }

    private async void ClearButtonClicked(MouseEventArgs e)
    {
        await BattleHub?.SendAsync(nameof(IGameHub.ClearField), Player.Id)!;
    }

    private async Task OnReadyButtonClick()
    {
        await BattleHub?.SendAsync(nameof(IGameHub.PlayerReady), Player.Id)!;
    }

    #endregion

    #region Hub API

    private async Task JoinedGame(PlayerInfo player)
    {
        Player = player;
        _field = InitField(Player.FieldSize);

        await LocalStorage.SetItemAsync("player", player.Id);
        await InvokeAsync(StateHasChanged);
    }

    private async Task UpdateField(Dictionary<int, CellState> cells, bool complete)
    {
        foreach (var shot in cells)
        {
            _field[shot.Key] = shot.Value;
        }

        FleetComplete = complete;

        await InvokeAsync(StateHasChanged);
    }

    private async Task ClearField()
    {
        _field = InitField(Player.FieldSize);

        await InvokeAsync(StateHasChanged);
    }

    private async Task SetReady(bool obj)
    {
        Waiting = true;
        SetOwnFieldState(false);

        await InvokeAsync(StateHasChanged);
    }

    private async Task GameStarted(Guid gameId)
    {
        // _enemyField = new CellState[Player.FieldSize * Player.FieldSize];

        // Player.InProgress = true;
        // Waiting = false;

        await InvokeAsync(StateHasChanged);
    }

    #endregion
}