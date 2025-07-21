# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Structure

SeaBattle is a real-time multiplayer Blazor WebAssembly battleship game built with .NET 9.0. The solution follows a 3-tier architecture:

- **Client** (SeaBattle.Client): Blazor WebAssembly frontend
- **Server** (SeaBattle.Server): ASP.NET Core backend with SignalR hub
- **Shared** (SeaBattle.Shared): Common models and game logic

## Build and Run Commands

```bash
# Build the entire solution
dotnet build

# Run the server (serves both API and client)
dotnet run --project Server

# Run with specific profile
dotnet run --project Server --launch-profile https
```

The application runs on:
- HTTPS: https://localhost:7004
- HTTP: http://localhost:5083

## Architecture Overview

### Real-time Communication
- **SignalR Hub**: `Server/Hubs/BattleHub.cs` handles all real-time game communication
- **Hub Interface**: `Shared/Hub/IGameHub.cs` defines client-side SignalR methods
- Players join SignalR groups based on game ID and player ID for targeted messaging

### Game State Management
- **Server State**: `GameState` class manages authoritative game state on server
- **Client State**: `GameStateClient` class provides client-optimized view of game state
- **Global Storage**: `GlobalGameStorage` singleton manages active games and matchmaking
- **Game Service**: `GameService` contains core game logic and state transformations

### Game Flow
1. **Setup Stage**: Players place ships on their 10x10 grid
2. **Game Stage**: Turn-based combat with real-time updates
3. **GameOver Stage**: Win/loss determination and final state display

### Key Components
- **GameBoard**: Main game interface with dual field display (own/enemy)
- **SeaCell**: Individual cell component handling clicks and state visualization
- **FormationComponent**: Ship placement interface during setup
- **PlayerState**: Manages individual player data, fleet, and shot history

### State Synchronization
- Server maintains authoritative game state
- `GameService.CreateGameStateUpdate()` creates complete state snapshots for clients
- SignalR groups ensure only relevant players receive updates
- Client receives both own field state and enemy field state (with shot results only)

## Development Notes

- Game uses a single-dimensional array for field representation (`CellState[]`)
- Coordinate conversion: `index = x * fieldSize + y`
- Ships cannot be placed diagonally adjacent to each other
- Shot results include automatic marking of adjacent cells when ships are destroyed
- Player matching uses a "vacant game" system for quick pairing

## Git Workflow Rules

- **NEVER commit changes automatically** - Only commit when explicitly asked by the user
- Always wait for explicit permission before running `git add` and `git commit`
- After making code changes, inform the user what was changed and wait for commit instructions
- User controls when and what gets committed to maintain proper git workflow