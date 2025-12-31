using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Meuhte.Server.Game;
using Xunit;

namespace Meuhte.Server.Tests;

public class GameHubTestFixture : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    public HttpClient HttpClient { get; private set; } = null!;
    public string HubUrl => "http://localhost/hub/game";

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove existing GameState registration and add fresh one for test isolation
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(GameState));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }
                    services.AddSingleton<GameState>();
                });
            });

        HttpClient = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        HttpClient?.Dispose();
        await _factory.DisposeAsync();
    }

    public HubConnection CreateHubConnection()
    {
        return new HubConnectionBuilder()
            .WithUrl(HubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();
    }

    public async Task<HubConnection> CreateConnectedClient()
    {
        var connection = CreateHubConnection();
        await connection.StartAsync();
        return connection;
    }
}

[CollectionDefinition("GameHub")]
public class GameHubCollection : ICollectionFixture<GameHubTestFixture>
{
}
