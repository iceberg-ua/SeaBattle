using System.Collections.Concurrent;

namespace SeaBattle.Server.Services;

/// <summary>
/// Service that tracks SignalR connections and maps them to players and games.
/// Enables proper cleanup when connections are lost.
/// </summary>
public class ConnectionTrackingService
{
    private readonly ConcurrentDictionary<string, PlayerConnection> _connections = new();
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _playerConnections = new();
    private readonly Lock _playerConnectionsLock = new();

    /// <summary>
    /// Information about a player's connection.
    /// </summary>
    public record PlayerConnection(Guid PlayerId, Guid? GameId, DateTime ConnectedAt);

    /// <summary>
    /// Registers a new connection for a player.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID</param>
    /// <param name="playerId">The player ID</param>
    /// <param name="gameId">The game ID (if player is in a game)</param>
    public void RegisterConnection(string connectionId, Guid playerId, Guid? gameId = null)
    {
        var connection = new PlayerConnection(playerId, gameId, DateTime.UtcNow);
        _connections.AddOrUpdate(connectionId, connection, (_, _) => connection);

        lock (_playerConnectionsLock)
        {
            if (!_playerConnections.TryGetValue(playerId, out var connections))
            {
                connections = new HashSet<string>();
                _playerConnections[playerId] = connections;
            }
            connections.Add(connectionId);
        }
    }

    /// <summary>
    /// Updates the game ID for an existing connection.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID</param>
    /// <param name="gameId">The new game ID</param>
    public void UpdateConnectionGame(string connectionId, Guid gameId)
    {
        if (_connections.TryGetValue(connectionId, out var connection))
        {
            var updatedConnection = connection with { GameId = gameId };
            _connections.TryUpdate(connectionId, updatedConnection, connection);
        }
    }

    /// <summary>
    /// Removes a connection when it disconnects.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID</param>
    /// <returns>The connection information if it existed</returns>
    public PlayerConnection? RemoveConnection(string connectionId)
    {
        if (_connections.TryRemove(connectionId, out var connection))
        {
            lock (_playerConnectionsLock)
            {
                if (_playerConnections.TryGetValue(connection.PlayerId, out var connections))
                {
                    connections.Remove(connectionId);
                    
                    // Clean up empty player connection sets
                    if (connections.Count == 0)
                    {
                        _playerConnections.TryRemove(connection.PlayerId, out _);
                    }
                }
            }
            return connection;
        }
        return null;
    }

    /// <summary>
    /// Checks if a player has any active connections.
    /// </summary>
    /// <param name="playerId">The player ID to check</param>
    /// <returns>True if the player has active connections</returns>
    public bool IsPlayerConnected(Guid playerId)
    {
        lock (_playerConnectionsLock)
        {
            return _playerConnections.TryGetValue(playerId, out var connections) && connections.Count > 0;
        }
    }

    /// <summary>
    /// Gets all active connections for a player.
    /// </summary>
    /// <param name="playerId">The player ID</param>
    /// <returns>Set of connection IDs for the player</returns>
    public HashSet<string> GetPlayerConnections(Guid playerId)
    {
        lock (_playerConnectionsLock)
        {
            if (_playerConnections.TryGetValue(playerId, out var connections))
            {
                return new HashSet<string>(connections);
            }
            return new HashSet<string>();
        }
    }

    /// <summary>
    /// Gets connection information for a specific connection.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID</param>
    /// <returns>Connection information if it exists</returns>
    public PlayerConnection? GetConnection(string connectionId)
    {
        return _connections.TryGetValue(connectionId, out var connection) ? connection : null;
    }

    /// <summary>
    /// Gets all players who are currently connected.
    /// </summary>
    /// <returns>Set of connected player IDs</returns>
    public HashSet<Guid> GetConnectedPlayers()
    {
        lock (_playerConnectionsLock)
        {
            return new HashSet<Guid>(_playerConnections.Keys);
        }
    }

    /// <summary>
    /// Gets connection statistics for monitoring.
    /// </summary>
    /// <returns>Connection statistics</returns>
    public ConnectionStats GetStats()
    {
        lock (_playerConnectionsLock)
        {
            return new ConnectionStats(
                TotalConnections: _connections.Count,
                ConnectedPlayers: _playerConnections.Count,
                AverageConnectionsPerPlayer: _playerConnections.Count > 0 
                    ? (double)_connections.Count / _playerConnections.Count 
                    : 0.0
            );
        }
    }

    /// <summary>
    /// Statistics about current connections.
    /// </summary>
    public record ConnectionStats(int TotalConnections, int ConnectedPlayers, double AverageConnectionsPerPlayer);
}