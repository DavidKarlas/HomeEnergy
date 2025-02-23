using MudBlazor.Services;
using HomeEnergy.Components;
using HomeEnergy.Database;
using HomeEnergy.Services;
using System.Reflection;
using HomeEnergy.Devices.Interfaces;
using HomeEnergy.Policies;
using ApexCharts;

var builder = WebApplication.CreateBuilder(args);

// Add MudBlazor services
builder.Services.AddMudServices();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

Assembly.GetExecutingAssembly().GetTypes()
             .Where(x => !x.IsAbstract && x.IsClass && typeof(IDeviceFactory).IsAssignableFrom(x))
             .ToList()
             .ForEach(f => builder.Services.Add(new ServiceDescriptor(typeof(IDeviceFactory), f, ServiceLifetime.Singleton)));
Assembly.GetExecutingAssembly().GetTypes()
             .Where(x => !x.IsAbstract && x.IsClass && typeof(IPolicyFactory).IsAssignableFrom(x))
             .ToList()
             .ForEach(f => builder.Services.Add(new ServiceDescriptor(typeof(IPolicyFactory), f, ServiceLifetime.Singleton)));

builder.Services.AddDbContext<MainDbContext>();
builder.Services.AddSingleton<DevicesManager>();
builder.Services.AddSingleton<SolarProductionForecast>();
builder.Services.AddSingleton<EnergyForecastService>();
builder.Services.AddSingleton<Entso_E_ApiClient>();
builder.Services.AddSingleton<DayAheadPricesService>();
builder.Services.AddApexCharts(e => {
    e.GlobalOptions = new ApexChartBaseOptions {
        Debug = true,
        Theme = new Theme { Mode = Mode.Dark }
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Services.GetService<EnergyForecastService>();

app.Run();
