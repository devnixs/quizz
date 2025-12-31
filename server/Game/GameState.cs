namespace Meuhte.Server.Game;

public sealed class GameState
{
    public const int MaxPlayers = 10;

    private readonly object _sync = new();
    private readonly List<Player> _players = new();

    public string? AdminConnectionId { get; private set; }

    public bool TryAddPlayer(string connectionId, string name, out string? error)
    {
        error = null;
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Name is required.";
            return false;
        }

        lock (_sync)
        {
            if (_players.Count >= MaxPlayers)
            {
                error = "Room is full.";
                return false;
            }

            if (_players.Any(p => p.ConnectionId == connectionId))
            {
                return true;
            }

            var player = new Player(connectionId, name);
            _players.Add(player);

            AdminConnectionId ??= connectionId;
        }

        return true;
    }

    public void RemovePlayer(string connectionId)
    {
        lock (_sync)
        {
            var removed = _players.RemoveAll(p => p.ConnectionId == connectionId) > 0;
            if (!removed)
            {
                return;
            }

            if (AdminConnectionId == connectionId)
            {
                AdminConnectionId = _players.FirstOrDefault()?.ConnectionId;
            }
        }
    }

    public bool SubmitAnswer(string connectionId, string answer, out string? error)
    {
        error = null;
        answer = answer.Trim();
        if (string.IsNullOrWhiteSpace(answer))
        {
            error = "Answer cannot be empty.";
            return false;
        }

        lock (_sync)
        {
            var player = _players.FirstOrDefault(p => p.ConnectionId == connectionId);
            if (player is null)
            {
                error = "Player not found.";
                return false;
            }

            player.Answer = answer;
            player.HasAnswered = true;
        }

        return true;
    }

    public bool UpdatePlayerName(string connectionId, string? targetConnectionId, string? currentName, string name, out string? error)
    {
        error = null;
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Name is required.";
            return false;
        }

        lock (_sync)
        {
            if (AdminConnectionId != connectionId)
            {
                error = "Only the admin can edit player names.";
                return false;
            }

            Player? player = null;

            if (!string.IsNullOrWhiteSpace(targetConnectionId))
            {
                player = _players.FirstOrDefault(p => p.ConnectionId == targetConnectionId);
            }

            if (player is null && !string.IsNullOrWhiteSpace(currentName))
            {
                var matches = _players.Where(p => p.Name == currentName).ToList();
                if (matches.Count > 1)
                {
                    error = "Multiple players share that name. Please retry.";
                    return false;
                }

                player = matches.FirstOrDefault();
            }

            if (player is null)
            {
                error = "Player not found.";
                return false;
            }

            player.Name = name;
        }

        return true;
    }

    public bool ResetGame(string connectionId, out string? error)
    {
        error = null;

        lock (_sync)
        {
            if (AdminConnectionId != connectionId)
            {
                error = "Only the admin can reset the game.";
                return false;
            }

            if (_players.Count == 0 || _players.Any(p => !p.HasAnswered))
            {
                error = "All players must answer before resetting.";
                return false;
            }

            foreach (var player in _players)
            {
                player.Answer = null;
                player.HasAnswered = false;
            }
        }

        return true;
    }

    public GameStateDto GetSnapshot()
    {
        lock (_sync)
        {
            var allAnswered = _players.Count > 0 && _players.All(p => p.HasAnswered);
            var players = _players
                .Select(p => new PlayerDto(
                    p.ConnectionId,
                    p.Name,
                    p.HasAnswered,
                    allAnswered ? p.Answer : null))
                .ToList();

            return new GameStateDto(players, AdminConnectionId, allAnswered, MaxPlayers);
        }
    }
}

public sealed class Player
{
    public Player(string connectionId, string name)
    {
        ConnectionId = connectionId;
        Name = name;
    }

    public string ConnectionId { get; }
    public string Name { get; set; }
    public string? Answer { get; set; }
    public bool HasAnswered { get; set; }
}

public sealed record PlayerDto(string Id, string Name, bool HasAnswered, string? Answer);
public sealed record GameStateDto(IReadOnlyList<PlayerDto> Players, string? AdminId, bool AllAnswered, int MaxPlayers);
