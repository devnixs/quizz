# Repository Guidelines

## Project Structure & Module Organization
- `client/`: Angular front-end for the game UI.
- `server/`: ASP.NET Core backend with a SignalR hub.
- `client/src/app/`: main UI component (`app.component.*`).
- `server/Game/`: game state and SignalR hub logic.

## Build, Test, and Development Commands
Front-end (Angular):
- `cd client && npm install` — install dependencies.
- `cd client && npm start` — run Angular dev server at `http://localhost:4200`.
- `cd client && npm run build` — build production assets.

Back-end (ASP.NET Core):
- `dotnet run --project server/Meuhte.Server.csproj` — run the SignalR server on `http://localhost:5000`.
- `curl http://localhost:5000/health` — quick health check.

## Coding Style & Naming Conventions
- TypeScript/Angular: 2-space indentation, `camelCase` for variables/functions, `PascalCase` for components/types.
- C#: 4-space indentation, `PascalCase` for types/methods, `camelCase` for locals.
- No formatter or linter is configured yet; keep changes consistent with existing files.

## Testing Guidelines
No test framework is configured. If you add tests, keep them close to the code (`*.spec.ts` for Angular; `*.Tests` project for .NET) and document how to run them.

## Commit & Pull Request Guidelines
No established commit convention yet. Use clear, imperative messages (e.g., `Add SignalR hub`) or adopt Conventional Commits consistently.

PRs should include a summary, testing notes, and UI screenshots when relevant.

## Configuration Tips
The Angular app connects to `http://localhost:5000/hub/game` by default. You can override this by setting `window.MEUHTE_HUB_URL` before bootstrapping the app (e.g., in `index.html`).
