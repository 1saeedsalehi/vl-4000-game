var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

var baseAddress = new Uri(builder.HostEnvironment.BaseAddress);

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = baseAddress });

builder.Services
    .AddSingleton<IMediator, DefaultMediator>()
    .AddSingleton<IHighScoreRepository, LocalStorageHighScoreRepository>()
    .AddSingleton<ISettingsRepository, LocalStorageSettingsRepository>()
    .AddSingleton<GameSettingsService>();

builder.Services.AddHttpClient<IGameManifestRepository, LocalGameManifestRepository>(
    client => client.BaseAddress = baseAddress);

builder.Services.AddHttpClient<ILevelRepository, LocalLevelRepository>(
    client => client.BaseAddress = baseAddress);

await builder.Build().RunAsync();
