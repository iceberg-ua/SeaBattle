namespace SeaBattle.Shared.Player;

public record PlayerInfo(Guid Id, string Name, int FieldSize, Dictionary<int, CellState> FieldState);
