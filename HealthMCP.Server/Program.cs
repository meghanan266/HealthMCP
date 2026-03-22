using HealthMCP.Server.Data;

DatabaseSeeder.Initialize();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

app.MapMcp("/mcp");

app.MapGet("/health", () =>
    Results.Json(new
    {
        status = "healthy",
        server = "HealthMCP",
        version = "1.0.0",
        tools = 12,
        transports = new[] { "HTTP/SSE", "stdio" },
        timestamp = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture)
    }));

app.Run();
