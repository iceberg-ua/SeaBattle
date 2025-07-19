using Microsoft.AspNetCore.Components;
using SeaBattle.Shared;

namespace SeaBattle.Client.Components;

public partial class GameBoard
{
    private GameStateClient? GameState => GameStateService.GameState;

    [Parameter]
    public EventCallback<(int, int)> CellClicked { get; set; }

    [Parameter]
    public CellState[] Field { get; set; } = null!;

    [Parameter]
    public CellState[] EnemyField { get; set; } = null!;

    private bool OwnFieldDisabled => GameState?.Player.State is not PlayerStateEnum.Formation;

    private bool EnemyFieldDisabled => GameState?.Player.State is PlayerStateEnum.WaitingForTurn;

    private async void OnCellClicked((int x, int y) cell)
    {
        try
        {
            await CellClicked.InvokeAsync(cell).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}