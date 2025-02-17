using Microsoft.AspNetCore.Components;
using SeaBattle.Shared;
using Index = SeaBattle.Client.Pages.Index;

namespace SeaBattle.Client.Components;

public partial class GameBoard
{
    [Parameter]
    public int FieldSize { get; set; }

    [Parameter]
    public EventCallback<(int, int)> CellClicked { get; set; }
    
    [Parameter]
    public CellState[] Field { get; set; } = null!;
    
    [Parameter]
    public CellState[] EnemyField { get; set; } = null!;

    [Parameter]
    public bool GameStarted { get; set; }
    
    [Parameter]
    public Index.GameStateEnum CurrentState { get; set; } = Index.GameStateEnum.Waiting;

    private bool OwnFieldDisabled => CurrentState is not Index.GameStateEnum.Setup;

    private bool EnemyFieldDisabled => CurrentState is not Index.GameStateEnum.InTurn;
    
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