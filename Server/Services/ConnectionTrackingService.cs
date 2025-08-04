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
    private readonly ConcurrentDictionary<Guid, PlayerDisconnection> _pendingDisconnections = new();
    private readonly Lock _playerConnectionsLock = new();

    /// <summary>
    /// Information about a player's connection.
    /// </summary>
    public record PlayerConnection(Guid PlayerId, Guid? GameId, DateTime ConnectedAt);

    /// <summary>
    /// Information about a player's disconnection with grace period.
    /// </summary>
    public record PlayerDisconnection(Guid PlayerId, Guid GameId, DateTime DisconnectedAt);

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

        // Clear any pending disconnection since player reconnected
        _pendingDisconnections.TryRemove(playerId, out _);
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
    /// Updates the game ID for all connections of a specific player.
    /// </summary>
    /// <param name="playerId">The player ID</param>
    /// <param name="gameId">The new game ID</param>
    public void UpdateGameId(Guid playerId, Guid gameId)
    {
        var playerConnections = GetPlayerConnections(playerId);
        foreach (var connectionId in playerConnections)
        {
            UpdateConnectionGame(connectionId, gameId);
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
    /// Adds a player to pending disconnections for grace period handling.
    /// </summary>
    /// <param name="playerId">The player ID</param>
    /// <param name="gameId">The game ID</param>
    public void AddPendingDisconnection(Guid playerId, Guid gameId)
    {
        var disconnection = new PlayerDisconnection(playerId, gameId, DateTime.UtcNow);
        _pendingDisconnections.TryAdd(playerId, disconnection);
    }

    /// <summary>
    /// Gets all pending disconnections that have exceeded the grace period.
    /// </summary>
    /// <param name="gracePeriodMinutes">Grace period in minutes</param>
    /// <returns>List of expired disconnections</returns>
    public List<PlayerDisconnection> GetExpiredDisconnections(double gracePeriodMinutes = 2.0)
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-gracePeriodMinutes);
        return _pendingDisconnections.Values
            .Where(d => d.DisconnectedAt < cutoffTime)
            .ToList();
    }

    /// <summary>
    /// Removes a pending disconnection.
    /// </summary>
    /// <param name="playerId">The player ID</param>
    /// <returns>True if the disconnection was removed</returns>
    public bool RemovePendingDisconnection(Guid playerId)
    {
        return _pendingDisconnections.TryRemove(playerId, out _);
    }

    /// <summary>
    /// Checks if a player has a pending disconnection.
    /// </summary>
    /// <param name="playerId">The player ID</param>
    /// <returns>True if player has pending disconnection</returns>
    public bool HasPendingDisconnection(Guid playerId)
    {
        return _pendingDisconnections.ContainsKey(playerId);
    }

    /// <summary>
    /// Statistics about current connections.
    /// </summary>
    public record ConnectionStats(int TotalConnections, int ConnectedPlayers, double AverageConnectionsPerPlayer);
}