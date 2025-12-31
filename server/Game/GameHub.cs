using Microsoft.AspNetCore.SignalR;

namespace Meuhte.Server.Game;

public sealed class GameHub : Hub
{
    private readonly GameState _state;

    public GameHub(GameState state)
    {
        _state = state;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _state.RemovePlayer(Context.ConnectionId);
        await BroadcastState();
        await base.OnDisconnectedAsync(exception);
    }

    public Task<GameStateDto> GetState()
    {
        return Task.FromResult(_state.GetSnapshot());
    }

    public async Task Join(string name)
    {
        if (!_state.TryAddPlayer(Context.ConnectionId, name, out var error))
        {
            throw new HubException(error ?? "Unable to join.");
        }

        await BroadcastState();
    }

    public async Task SubmitAnswer(string answer)
    {
        if (!_state.SubmitAnswer(Context.ConnectionId, answer, out var error))
        {
            throw new HubException(error ?? "Unable to submit answer.");
        }

        await BroadcastState();
    }

    public async Task UpdatePlayerName(string? playerId, string? currentName, string name)
    {
        if (!_state.UpdatePlayerName(Context.ConnectionId, playerId, currentName, name, out var error))
        {
            throw new HubException(error ?? "Unable to update player name.");
        }

        await BroadcastState();
    }

    public async Task ResetGame()
    {
        if (!_state.ResetGame(Context.ConnectionId, out var error))
        {
            throw new HubException(error ?? "Unable to reset.");
        }

        await BroadcastState();
    }

    private Task BroadcastState()
    {
        var snapshot = _state.GetSnapshot();
        return Clients.All.SendAsync("StateUpdated", snapshot);
    }
}
