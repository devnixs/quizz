using Meuhte.Server.Game;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("dev", policy =>
    {
        policy.WithOrigins(
                "http://localhost:4200",
                "http://127.0.0.1:4200",
                "http://localhost:5173",
                "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddSignalR();
builder.Services.AddSingleton<GameState>();

var app = builder.Build();

app.UseRouting();
app.UseCors("dev");

// Serve static files from wwwroot (Angular build output)
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHub<GameHub>("/hub/game");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// SPA fallback: serve index.html for non-API routes
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program { }
