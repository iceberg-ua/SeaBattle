using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SeaBattle.Client;
using SeaBattle.Client.Services;
using SeaBattle.Shared;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<ILocalStorageService, LocalStorageService>();
builder.Services.AddScoped<IGameStateService, GameStateService>();

// Error handling and notification services
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IErrorHandlingService, ErrorHandlingService>();

await builder.Build().RunAsync();
