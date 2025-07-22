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
            .WithAutomaticReconnect()
            .Build();

        // Subscribe to connection state events
        BattleHub.Closed += OnConnectionClosed;
        BattleHub.Reconnecting += OnConnectionReconnecting;
        BattleHub.Reconnected += OnConnectionReconnected;

        //subscribe to server event when game is created/found and player is joined to it
        BattleHub.On<GameStateClient>(nameof(IGameHub.JoinedGame), OnJoinedGame);
        BattleHub.On<string>(nameof(IGameHub.Error), OnError);
        BattleHub.On<Guid>(nameof(IGameHub.PlayerDisconnected), OnPlayerDisconnected);

        await BattleHub.StartAsync();
        await TryGetPlayerState();
    }

    #region Hub Handlers

    private async Task TryGetPlayerState()
    {
        var playerId = await LocalStorage.GetItemAsync("player");
        if (Guid.TryParse(playerId, out var id) && id != Guid.Empty)
        {
            // Try to reconnect to existing game first
            await BattleHub?.SendAsync(nameof(IGameHub.ReconnectToGame), id)!;
        }
        else
        {
            // No valid player ID, start fresh
            await BattleHub?.SendAsync(nameof(IGameHub.JoinGame), Guid.Empty, "")!;
        }
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

    private async Task OnError(string message)
    {
        // Log error and potentially redirect to sign-in for critical errors
        Console.WriteLine($"Hub Error: {message}");
        
        // If it's a critical connection error, redirect to sign-in
        if (message.Contains("Invalid player ID") || message.Contains("Game not found"))
        {
            Navigation.NavigateTo("/sign-in");
        }
        
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnPlayerDisconnected(Guid disconnectedPlayerId)
    {
        Console.WriteLine($"Player {disconnectedPlayerId} disconnected from the game");
        
        // Update UI to show disconnection notification
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnConnectionClosed(Exception? exception)
    {
        Console.WriteLine($"Connection closed. Exception: {exception?.Message}");
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnConnectionReconnecting(Exception? exception)
    {
        Console.WriteLine($"Connection reconnecting. Exception: {exception?.Message}");
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnConnectionReconnected(string? connectionId)
    {
        Console.WriteLine($"Connection reconnected with ID: {connectionId}");
        
        // Attempt to rejoin the game after reconnection
        await TryGetPlayerState();
        await InvokeAsync(StateHasChanged);
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        GameStateService.StateChanged -= OnGameStateChanged;
        
        // Unsubscribe from connection events
        if (BattleHub != null)
        {
            BattleHub.Closed -= OnConnectionClosed;
            BattleHub.Reconnecting -= OnConnectionReconnecting;
            BattleHub.Reconnected -= OnConnectionReconnected;
            await BattleHub.DisposeAsync();
        }
    }
}
