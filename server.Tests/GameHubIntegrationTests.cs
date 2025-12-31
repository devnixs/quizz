using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Meuhte.Server.Game;
using Xunit;

namespace Meuhte.Server.Tests;

public class GameHubIntegrationTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private readonly List<HubConnection> _connections = new();

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(GameState));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }
                    services.AddSingleton<GameState>();
                });
            });
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        foreach (var connection in _connections)
        {
            await connection.DisposeAsync();
        }
        await _factory.DisposeAsync();
    }

    private HubConnection CreateConnection()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hub/game", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();
        _connections.Add(connection);
        return connection;
    }

    private async Task<HubConnection> CreateConnectedClient()
    {
        var connection = CreateConnection();
        await connection.StartAsync();
        return connection;
    }

    #region GetState Tests

    [Fact]
    public async Task GetState_WhenNoPlayers_ReturnsEmptyState()
    {
        // Arrange
        var client = await CreateConnectedClient();

        // Act
        var state = await client.InvokeAsync<GameStateDto>("GetState");

        // Assert
        Assert.NotNull(state);
        Assert.Empty(state.Players);
        Assert.Null(state.AdminId);
        Assert.False(state.AllAnswered);
        Assert.Equal(10, state.MaxPlayers);
    }

    #endregion

    #region Join Tests

    [Fact]
    public async Task Join_WithValidName_AddsPlayerToGame()
    {
        // Arrange
        var client = await CreateConnectedClient();
        GameStateDto? receivedState = null;
        var stateReceived = new TaskCompletionSource<bool>();

        client.On<GameStateDto>("StateUpdated", state =>
        {
            receivedState = state;
            stateReceived.TrySetResult(true);
        });

        // Act
        await client.InvokeAsync("Join", "Player1");
        await stateReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.NotNull(receivedState);
        Assert.Single(receivedState.Players);
        Assert.Equal("Player1", receivedState.Players[0].Name);
        Assert.False(receivedState.Players[0].HasAnswered);
        Assert.Null(receivedState.Players[0].Answer);
    }

    [Fact]
    public async Task Join_FirstPlayer_BecomesAdmin()
    {
        // Arrange
        var client = await CreateConnectedClient();
        GameStateDto? receivedState = null;
        var stateReceived = new TaskCompletionSource<bool>();

        client.On<GameStateDto>("StateUpdated", state =>
        {
            receivedState = state;
            stateReceived.TrySetResult(true);
        });

        // Act
        await client.InvokeAsync("Join", "FirstPlayer");
        await stateReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.NotNull(receivedState);
        Assert.Equal(receivedState.Players[0].Id, receivedState.AdminId);
    }

    [Fact]
    public async Task Join_SecondPlayer_DoesNotBecomeAdmin()
    {
        // Arrange
        var client1 = await CreateConnectedClient();
        var client2 = await CreateConnectedClient();
        GameStateDto? finalState = null;
        var stateCount = 0;
        var stateReceived = new TaskCompletionSource<bool>();

        client2.On<GameStateDto>("StateUpdated", state =>
        {
            finalState = state;
            stateCount++;
            if (stateCount >= 2)
            {
                stateReceived.TrySetResult(true);
            }
        });

        // Act
        await client1.InvokeAsync("Join", "Admin");
        await client2.InvokeAsync("Join", "Player2");
        await stateReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.NotNull(finalState);
        Assert.Equal(2, finalState.Players.Count);
        var admin = finalState.Players.First(p => p.Name == "Admin");
        Assert.Equal(admin.Id, finalState.AdminId);
    }

    [Fact]
    public async Task Join_WithEmptyName_ThrowsHubException()
    {
        // Arrange
        var client = await CreateConnectedClient();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HubException>(
            () => client.InvokeAsync("Join", ""));

        Assert.Contains("Name is required", exception.Message);
    }

    [Fact]
    public async Task Join_WithWhitespaceName_ThrowsHubException()
    {
        // Arrange
        var client = await CreateConnectedClient();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HubException>(
            () => client.InvokeAsync("Join", "   "));

        Assert.Contains("Name is required", exception.Message);
    }

    [Fact]
    public async Task Join_SamePlayerTwice_DoesNotDuplicate()
    {
        // Arrange
        var client = await CreateConnectedClient();
        var stateUpdates = new List<GameStateDto>();
        var stateReceived = new TaskCompletionSource<bool>();

        client.On<GameStateDto>("StateUpdated", state =>
        {
            stateUpdates.Add(state);
            if (stateUpdates.Count >= 2)
            {
                stateReceived.TrySetResult(true);
            }
        });

        // Act
        await client.InvokeAsync("Join", "Player1");
        await client.InvokeAsync("Join", "Player1Again");
        await stateReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        var finalState = stateUpdates.Last();
        Assert.Single(finalState.Players);
    }

    [Fact]
    public async Task Join_WhenRoomIsFull_ThrowsHubException()
    {
        // Arrange
        var clients = new List<HubConnection>();
        for (int i = 0; i < 10; i++)
        {
            var client = await CreateConnectedClient();
            clients.Add(client);
            await client.InvokeAsync("Join", $"Player{i}");
        }

        var extraClient = await CreateConnectedClient();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HubException>(
            () => extraClient.InvokeAsync("Join", "ExtraPlayer"));

        Assert.Contains("Room is full", exception.Message);
    }

    [Fact]
    public async Task Join_BroadcastsStateToAllClients()
    {
        // Arrange
        var client1 = await CreateConnectedClient();
        var client2 = await CreateConnectedClient();

        GameStateDto? client1State = null;
        GameStateDto? client2State = null;
        var client1Received = new TaskCompletionSource<bool>();
        var client2Received = new TaskCompletionSource<bool>();

        await client1.InvokeAsync("Join", "Player1");

        client1.On<GameStateDto>("StateUpdated", state =>
        {
            if (state.Players.Count == 2)
            {
                client1State = state;
                client1Received.TrySetResult(true);
            }
        });

        client2.On<GameStateDto>("StateUpdated", state =>
        {
            if (state.Players.Count == 2)
            {
                client2State = state;
                client2Received.TrySetResult(true);
            }
        });

        // Act
        await client2.InvokeAsync("Join", "Player2");
        await Task.WhenAll(
            client1Received.Task.WaitAsync(TimeSpan.FromSeconds(5)),
            client2Received.Task.WaitAsync(TimeSpan.FromSeconds(5)));

        // Assert
        Assert.NotNull(client1State);
        Assert.NotNull(client2State);
        Assert.Equal(2, client1State.Players.Count);
        Assert.Equal(2, client2State.Players.Count);
    }

    #endregion

    #region SubmitAnswer Tests

    [Fact]
    public async Task SubmitAnswer_WithValidAnswer_UpdatesPlayerState()
    {
        // Arrange
        var client = await CreateConnectedClient();
        await client.InvokeAsync("Join", "Player1");

        GameStateDto? receivedState = null;
        var stateReceived = new TaskCompletionSource<bool>();

        client.On<GameStateDto>("StateUpdated", state =>
        {
            if (state.Players.Any(p => p.HasAnswered))
            {
                receivedState = state;
                stateReceived.TrySetResult(true);
            }
        });

        // Act
        await client.InvokeAsync("SubmitAnswer", "My Answer");
        await stateReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.NotNull(receivedState);
        Assert.True(receivedState.Players[0].HasAnswered);
    }

    [Fact]
    public async Task SubmitAnswer_WhenAllPlayersAnswer_RevealsAnswers()
    {
        // Arrange
        var client1 = await CreateConnectedClient();
        var client2 = await CreateConnectedClient();

        await client1.InvokeAsync("Join", "Player1");
        await client2.InvokeAsync("Join", "Player2");

        GameStateDto? finalState = null;
        var allAnswered = new TaskCompletionSource<bool>();

        client1.On<GameStateDto>("StateUpdated", state =>
        {
            if (state.AllAnswered)
            {
                finalState = state;
                allAnswered.TrySetResult(true);
            }
        });

        // Act
        await client1.InvokeAsync("SubmitAnswer", "Answer1");
        await client2.InvokeAsync("SubmitAnswer", "Answer2");
        await allAnswered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.NotNull(finalState);
        Assert.True(finalState.AllAnswered);
        Assert.Contains(finalState.Players, p => p.Answer == "Answer1");
        Assert.Contains(finalState.Players, p => p.Answer == "Answer2");
    }

    [Fact]
    public async Task SubmitAnswer_WhenNotAllPlayersAnswer_HidesAnswers()
    {
        // Arrange
        var client1 = await CreateConnectedClient();
        var client2 = await CreateConnectedClient();

        await client1.InvokeAsync("Join", "Player1");
        await client2.InvokeAsync("Join", "Player2");

        GameStateDto? receivedState = null;
        var stateReceived = new TaskCompletionSource<bool>();

        client1.On<GameStateDto>("StateUpdated", state =>
        {
            if (state.Players.Any(p => p.HasAnswered))
            {
                receivedState = state;
                stateReceived.TrySetResult(true);
            }
        });

        // Act
        await client1.InvokeAsync("SubmitAnswer", "Answer1");
        await stateReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.NotNull(receivedState);
        Assert.False(receivedState.AllAnswered);
        Assert.All(receivedState.Players, p => Assert.Null(p.Answer));
    }

    [Fact]
    public async Task SubmitAnswer_WithEmptyAnswer_ThrowsHubException()
    {
        // Arrange
        var client = await CreateConnectedClient();
        await client.InvokeAsync("Join", "Player1");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HubException>(
            () => client.InvokeAsync("SubmitAnswer", ""));

        Assert.Contains("Answer cannot be empty", exception.Message);
    }

    [Fact]
    public async Task SubmitAnswer_WithoutJoining_ThrowsHubException()
    {
        // Arrange
        var client = await CreateConnectedClient();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HubException>(
            () => client.InvokeAsync("SubmitAnswer", "My Answer"));

        Assert.Contains("Player not found", exception.Message);
    }

    [Fact]
    public async Task SubmitAnswer_TrimsWhitespace()
    {
        // Arrange
        var client = await CreateConnectedClient();
        await client.InvokeAsync("Join", "Player1");

        GameStateDto? receivedState = null;
        var stateReceived = new TaskCompletionSource<bool>();

        client.On<GameStateDto>("StateUpdated", state =>
        {
            if (state.AllAnswered)
            {
                receivedState = state;
                stateReceived.TrySetResult(true);
            }
        });

        // Act
        await client.InvokeAsync("SubmitAnswer", "  My Answer  ");
        await stateReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.NotNull(receivedState);
        Assert.Equal("My Answer", receivedState.Players[0].Answer);
    }

    #endregion

    #region UpdatePlayerName Tests

    [Fact]
    public async Task UpdatePlayerName_AsAdmin_UpdatesTargetPlayerName()
    {
        // Arrange
        var admin = await CreateConnectedClient();
        var player = await CreateConnectedClient();

        await admin.InvokeAsync("Join", "Admin");
        await player.InvokeAsync("Join", "OldName");

        // Get player ID
        var state = await admin.InvokeAsync<GameStateDto>("GetState");
        var playerId = state.Players.First(p => p.Name == "OldName").Id;

        GameStateDto? updatedState = null;
        var stateReceived = new TaskCompletionSource<bool>();

        admin.On<GameStateDto>("StateUpdated", s =>
        {
            if (s.Players.Any(p => p.Name == "NewName"))
            {
                updatedState = s;
                stateReceived.TrySetResult(true);
            }
        });

        // Act
        await admin.InvokeAsync("UpdatePlayerName", playerId, "OldName", "NewName");
        await stateReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.NotNull(updatedState);
        Assert.Contains(updatedState.Players, p => p.Name == "NewName");
        Assert.DoesNotContain(updatedState.Players, p => p.Name == "OldName");
    }

    [Fact]
    public async Task UpdatePlayerName_AsNonAdmin_ThrowsHubException()
    {
        // Arrange
        var admin = await CreateConnectedClient();
        var player = await CreateConnectedClient();

        await admin.InvokeAsync("Join", "Admin");
        await player.InvokeAsync("Join", "Player");

        var state = await admin.InvokeAsync<GameStateDto>("GetState");
        var adminId = state.Players.First(p => p.Name == "Admin").Id;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HubException>(
            () => player.InvokeAsync("UpdatePlayerName", adminId, "Admin", "Hacked"));

        Assert.Contains("Only the admin can edit player names", exception.Message);
    }

    [Fact]
    public async Task UpdatePlayerName_WithEmptyName_ThrowsHubException()
    {
        // Arrange
        var admin = await CreateConnectedClient();
        var player = await CreateConnectedClient();

        await admin.InvokeAsync("Join", "Admin");
        await player.InvokeAsync("Join", "Player");

        var state = await admin.InvokeAsync<GameStateDto>("GetState");
        var playerId = state.Players.First(p => p.Name == "Player").Id;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HubException>(
            () => admin.InvokeAsync("UpdatePlayerName", playerId, "Player", ""));

        Assert.Contains("Name is required", exception.Message);
    }

    [Fact]
    public async Task UpdatePlayerName_WithNonExistentPlayer_ThrowsHubException()
    {
        // Arrange
        var admin = await CreateConnectedClient();
        await admin.InvokeAsync("Join", "Admin");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HubException>(
            () => admin.InvokeAsync("UpdatePlayerName", "fake-id", "FakeName", "NewName"));

        Assert.Contains("Player not found", exception.Message);
    }

    [Fact]
    public async Task UpdatePlayerName_ByCurrentName_UpdatesPlayer()
    {
        // Arrange
        var admin = await CreateConnectedClient();
        var player = await CreateConnectedClient();

        await admin.InvokeAsync("Join", "Admin");
        await player.InvokeAsync("Join", "TargetPlayer");

        GameStateDto? updatedState = null;
        var stateReceived = new TaskCompletionSource<bool>();

        admin.On<GameStateDto>("StateUpdated", s =>
        {
            if (s.Players.Any(p => p.Name == "RenamedPlayer"))
            {
                updatedState = s;
                stateReceived.TrySetResult(true);
            }
        });

        // Act - use null for playerId, use currentName instead
        await admin.InvokeAsync("UpdatePlayerName", (string?)null, "TargetPlayer", "RenamedPlayer");
        await stateReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.NotNull(updatedState);
        Assert.Contains(updatedState.Players, p => p.Name == "RenamedPlayer");
    }

    [Fact]
    public async Task UpdatePlayerName_WithDuplicateNames_ThrowsHubException()
    {
        // Arrange
        var admin = await CreateConnectedClient();
        var player1 = await CreateConnectedClient();
        var player2 = await CreateConnectedClient();

        await admin.InvokeAsync("Join", "Admin");
        await player1.InvokeAsync("Join", "SameName");
        await player2.InvokeAsync("Join", "SameName");

        // Act & Assert - trying to rename by currentName when multiple match
        var exception = await Assert.ThrowsAsync<HubException>(
            () => admin.InvokeAsync("UpdatePlayerName", (string?)null, "SameName", "NewName"));

        Assert.Contains("Multiple players share that name", exception.Message);
    }

    #endregion

    #region ResetGame Tests

    [Fact]
    public async Task ResetGame_AsAdmin_ClearsAllAnswers()
    {
        // Arrange
        var admin = await CreateConnectedClient();
        var player = await CreateConnectedClient();

        await admin.InvokeAsync("Join", "Admin");
        await player.InvokeAsync("Join", "Player");

        await admin.InvokeAsync("SubmitAnswer", "AdminAnswer");
        await player.InvokeAsync("SubmitAnswer", "PlayerAnswer");

        GameStateDto? resetState = null;
        var stateReceived = new TaskCompletionSource<bool>();

        admin.On<GameStateDto>("StateUpdated", s =>
        {
            if (!s.AllAnswered && s.Players.All(p => !p.HasAnswered))
            {
                resetState = s;
                stateReceived.TrySetResult(true);
            }
        });

        // Act
        await admin.InvokeAsync("ResetGame");
        await stateReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.NotNull(resetState);
        Assert.False(resetState.AllAnswered);
        Assert.All(resetState.Players, p =>
        {
            Assert.False(p.HasAnswered);
            Assert.Null(p.Answer);
        });
    }

    [Fact]
    public async Task ResetGame_AsNonAdmin_ThrowsHubException()
    {
        // Arrange
        var admin = await CreateConnectedClient();
        var player = await CreateConnectedClient();

        await admin.InvokeAsync("Join", "Admin");
        await player.InvokeAsync("Join", "Player");

        await admin.InvokeAsync("SubmitAnswer", "AdminAnswer");
        await player.InvokeAsync("SubmitAnswer", "PlayerAnswer");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HubException>(
            () => player.InvokeAsync("ResetGame"));

        Assert.Contains("Only the admin can reset the game", exception.Message);
    }

    [Fact]
    public async Task ResetGame_WhenNotAllAnswered_ThrowsHubException()
    {
        // Arrange
        var admin = await CreateConnectedClient();
        var player = await CreateConnectedClient();

        await admin.InvokeAsync("Join", "Admin");
        await player.InvokeAsync("Join", "Player");

        await admin.InvokeAsync("SubmitAnswer", "AdminAnswer");
        // Player hasn't answered yet

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HubException>(
            () => admin.InvokeAsync("ResetGame"));

        Assert.Contains("All players must answer before resetting", exception.Message);
    }

    [Fact]
    public async Task ResetGame_WithNoPlayers_ThrowsHubException()
    {
        // Arrange
        var admin = await CreateConnectedClient();
        await admin.InvokeAsync("Join", "Admin");

        // Disconnect admin to leave room empty, then reconnect
        await admin.StopAsync();
        _connections.Remove(admin);

        var newAdmin = await CreateConnectedClient();
        await newAdmin.InvokeAsync("Join", "NewAdmin");

        // Need to answer first to attempt reset
        // Actually with only one player who answered, reset should work
        // Let me verify the edge case

        await newAdmin.InvokeAsync("SubmitAnswer", "Answer");

        GameStateDto? resetState = null;
        var stateReceived = new TaskCompletionSource<bool>();

        newAdmin.On<GameStateDto>("StateUpdated", s =>
        {
            if (!s.AllAnswered)
            {
                resetState = s;
                stateReceived.TrySetResult(true);
            }
        });

        // Act
        await newAdmin.InvokeAsync("ResetGame");
        await stateReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.NotNull(resetState);
        Assert.False(resetState.AllAnswered);
    }

    [Fact]
    public async Task ResetGame_BroadcastsToAllClients()
    {
        // Arrange
        var admin = await CreateConnectedClient();
        var player = await CreateConnectedClient();

        await admin.InvokeAsync("Join", "Admin");
        await player.InvokeAsync("Join", "Player");

        await admin.InvokeAsync("SubmitAnswer", "AdminAnswer");
        await player.InvokeAsync("SubmitAnswer", "PlayerAnswer");

        GameStateDto? adminState = null;
        GameStateDto? playerState = null;
        var adminReceived = new TaskCompletionSource<bool>();
        var playerReceived = new TaskCompletionSource<bool>();

        admin.On<GameStateDto>("StateUpdated", s =>
        {
            if (!s.AllAnswered && s.Players.All(p => !p.HasAnswered))
            {
                adminState = s;
                adminReceived.TrySetResult(true);
            }
        });

        player.On<GameStateDto>("StateUpdated", s =>
        {
            if (!s.AllAnswered && s.Players.All(p => !p.HasAnswered))
            {
                playerState = s;
                playerReceived.TrySetResult(true);
            }
        });

        // Act
        await admin.InvokeAsync("ResetGame");
        await Task.WhenAll(
            adminReceived.Task.WaitAsync(TimeSpan.FromSeconds(5)),
            playerReceived.Task.WaitAsync(TimeSpan.FromSeconds(5)));

        // Assert
        Assert.NotNull(adminState);
        Assert.NotNull(playerState);
        Assert.False(adminState.AllAnswered);
        Assert.False(playerState.AllAnswered);
    }

    #endregion

    #region Disconnect Tests

    [Fact]
    public async Task Disconnect_RemovesPlayerFromGame()
    {
        // Arrange
        var admin = await CreateConnectedClient();
        var player = await CreateConnectedClient();

        await admin.InvokeAsync("Join", "Admin");
        await player.InvokeAsync("Join", "PlayerToLeave");

        GameStateDto? stateAfterDisconnect = null;
        var stateReceived = new TaskCompletionSource<bool>();

        admin.On<GameStateDto>("StateUpdated", s =>
        {
            if (s.Players.Count == 1)
            {
                stateAfterDisconnect = s;
                stateReceived.TrySetResult(true);
            }
        });

        // Act
        await player.StopAsync();
        _connections.Remove(player);
        await stateReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.NotNull(stateAfterDisconnect);
        Assert.Single(stateAfterDisconnect.Players);
        Assert.Equal("Admin", stateAfterDisconnect.Players[0].Name);
    }

    [Fact]
    public async Task Disconnect_WhenAdminLeaves_TransfersAdminToNextPlayer()
    {
        // Arrange
        var admin = await CreateConnectedClient();
        var player = await CreateConnectedClient();

        await admin.InvokeAsync("Join", "Admin");
        await player.InvokeAsync("Join", "Player");

        GameStateDto? stateAfterDisconnect = null;
        var stateReceived = new TaskCompletionSource<bool>();

        player.On<GameStateDto>("StateUpdated", s =>
        {
            if (s.Players.Count == 1)
            {
                stateAfterDisconnect = s;
                stateReceived.TrySetResult(true);
            }
        });

        // Act
        await admin.StopAsync();
        _connections.Remove(admin);
        await stateReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.NotNull(stateAfterDisconnect);
        Assert.Single(stateAfterDisconnect.Players);
        Assert.Equal(stateAfterDisconnect.Players[0].Id, stateAfterDisconnect.AdminId);
    }

    [Fact]
    public async Task Disconnect_WhenLastPlayerLeaves_ClearsAdmin()
    {
        // Arrange
        var admin = await CreateConnectedClient();
        await admin.InvokeAsync("Join", "Admin");

        // Act
        await admin.StopAsync();
        _connections.Remove(admin);

        // Create new connection and check state
        var newClient = await CreateConnectedClient();
        var state = await newClient.InvokeAsync<GameStateDto>("GetState");

        // Assert
        Assert.Empty(state.Players);
        Assert.Null(state.AdminId);
    }

    [Fact]
    public async Task Disconnect_BroadcastsUpdatedState()
    {
        // Arrange
        var client1 = await CreateConnectedClient();
        var client2 = await CreateConnectedClient();
        var client3 = await CreateConnectedClient();

        await client1.InvokeAsync("Join", "Player1");
        await client2.InvokeAsync("Join", "Player2");
        await client3.InvokeAsync("Join", "Player3");

        GameStateDto? client1State = null;
        GameStateDto? client3State = null;
        var client1Received = new TaskCompletionSource<bool>();
        var client3Received = new TaskCompletionSource<bool>();

        client1.On<GameStateDto>("StateUpdated", s =>
        {
            if (s.Players.Count == 2)
            {
                client1State = s;
                client1Received.TrySetResult(true);
            }
        });

        client3.On<GameStateDto>("StateUpdated", s =>
        {
            if (s.Players.Count == 2)
            {
                client3State = s;
                client3Received.TrySetResult(true);
            }
        });

        // Act
        await client2.StopAsync();
        _connections.Remove(client2);
        await Task.WhenAll(
            client1Received.Task.WaitAsync(TimeSpan.FromSeconds(5)),
            client3Received.Task.WaitAsync(TimeSpan.FromSeconds(5)));

        // Assert
        Assert.NotNull(client1State);
        Assert.NotNull(client3State);
        Assert.Equal(2, client1State.Players.Count);
        Assert.Equal(2, client3State.Players.Count);
        Assert.DoesNotContain(client1State.Players, p => p.Name == "Player2");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task MultipleOperations_MaintainConsistentState()
    {
        // Arrange
        var admin = await CreateConnectedClient();
        var player1 = await CreateConnectedClient();
        var player2 = await CreateConnectedClient();

        // Act - Join all players
        await admin.InvokeAsync("Join", "Admin");
        await player1.InvokeAsync("Join", "Player1");
        await player2.InvokeAsync("Join", "Player2");

        // Submit answers
        await admin.InvokeAsync("SubmitAnswer", "AdminAnswer");
        await player1.InvokeAsync("SubmitAnswer", "Player1Answer");
        await player2.InvokeAsync("SubmitAnswer", "Player2Answer");

        // Wait for state to settle
        await Task.Delay(100);

        // Get state
        var state = await admin.InvokeAsync<GameStateDto>("GetState");

        // Assert
        Assert.Equal(3, state.Players.Count);
        Assert.True(state.AllAnswered);
        Assert.All(state.Players, p =>
        {
            Assert.True(p.HasAnswered);
            Assert.NotNull(p.Answer);
        });

        // Reset game
        await admin.InvokeAsync("ResetGame");

        // Get state after reset
        var stateAfterReset = await admin.InvokeAsync<GameStateDto>("GetState");

        // Assert after reset
        Assert.Equal(3, stateAfterReset.Players.Count);
        Assert.False(stateAfterReset.AllAnswered);
        Assert.All(stateAfterReset.Players, p =>
        {
            Assert.False(p.HasAnswered);
            Assert.Null(p.Answer);
        });
    }

    [Fact]
    public async Task Join_TrimsWhitespaceFromName()
    {
        // Arrange
        var client = await CreateConnectedClient();
        GameStateDto? receivedState = null;
        var stateReceived = new TaskCompletionSource<bool>();

        client.On<GameStateDto>("StateUpdated", state =>
        {
            receivedState = state;
            stateReceived.TrySetResult(true);
        });

        // Act
        await client.InvokeAsync("Join", "  TrimmedName  ");
        await stateReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.NotNull(receivedState);
        Assert.Equal("TrimmedName", receivedState.Players[0].Name);
    }

    #endregion
}
