using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;
using Scalar.AspNetCore;
using Stathub.Infrastructure.Storage;
using Stathub.Modules.Leagues.Infrastructure;
using Stathub.Modules.Matches.Infrastructure;
using Stathub.Modules.Players.Infrastructure;
using Stathub.Modules.Rosters.Infrastructure;
using Stathub.Modules.Standings.Application;
using Stathub.Modules.Standings.Application.Interfaces;
using Stathub.Modules.Standings.Infrastructure.Services;
using Stathub.Modules.Statistics.Application;
using Stathub.Modules.Statistics.Application.Interfaces;
using Stathub.Modules.Statistics.Infrastructure.Services;
using Stathub.Modules.Teams.Infrastructure;
using Stathub.WebApi.Hubs;
using Stathub.WebApi.Infrastructure;
using Stathub.WebApi.Infrastructure.Public;
using Stathub.WebApi.Infrastructure.Uploads;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Отладочная политика для браузера: Expo web (react-native-web) крутится на другом origin
// (обычно http://localhost:8081) и без CORS не сможет обратиться к API.
// Разрешены только локальные адреса и только в Development.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(policy => policy
            .SetIsOriginAllowed(origin =>
                Uri.TryCreate(origin, UriKind.Absolute, out var parsed) &&
                parsed.Host is "localhost" or "127.0.0.1")
            .AllowAnyHeader()
            .AllowAnyMethod()
            // SignalR-клиент по умолчанию идёт с credentials; origin при этом перечислен явно,
            // поэтому AllowCredentials безопасен (в отличие от политики с "*").
            .AllowCredentials()));
}

builder.Services.AddLeaguesModule(builder.Configuration);
builder.Services.AddMatchesModule(builder.Configuration);
builder.Services.AddPlayersModule(builder.Configuration);
builder.Services.AddTeamsModule(builder.Configuration);
builder.Services.AddRostersModule(builder.Configuration);

// Standings — чистое чтение без своего DbContext, поэтому регистрация без модульного extension:
// порты таблицы реализуются адаптерами поверх читателей Leagues/Matches/Teams.
builder.Services.AddScoped<IStandingsRulesSource, StandingsRulesSource>();
builder.Services.AddScoped<IStandingsMatchSource, MatchResultsSource>();
builder.Services.AddScoped<IStandingsTeamNameSource, TeamNameSource>();
builder.Services.AddScoped<StandingsService>();

// Statistics — тоже чистое чтение: считает из протокола матчей, своей БД не имеет.
builder.Services.AddScoped<ITournamentPlayerEventsSource, TournamentPlayerEventsSource>();
builder.Services.AddScoped<IPlayerNameSource, PlayerNameSource>();
builder.Services.AddScoped<ITeamBadgeSource, TeamBadgeSource>();
builder.Services.AddScoped<PlayerStatisticsService>();

// Публичная витрина: композиция read-моделей + one-way SignalR для зрителей (doc §2.1, §4).
builder.Services.AddSignalR();
builder.Services.AddScoped<PublicReadService>();
builder.Services.AddScoped<MatchNotifier>();

// Логотипы: в dev — локальный диск под wwwroot/uploads, раздача статикой.
// MinIO подключается заменой ILogoStorage, домен и БД не меняются (doc §5).
var webRootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(Path.Combine(webRootPath, "uploads", "logos"));
builder.Services.AddSingleton<ILogoStorage>(_ => new LocalLogoStorage(Path.Combine(webRootPath, "uploads")));

// Логотипы: держим тело multipart в памяти. ASP.NET выносит формы больше
// MemoryBufferThreshold во временный файл %TEMP%, а в ограниченных средах (песочница,
// сервисный аккаунт без прав на Temp) это даёт UnauthorizedAccessException и 500.
// Порог равен лимиту запроса, поэтому до него дело не доходит вообще.
builder.Services.Configure<FormOptions>(options =>
{
    options.MemoryBufferThreshold = (int)LogoFile.MaxRequestBytes;
    options.MultipartBodyLengthLimit = LogoFile.MaxRequestBytes;
});

var app = builder.Build();

app.UseExceptionHandler();

// Логотипы команд и лиг: файлы из wwwroot/uploads отдаются как статика.
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseCors();
    app.MapOpenApi();
    app.MapScalarApiReference();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapControllers();
app.MapHub<MatchHub>("/hubs/matches");

app.Run();
