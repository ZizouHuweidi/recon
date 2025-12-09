using Microsoft.EntityFrameworkCore;
using recon.Goyim.Data;
using recon.Goyim.Models;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.AddNpgsqlDbContext<ReconDbContext>("recon-db");

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Auto-migration
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ReconDbContext>();
    // Wait for DB to be ready is handled by resilience usually, but migrate might need retries if container starts slow.
    // However, Aspire usually handles connection string availability.
    try
    {
        await context.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        // Log locally or just ignore if it's transient
        Console.WriteLine($"Migration failed: {ex.Message}");
        // In prod we might want to crush, but effectively the health check will fail if DB is down
    }
}

app.MapGet("/", () => "API service is running.");

var users = app.MapGroup("/users");

users.MapGet("/", async (ReconDbContext db) => await db.Users.ToListAsync());

users.MapGet("/{id}", async (int id, ReconDbContext db) =>
    await db.Users.FindAsync(id) is User user ? Results.Ok(user) : Results.NotFound());

users.MapPost("/", async (User user, ReconDbContext db) =>
{
    db.Users.Add(user);
    await db.SaveChangesAsync();
    return Results.Created($"/users/{user.Id}", user);
});

users.MapPut("/{id}", async (int id, User input, ReconDbContext db) =>
{
    var user = await db.Users.FindAsync(id);
    if (user is null) return Results.NotFound();

    user.Name = input.Name;
    user.Email = input.Email;
    user.Role = input.Role;

    await db.SaveChangesAsync();
    return Results.NoContent();
});

users.MapDelete("/{id}", async (int id, ReconDbContext db) =>
{
    if (await db.Users.FindAsync(id) is User user)
    {
        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return Results.Ok(user);
    }

    return Results.NotFound();
});

app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
