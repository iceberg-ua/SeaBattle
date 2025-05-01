using SeaBattle.Shared.Player;

namespace SeaBattle.Shared;

public enum CellState
{
    empty = 0,
    ship,
    hit,
    miss
}

public class GameState
{
    public Guid Id { get; } = Guid.NewGuid();

    public int Size { get; } = 10;

    public GameStageEnum Stage { get; set; } = GameStageEnum.Setup;

    public bool InProgress => Stage == GameStageEnum.Game;

    public Dictionary<Guid, PlayerState> Players { get; } = new(2);

}

///NOTES:
//
// * introduce mode property which will handle cell click (preparing mode, battle mode)
// * refresh after game was over shows wrong state
// * after each move show the text result in a lable


// show name of the opponent
// show the rest of the fleet to shot down/to place
// better indicate change 
// show the rest of the opponents fleet after game change