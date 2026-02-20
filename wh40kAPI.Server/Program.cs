using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using wh40kAPI.Server.Data;
using wh40kAPI.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Prefer environment variable for connection string in production
string? connectionString;
if (builder.Environment.IsProduction())
{
    connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? builder.Configuration.GetConnectionString("DefaultConnection");
}
else
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
}

if (string.IsNullOrEmpty(connectionString))
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

var serverVersion = ServerVersion.AutoDetect(connectionString);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));

builder.Services.AddScoped<DataImportService>();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseDefaultFiles();
app.MapStaticAssets();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("/index.html");

app.Run();
