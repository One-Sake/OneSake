using System.Text.Json.Serialization;
using OneSake.Server.Hubs;
using OneSake.Server.Match;
using OneSake.Server.Services;
using Microsoft.EntityFrameworkCore;
using OneSake.Persistence;
using System.Net;

var builder = WebApplication.CreateBuilder(args);
var configuredCorsOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()?
    .Select(NormalizeOrigin)
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .ToHashSet(StringComparer.OrdinalIgnoreCase)
    ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

builder.Services.AddDbContext<OneSakeDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// =========================
// SERVICE CONFIGURATION
// =========================

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddControllers(); // Support Adden fuer Controllers

builder.Services.AddHttpClient<CardService>();
builder.Services.AddScoped<CardImportService>();
builder.Services.AddScoped<CardQueryService>();

// Enables Swagger (API documentation UI)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// SignalR aktivieren fuer Real-Time Kommunikation
builder.Services
    .AddSignalR()
    .AddJsonProtocol(options =>
    {
        // Enums als Strings serializieren anstatt nummern -> statt zb. runnning:1 hat man running:works 
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
// Singleton service um game rooms zu managen
builder.Services.AddSingleton<MatchRoomManager>();
// Loescht leere Rooms automatisch nach 5 Minuten
builder.Services.AddHostedService<RoomCleanupService>();
// Erlaubt frontend (localhost:5173) zu backend zu reden
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy
            .SetIsOriginAllowed(origin =>
            {
                var normalizedOrigin = NormalizeOrigin(origin);
                if (string.IsNullOrWhiteSpace(normalizedOrigin))
                {
                    return false;
                }

                if (configuredCorsOrigins.Contains(normalizedOrigin))
                {
                    return true;
                }

                return builder.Environment.IsDevelopment() && IsDevelopmentOrigin(normalizedOrigin);
            })
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

var app = builder.Build();

// =========================
// MIDDLEWARE PIPELINE
// =========================

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection(); // Redirekt Http zu Https ( derzeit aber optional ) 

app.UseCors("AllowFrontend"); // Enable Cors

app.MapControllers(); // Controllers Map 

app.MapHub<MatchHub>("/matchHub");

app.Run();

static string NormalizeOrigin(string? origin)
{
    return origin?.Trim().TrimEnd('/') ?? string.Empty;
}

static bool IsDevelopmentOrigin(string origin)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
    {
        return false;
    }

    if (uri.Scheme is not ("http" or "https"))
    {
        return false;
    }

    if (uri.IsLoopback)
    {
        return true;
    }

    if (IPAddress.TryParse(uri.Host, out var ipAddress))
    {
        return IsPrivateIpv4(ipAddress);
    }

    return false;
}

static bool IsPrivateIpv4(IPAddress ipAddress)
{
    if (ipAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
    {
        return false;
    }

    var bytes = ipAddress.GetAddressBytes();
    return bytes[0] == 10
        || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
        || (bytes[0] == 192 && bytes[1] == 168);
}

// NOTE:
// Controllers sind auto-discovered Komponente also kann man einfach per MapController und AddControllers diese einfach hinzufuegen
