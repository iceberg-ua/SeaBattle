﻿using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using SeaBattle.Shared.Hub;
using SeaBattle.Shared;

namespace SeaBattle.Client.Shared;

public partial class MainLayout
{
    [Parameter]
    public RenderFragment? Body { get; set; }

    public HubConnection BattleHub { get; private set; } = null!;
    public GameStateClient GameState { get; set; } = null!;

    private bool _initialized;

    protected override async Task OnInitializedAsync()
    {
        BattleHub = new HubConnectionBuilder()
            .WithUrl(Navigation.ToAbsoluteUri("/battlehub"))
            .Build();

        //subscribe to server event when game is created/found and player is joined to it
        BattleHub.On<GameStateClient>(nameof(IGameHub.JoinedGame), OnJoinedGame);

        await BattleHub.StartAsync();
        await TryGetPlayerState();
    }

    #region Hub Handlers

    private async Task TryGetPlayerState()
    {
        var playerId = await LocalStorage.GetItemAsync("player");
        _ = Guid.TryParse(playerId, out var id);
        await BattleHub?.SendAsync(nameof(IGameHub.JoinGame), id, "")!; //request player info with the id
    }

    private async Task OnJoinedGame(GameStateClient? gameState)
    {
        _initialized = true;

        if (gameState is null)
            Navigation.NavigateTo("/sign-in");
        else
        {
            GameState = gameState;
            await LocalStorage.SetItemAsync("player", GameState.Player.Id);
            Navigation.NavigateTo("/");
        }
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        await BattleHub.DisposeAsync();
    }
}
