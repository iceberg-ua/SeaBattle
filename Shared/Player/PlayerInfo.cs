namespace SeaBattle.Shared.Player;

public record PlayerInfo(Guid Id, string Name, PlayerStateEnum State, Dictionary<int, CellState> FieldState);
