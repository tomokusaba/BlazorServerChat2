using BlazorServerChat2.Areas.Identity;
using BlazorServerChat2.Data;
using BlazorServerChat2.Hubs;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
username = builder.Configuration.GetSection("AppConfiguration")["UserName"];

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
var RedisConnString = builder.Configuration.GetConnectionString("Redis");
builder.Services.AddDistributedRedisCache(options =>
{
    options.Configuration = RedisConnString;
    options.InstanceName = "myapp.";
});
var redis = ConnectionMultiplexer.Connect(RedisConnString);
//builder.Services.AddDataProtection().SetApplicationName("MyApp").PersistKeysToRedis(redis, "DataProtection-Keys");
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "YourAppCookieName";
    options.IdleTimeout = TimeSpan.FromDays(1000);
    options.Cookie.HttpOnly = true;
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddDefaultIdentity<IdentityUser>(@ options =>{ options.SignIn.RequireConfirmedAccount = true;
    options.User.AllowedUserNameCharacters = null;
    
})
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.Cookie.Name = "YourAppCookieName";
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(1000);
    
    options.LoginPath = "/Identity/Account/Login";
    // ReturnUrlParameter requires 
    //using Microsoft.AspNetCore.Authentication.Cookies;
    options.ReturnUrlParameter = CookieAuthenticationDefaults.ReturnUrlParameter;
    options.SlidingExpiration = true;
});
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<IdentityUser>>();
builder.Services.AddSingleton<WeatherForecastService>();
builder.Services.AddScoped<ClientHub>();
builder.Services.AddSingleton<Room>();
builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.MapBlazorHub();
app.MapHub<BlazorChatHub>(BlazorChatHub.HubUrl);
app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
//app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

partial class Program
{
    public static string? username { get; private set; }
}