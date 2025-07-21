using Microsoft.AspNetCore.ResponseCompression;
using SeaBattle.Server.Hubs;
using SeaBattle.Server.Services;
using SeaBattle.Server.Infrastructure.Events;
using SeaBattle.Server.Infrastructure.Repositories;
using SeaBattle.Shared;
using SeaBattle.Shared.Domain;
using SeaBattle.Shared.Domain.Events;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/octet-stream" });
});

// Legacy services (to be phased out)
builder.Services.AddSingleton<GlobalGameStorage>();
builder.Services.AddSingleton<GameService>();

// Domain services
builder.Services.AddScoped<GameLogicService>();

// Infrastructure services
builder.Services.AddSingleton<IGameRepository, InMemoryGameRepository>();
builder.Services.AddScoped<IDomainEventPublisher, LoggingDomainEventPublisher>();
builder.Services.AddSingleton<GameLockingService>();

var app = builder.Build();

app.UseResponseCompression();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.MapRazorPages();
app.MapControllers();
app.MapHub<BattleHub>("/battlehub");
app.MapFallbackToFile("index.html");

#if DEBUG
    app.UseWebAssemblyDebugging();
#endif

app.Run();
