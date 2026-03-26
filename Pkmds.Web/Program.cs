// Configure Blazor WebAssembly application and services

var builder = WebAssemblyHostBuilder.CreateDefault(args);
var services = builder.Services;
var logging = builder.Logging;

// Configure Serilog with browser console sink for client-side logging
// The level switch allows runtime control of log verbosity
var levelSwitch = new LoggingLevelSwitch();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.ControlledBy(levelSwitch)
    .Enrich.FromLogContext()
    .WriteTo.BrowserConsole()
    .CreateLogger();

logging.ClearProviders();
logging.AddSerilog(Log.Logger, true);

// Configure Sentry for error tracking and user feedback
var sentryDsn = builder.Configuration["Sentry:Dsn"] ?? string.Empty;
if (sentryDsn.StartsWith("%%"))
{
    sentryDsn = string.Empty; // Ignore unreplaced CI placeholder
}

builder.UseSentry(options =>
{
    options.Dsn = sentryDsn;
    options.SendDefaultPii = false;
    options.TracesSampleRate = 0.1;
    options.MaxAttachmentSize = 10 * 1024 * 1024; // 10 MiB
});

logging.AddSentry(o => o.InitializeSdk = false);

// Add Blazor root components
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure MudBlazor UI library
services
    .AddMudServices(config =>
    {
        config.SnackbarConfiguration.PreventDuplicates = false;
        config.SnackbarConfiguration.ClearAfterNavigation = true;
    });

// Register application services
services
    .AddSingleton(_ => new HttpClient { BaseAddress = new(builder.HostEnvironment.BaseAddress) })
    .AddFileSystemAccessService() // File System Access API for loading/saving files
    .AddSingleton<IAppState, AppState>()
    .AddSingleton<IRefreshService, RefreshService>()
    .AddSingleton<IAppService, AppService>()
    .AddSingleton<IDragDropService, DragDropService>()
    .AddSingleton<ILoggingService, LoggingService>()
    .AddSingleton<ISettingsService, SettingsService>()
    .AddSingleton(levelSwitch)
    .AddSingleton<JsService>()
    .AddSingleton<BlazorAesProvider>()
    .AddSingleton<BlazorMd5Provider>()
    .AddSingleton<IBugReportService, BugReportService>()
    .AddSingleton<IDescriptionService, DescriptionService>();

var app = builder.Build();

// IMPORTANT: Replace PKHeX.Core's cryptography providers with JavaScript-based implementations
// This is necessary because Blazor WASM doesn't support System.Security.Cryptography APIs natively.
// We use crypto-js via JavaScript interop for AES encryption/decryption and MD5 hashing.
RuntimeCryptographyProvider.Aes = app.Services.GetRequiredService<BlazorAesProvider>();
RuntimeCryptographyProvider.Md5 = app.Services.GetRequiredService<BlazorMd5Provider>();

// Configure logging service to allow runtime changes to log level
var loggingService = app.Services.GetRequiredService<ILoggingService>();
loggingService.OnLoggingConfigurationChanged += level => levelSwitch.MinimumLevel = level;

await app.RunAsync();
