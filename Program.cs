using Memoria;
using Memoria.Middlewares;
using Memoria.Models.Config;
using Memoria.Services;
using Memoria.Setup;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

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

builder.Services.AddDbContext<AppDbContext>();

builder.Services.AddScoped<SessionValidationMiddleware>();

builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddSingleton<IKeyService, KeyService>();

builder.Services.AddSingleton<IConfigureOptions<CookieAuthenticationOptions>, ConfigureCookieOptions>();
builder.Services.AddSingleton<IConfigureOptions<OpenIdConnectOptions>, ConfigureOidcOptions>();

var authBuilder = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie();

var oidcOptions = builder.Configuration.GetSection(OAuthConfig.ConfigKey).Get<OAuthConfig>();

foreach (var idp in oidcOptions.IdentityProviders)
{
    authBuilder.AddOpenIdConnect(idp.Identifier, options =>
    {
    });
}

builder.Services.AddAuthorization();

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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
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