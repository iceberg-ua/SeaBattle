namespace SeaBattle.Shared.Player;

public record PlayerInfo(Guid Id, string Name, string OponentsName, int FieldSize, Dictionary<int, CellState> FieldState);
