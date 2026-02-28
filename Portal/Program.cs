using TicTacToe;
using LightCycles;
using Asteroids;
using PocketTanks;

var builder = WebApplication.CreateBuilder(args);

// MVC + API
builder.Services.AddControllersWithViews();

// SignalR
builder.Services.AddSignalR();

// Register game services
builder.Services.AddTicTacToe(builder.Configuration);
builder.Services.AddLightCycles(builder.Configuration);
builder.Services.AddAsteroids(builder.Configuration);
builder.Services.AddPocketTanks(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();

// MVC routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Map game hubs + endpoints
app.MapTicTacToe();
app.MapLightCycles();
app.MapAsteroids();
app.MapPocketTanks();

app.Run();
