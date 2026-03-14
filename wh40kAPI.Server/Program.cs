using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Net;
using System.Threading.RateLimiting;
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

// AutoDetect requires an active DB connection at startup; fall back to a safe default
// if the server is temporarily unavailable (e.g., credentials were just changed).
var serverVersion = DetectServerVersion(connectionString);
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

var bsDataServerVersion = DetectServerVersion(bsDataConnectionString);
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

var ktBsDataServerVersion = DetectServerVersion(ktBsDataConnectionString);
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
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Trust only the nginx container on the internal Docker network.
    // Clear the default loopback-only trust so the Docker subnet is accepted.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
});

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("admin", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString();
        // Requests with no IP get their own very restrictive bucket (1/min)
        // to prevent sharing and effectively block unknown sources
        if (ip is null)
        {
            return RateLimitPartition.GetFixedWindowLimiter("no-ip",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 1,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        }
        return RateLimitPartition.GetFixedWindowLimiter(ip,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
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

// Ensure database is created; wrapped in try-catch so the server starts even if the DB
// is temporarily unavailable (e.g., credentials were just changed).
using (var scope = app.Services.CreateScope())
{
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        scope.ServiceProvider.GetRequiredService<BsDataDbContext>().Database.EnsureCreated();
        scope.ServiceProvider.GetRequiredService<KtBsDataDbContext>().Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        startupLogger.LogWarning(ex,
            "Could not connect to one or more databases at startup. " +
            "Database operations will fail until the connection is restored. " +
            "Check your connection strings and database credentials.");
    }
}

app.UseDefaultFiles();
app.MapStaticAssets();

// Must be first — rewrites RemoteIpAddress from X-Forwarded-For before any middleware reads it
app.UseForwardedHeaders();

// Security headers for all responses
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
    await next();
});

// Configure the HTTP request pipeline.
app.MapOpenApi();
app.MapScalarApiReference("/scalar/wh40k", options => options.AddDocument("wh40k", "WH40K API"));
app.MapScalarApiReference("/scalar/bsdata", options => options.AddDocument("bsdata", "BSData 40k"));
app.MapScalarApiReference("/scalar/ktbsdata", options => options.AddDocument("ktbsdata", "BSData Kill Team"));

app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("/index.html");

app.Run();

// AutoDetect opens a live DB connection to query the server version.
// If the DB is unreachable (e.g., credentials were just changed), fall back to a
// safe MariaDB 10.6 default so the server still starts up and can serve requests.
static ServerVersion DetectServerVersion(string cs)
{
    try
    {
        return ServerVersion.AutoDetect(cs);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(
            $"[WARN] Could not auto-detect database server version: {ex.Message}. " +
            "Falling back to MariaDB 10.6. " +
            "Check your connection string and database credentials.");
        return ServerVersion.Parse("10.6.0-mariadb");
    }
}
