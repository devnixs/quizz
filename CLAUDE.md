# CLAUDE.md

This file provides guidance for Claude Code when working with this repository.

## Project Overview

Meuhte is a real-time multiplayer party game where players submit answers and reveal them simultaneously. Built with an Angular 17 frontend and ASP.NET Core 8 backend using SignalR for WebSocket communication.

## Architecture

- **Frontend** (`client/`): Angular 17 standalone component app with SignalR client
- **Backend** (`server/`): ASP.NET Core 8 minimal API with SignalR hub
- **Game Logic** (`server/Game/`): `GameState.cs` manages in-memory state, `GameHub.cs` handles SignalR events

### Key Files

| File | Purpose |
|------|---------|
| `server/Program.cs` | App bootstrap, CORS config, route mapping |
| `server/Game/GameState.cs` | Thread-safe game state with player/answer management |
| `server/Game/GameHub.cs` | SignalR hub methods (Join, SubmitAnswer, ResetGame, etc.) |
| `client/src/app/app.component.ts` | Main UI component with SignalR connection logic |

## Development Commands

### Backend (ASP.NET Core)
```bash
dotnet run --project server/Meuhte.Server.csproj    # Start server on http://localhost:5000
curl http://localhost:5000/health                   # Health check
```

### Frontend (Angular)
```bash
cd client && npm install   # Install dependencies
cd client && npm start     # Dev server on http://localhost:4200
cd client && npm run build # Production build
```

## Game Rules

1. First player to join becomes the admin
2. Players submit answers (hidden until everyone submits)
3. Once all players answer, answers are revealed to everyone
4. Admin can reset the game and rename players
5. Maximum 10 players per room

## Coding Conventions

### TypeScript/Angular
- 2-space indentation
- `camelCase` for variables/functions
- `PascalCase` for components/types/interfaces

### C#
- 4-space indentation
- `PascalCase` for types/methods/properties
- `camelCase` for local variables
- Use `lock` for thread-safe state mutations

## Docker

Build and run the full stack in a single container:
```bash
docker build -t meuhte:latest .               # Build image
docker run -p 8080:8080 meuhte:latest         # Run on http://localhost:8080
```

The container packages both Angular (static files) and ASP.NET Core (serves files + SignalR hub).

## Configuration

For local development, the Angular app connects to `/hub/game` (same origin). For separate frontend/backend development, override by setting `window.MEUHTE_HUB_URL` in `index.html` before app bootstrap (e.g., `http://localhost:5000/hub/game`).

CORS is configured for localhost development on ports 4200 and 5173.

## SignalR API

### Client-to-Server Methods
- `Join(name: string)` - Join the game
- `SubmitAnswer(answer: string)` - Submit an answer
- `UpdatePlayerName(playerId: string, name: string)` - Admin: rename a player
- `ResetGame()` - Admin: clear all answers for next round
- `GetState()` - Fetch current game state

### Server-to-Client Events
- `StateUpdated(GameStateDto)` - Broadcasts full state on every change

## Testing

No test framework is configured yet. If adding tests:
- Angular: `*.spec.ts` files with Karma/Jasmine
- .NET: Separate `*.Tests` project
