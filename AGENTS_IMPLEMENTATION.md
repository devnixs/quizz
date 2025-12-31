# Implementation Notes

## What I Built
- Created an Angular front end in `client/` with a single-page game UI that supports username entry, answer submission, live player list, admin controls, and answer reveal.
- Implemented an ASP.NET Core backend in `server/` using SignalR websockets to broadcast real-time game state.
- Added a fun, mobile-friendly UI that fits up to 10 players on a phone screen.

## How It Works
- The first player to join becomes the admin. The admin can reset once all players have answered and can rename other players.
- The server maintains game state in-memory (`server/Game/GameState.cs`) and pushes snapshots to all clients on every update.
- Answers are only revealed to clients after every player has submitted an answer.

## Files Added
- Backend: `server/Program.cs`, `server/Game/GameHub.cs`, `server/Game/GameState.cs`, `server/Meuhte.Server.csproj`.
- Frontend: Angular workspace under `client/` with `app.component.*` and basic configuration.

## Run Locally
1. Start backend: `dotnet run --project server/Meuhte.Server.csproj`.
2. Start frontend: `cd client && npm install && npm start`.
3. Open `http://localhost:4200` and join from multiple devices/tabs.

## Notes
- The client defaults to `http://localhost:5000/hub/game` for SignalR; override via `window.MEUHTE_HUB_URL` if needed.
- There are no automated tests yet; add them as the project grows.
- Added `import 'zone.js';` in `client/src/main.ts` to fix Angular bootstrap error (NG0908) that prevented the UI from rendering.
- Added Enter-key handlers in `client/src/app/app.component.html` so username and answer inputs submit without clicking the buttons.
- Updated admin rename controls so the Save button only appears/enables when a name actually changes, and it resets after a successful save.
- Refined the rename flow to clear edit buffers, force a fresh state fetch after saving, and only show the Save button when an actual edit exists.
- Expanded rename handling to pass both player id and current name to the hub, with a server-side fallback lookup by name if the id is missing.
- Split the main Angular template into standalone components under `client/src/app/components/` (hero, join card, answer card, players list, error banner) and shared DTO types via `client/src/app/models.ts`.
