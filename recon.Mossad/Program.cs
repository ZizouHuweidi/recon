

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var app = builder.Build();

app.MapPost("/ingest", async (HttpRequest request) =>
{
    // Stub for receiving telemetry
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    Console.WriteLine($"Received data: {body.Length} bytes");
    return Results.Ok();
});

app.MapDefaultEndpoints();
app.Run();
