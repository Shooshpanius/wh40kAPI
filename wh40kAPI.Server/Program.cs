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

// KT BSData separate database
string? ktBsDataConnectionString;
if (builder.Environment.IsProduction())
{
    ktBsDataConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__KtBsDataConnection")
        ?? builder.Configuration.GetConnectionString("KtBsDataConnection");
}
else
{
    ktBsDataConnectionString = builder.Configuration.GetConnectionString("KtBsDataConnection");
}

if (string.IsNullOrEmpty(ktBsDataConnectionString))
    throw new InvalidOperationException("Connection string 'KtBsDataConnection' not found.");

var ktBsDataServerVersion = ServerVersion.AutoDetect(ktBsDataConnectionString);
builder.Services.AddDbContext<KtBsDataDbContext>(options =>
    options.UseMySql(ktBsDataConnectionString, ktBsDataServerVersion));

builder.Services.AddScoped<DataImportService>();
builder.Services.AddScoped<BsDataImportService>();
builder.Services.AddScoped<KtBsDataImportService>();
builder.Services.AddHttpClient("github", client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("wh40kAPI/1.0");
    client.Timeout = TimeSpan.FromMinutes(10);
});
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);
builder.Services.AddOpenApi("wh40k", options =>
{
    options.ShouldInclude = (desc) => desc.GroupName == "wh40k";
});
builder.Services.AddOpenApi("bsdata", options =>
{
    options.ShouldInclude = (desc) => desc.GroupName == "bsdata";
});
builder.Services.AddOpenApi("ktbsdata", options =>
{
    options.ShouldInclude = (desc) => desc.GroupName == "ktbsdata";
});

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    var bsDb = scope.ServiceProvider.GetRequiredService<BsDataDbContext>();
    bsDb.Database.EnsureCreated();

    var ktBsDb = scope.ServiceProvider.GetRequiredService<KtBsDataDbContext>();
    ktBsDb.Database.EnsureCreated();
}

app.UseDefaultFiles();
app.MapStaticAssets();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
    app.MapOpenApi();
    app.MapScalarApiReference("/scalar/wh40k", options => options.AddDocument("wh40k", "WH40K API"));
    app.MapScalarApiReference("/scalar/bsdata", options => options.AddDocument("bsdata", "BSData 40k"));
    app.MapScalarApiReference("/scalar/ktbsdata", options => options.AddDocument("ktbsdata", "BSData Kill Team"));
//}

app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("/index.html");

app.Run();
