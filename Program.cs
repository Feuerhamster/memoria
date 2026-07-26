using EFCoreSecondLevelCacheInterceptor;
using Memoria;
using Memoria.Authentication;
using Memoria.Extensions;
using Memoria.Middlewares;
using Memoria.Models.Config;
using Memoria.Models.Database;
using Memoria.Services;
using Memoria.Setup;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using AppDbContext = Memoria.AppDbContext;
using AuthenticationService = Memoria.Services.AuthenticationService;
using IAuthenticationService = Memoria.Services.IAuthenticationService;

const string DATA_PROTECTION_KEYS_COLLECTION_NAME = "aspnet-data-protection-keys";
const string DATA_PROTECTION_APPLICATION_NAME = "memoria";

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "[dd.MM hh:mm:ss] ";
    options.SingleLine = true;
});

builder.Services.AddControllers().AddJsonOptions(options => {
    options.JsonSerializerOptions.AllowOutOfOrderMetadataProperties = true;
});;


builder.Services.AddOpenApi();

builder.Services.Configure<DatabaseConfig>(builder.Configuration.GetSection(DatabaseConfig.ConfigKey));
builder.Services.Configure<OAuthConfig>(builder.Configuration.GetSection(OAuthConfig.ConfigKey));
builder.Services.Configure<SessionConfig>(builder.Configuration.GetSection(SessionConfig.ConfigKey));
builder.Services.Configure<FileConfig>(builder.Configuration.GetSection(FileConfig.ConfigKey));

var fileConfig = builder.Configuration.GetSection(FileConfig.ConfigKey).Get<FileConfig>() ?? new FileConfig();

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = fileConfig.UploadLimitMb * 1024 * 1024;
});

builder.Services.AddConfiguredDbContext();

builder.Services.AddScoped<SessionValidationMiddleware>();

builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IAccessPolicyHelperService, AccessPolicyHelperService>();
builder.Services.AddScoped<IImageService, ImageService>();

builder.Services.AddScoped<ISpaceService, SpaceService>();

builder.Services.AddHttpClient();

builder.Services.AddSingleton<IKeyService, KeyService>();

builder.Services.AddSingleton<IConfigureOptions<CookieAuthenticationOptions>, ConfigureCookieOptions>();
builder.Services.AddSingleton<IConfigureOptions<OpenIdConnectOptions>, ConfigureOidcOptions>();

var authBuilder = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie()
    .AddScheme<AuthenticationSchemeOptions, BasicAuthHandler>(BasicAuthHandler.SchemeName, null);

var oidcOptions = builder.Configuration.GetSection(OAuthConfig.ConfigKey).Get<OAuthConfig>();

foreach (var idp in oidcOptions.IdentityProviders)
{
    authBuilder.AddOpenIdConnect(idp.Identifier, options =>
    {
    });
}

builder.Services.AddSingleton<IAuthorizationHandler, TokenPermissionHandler>();

builder.Services.AddDataProtection().SetApplicationName(DATA_PROTECTION_APPLICATION_NAME).PersistKeysToDbContext<AppDbContext>();

builder.Services.AddCors(options => {
    options.AddDefaultPolicy(policy => {
        policy.WithOrigins("http://localhost:5173");
        policy.AllowCredentials();
        policy.AllowAnyHeader();
        policy.AllowAnyMethod();
    });
});

// Reverse Proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.All;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

using (var migrationScope = app.Services.CreateScope())
{
    var migrationDb = migrationScope.ServiceProvider.GetRequiredService<AppDbContext>();
    migrationDb.Database.Migrate();

    // FTS5 virtual table backing global full-text search. Not an EF migration on purpose:
    // dotnet ef migrations add doesn't know about virtual tables, and this project still squashes
    // its migration history during early development — an idempotent startup statement survives that.
    migrationDb.Database.ExecuteSqlRaw("""
        CREATE VIRTUAL TABLE IF NOT EXISTS SearchIndexFts USING fts5(
            entity_type UNINDEXED,
            entity_id UNINDEXED,
            title,
            body
        );
        """);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.UseCors();
}
else
{
    app.UseForwardedHeaders();
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseCustomSessionValidation();

app.MapControllers();

app.Run();