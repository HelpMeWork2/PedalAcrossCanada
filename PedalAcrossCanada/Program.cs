using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PedalAcrossCanada;
using PedalAcrossCanada.Auth;
using PedalAcrossCanada.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? throw new InvalidOperationException("ApiBaseUrl is not configured in wwwroot/appsettings.json.");

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

builder.Services.AddAuthorizationCore();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<JwtAuthStateProvider>());

builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuthHttpService>();
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<EventHttpService>();
builder.Services.AddScoped<MilestoneHttpService>();
builder.Services.AddScoped<TeamHttpService>();
builder.Services.AddScoped<ParticipantHttpService>();
builder.Services.AddScoped<ActivityHttpService>();
builder.Services.AddScoped<StravaHttpService>();

await builder.Build().RunAsync();
