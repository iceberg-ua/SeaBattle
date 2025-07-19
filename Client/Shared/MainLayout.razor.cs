using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using SeaBattle.Shared.Hub;
using SeaBattle.Shared;

namespace SeaBattle.Client.Shared;

public partial class MainLayout
{
    [Parameter]
    public RenderFragment? Body { get; set; }

    public HubConnection BattleHub { get; private set; } = null!;

    private bool _initialized;

    protected override async Task OnInitializedAsync()
    {
        // Subscribe to game state changes
        GameStateService.StateChanged += OnGameStateChanged;
        
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
            GameStateService.UpdateGameState(gameState);
            await LocalStorage.SetItemAsync("player", gameState.Player.Id);
            Navigation.NavigateTo("/");
        }
    }
    
    private void OnGameStateChanged(GameStateClient? newState)
    {
        InvokeAsync(StateHasChanged);
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        GameStateService.StateChanged -= OnGameStateChanged;
        await BattleHub.DisposeAsync();
    }
}
