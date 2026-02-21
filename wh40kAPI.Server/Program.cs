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

// BSData separate database
string? bsDataConnectionString;
if (builder.Environment.IsProduction())
{
    bsDataConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__BsDataConnection")
        ?? builder.Configuration.GetConnectionString("BsDataConnection");
}
else
{
    bsDataConnectionString = builder.Configuration.GetConnectionString("BsDataConnection");
}

if (string.IsNullOrEmpty(bsDataConnectionString))
    throw new InvalidOperationException("Connection string 'BsDataConnection' not found.");

var bsDataServerVersion = ServerVersion.AutoDetect(bsDataConnectionString);
builder.Services.AddDbContext<BsDataDbContext>(options =>
    options.UseMySql(bsDataConnectionString, bsDataServerVersion));

builder.Services.AddScoped<DataImportService>();
builder.Services.AddScoped<BsDataImportService>();
builder.Services.AddHttpClient("github", client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("wh40kAPI/1.0");
    client.Timeout = TimeSpan.FromMinutes(10);
});
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    var bsDb = scope.ServiceProvider.GetRequiredService<BsDataDbContext>();
    bsDb.Database.EnsureCreated();
}

app.UseDefaultFiles();
app.MapStaticAssets();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
    app.MapOpenApi();
    app.MapScalarApiReference();
//}

app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("/index.html");

app.Run();
