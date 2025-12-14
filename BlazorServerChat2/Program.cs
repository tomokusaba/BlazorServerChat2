using Azure.AI.OpenAI;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using BlazorApp31.Plugin;
using BlazorServerChat2.Areas.Identity;
using BlazorServerChat2.Data;
using BlazorServerChat2.Hubs;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.FluentUI.AspNetCore.Components;
using OpenTelemetry.Metrics;
//using StackExchange.Redis;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
Username = builder.Configuration.GetSection("AppConfiguration")["UserName"];
GptKey = builder.Configuration.GetValue<string>("Settings:OpenAIKey");
GptUrl = builder.Configuration.GetValue<string>("Settings:OpenAIEndPoint") ?? string.Empty;

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

var RedisConnString = builder.Configuration.GetConnectionString("Redis");
//builder.Services.AddDistributedRedisCache(options =>
//{
//    options.Configuration = RedisConnString;
//    options.InstanceName = "myapp.";
//});
//var redis = ConnectionMultiplexer.Connect(RedisConnString);
//builder.Services.AddDataProtection().SetApplicationName("MyApp").PersistKeysToRedis(redis, "DataProtection-Keys");
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "YourAppCookieName";
    options.IdleTimeout = TimeSpan.FromSeconds(10);
    options.Cookie.IsEssential = true;

});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    options.User.AllowedUserNameCharacters = null!;

})
    .AddEntityFrameworkStores<ApplicationDbContext>();

//builder.Services.ConfigureApplicationCookie(options =>
//{
//    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
//    options.Cookie.Name = "YourAppCookieName";
//    options.ExpireTimeSpan = TimeSpan.FromDays(1000);

//    options.LoginPath = "/Identity/Account/Login";
//    // ReturnUrlParameter requires 
//    //using Microsoft.AspNetCore.Authentication.Cookies;
//    options.ReturnUrlParameter = CookieAuthenticationDefaults.ReturnUrlParameter;
//    options.SlidingExpiration = true;
//});
if (OperatingSystem.IsWindows())
{
    builder.Logging.AddEventLog();
}
builder.Logging.AddFilter("Microsoft.SemanticKernel", LogLevel.Trace);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Error);
var applicationInsightsConnectionString =
    builder.Configuration["AzureMonitor:ConnectionString"]
    ?? builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
    ?? builder.Configuration["ApplicationInsights:ConnectionString"]
    ?? string.Empty;

var openTelemetrySourceName = builder.Environment.ApplicationName;

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(openTelemetrySourceName))
    .WithTracing(tracing => tracing
        .AddSource(openTelemetrySourceName)
        .AddSource("*Microsoft.Extensions.AI")
        .AddSource("*Microsoft.Extensions.Agents*")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(metrics => metrics
        .AddMeter("*Microsoft.Agents.AI")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .UseAzureMonitor(options =>
    {
        if (!string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
        {
            options.ConnectionString = applicationInsightsConnectionString;
        }
    });

builder.Services.AddApplicationInsightsTelemetry();


builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromDays(3);

});
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpClient();
builder.Services.AddFluentUIComponents(options =>
{
    //options.HostingModel = BlazorHostingModel.Server;
});

builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<IdentityUser>>();
builder.Services.AddSingleton<WeatherForecastService>();
builder.Services.AddScoped<ClientHub>();
builder.Services.AddSingleton<Room>();
builder.Services.AddSingleton<HttpClient>();
//builder.Services.AddSingleton<SemanticKernelLogic>();
            string baseUrl = builder.Configuration.GetValue<string>("Settings:BaseUrl") ?? string.Empty;
            string key = builder.Configuration.GetValue<string>("Settings:OpenAIKey") ?? string.Empty;
            string deploymentName = builder.Configuration.GetValue<string>("Settings:DeploymentName") ?? string.Empty;
            var client = new AzureOpenAIClient(
                new Uri(baseUrl),
                new System.ClientModel.ApiKeyCredential(key))
                .GetChatClient(deploymentName)
                .AsIChatClient()
                .AsBuilder()
                .UseOpenTelemetry(sourceName: openTelemetrySourceName, configure: (cfg) => cfg.EnableSensitiveData = builder.Environment.IsDevelopment())
                .Build();
builder.Services.AddSingleton<IChatClient>(client);
builder.Services.AddSingleton<AgentFrameworkLogic>();
builder.Services.AddScoped<ScreenModePlugin>();
builder.Services.AddScoped<WeatherPlugin>();
builder.Services.AddHttpLogging(c =>
{
    
});
//builder.Logging.ClearProviders();
var app = builder.Build();

//var _telemetryClient = app.Services.GetRequiredService<TelemetryClient>();

//var meterListener = new MeterListener();

//meterListener.InstrumentPublished = (Instrument, listener) =>
//{
//    if (Instrument.Meter.Name.StartsWith("Microsoft.SemanticKernel", StringComparison.Ordinal))
//    {
//        listener.EnableMeasurementEvents(Instrument);
//    }
//    if (Instrument.Meter.Name.StartsWith("SemanticKernelLogic", StringComparison.Ordinal))
//    {
//        listener.EnableMeasurementEvents(Instrument);
//    }
//    if (Instrument.Meter.Name.StartsWith("AgentFrameworkLogic", StringComparison.Ordinal))
//    {
//        listener.EnableMeasurementEvents(Instrument);
//    }
//    if (Instrument.Meter.Name.StartsWith("AzureChatCompletion", StringComparison.Ordinal))
//    {
//        listener.EnableMeasurementEvents(Instrument);
//    }
//    // Microsoft Agent Framework と Microsoft.Extensions.AI のメトリクス
//    if (Instrument.Meter.Name.StartsWith("Microsoft.Extensions.AI", StringComparison.Ordinal))
//    {
//        listener.EnableMeasurementEvents(Instrument);
//    }
//    if (Instrument.Meter.Name.StartsWith("Microsoft.Agents", StringComparison.Ordinal))
//    {
//        listener.EnableMeasurementEvents(Instrument);
//    }

//};

//meterListener.SetMeasurementEventCallback<double>((instrument, measurment, tags, state) =>
//{
//    _telemetryClient.GetMetric(instrument.Name).TrackValue(measurment);
//});

//meterListener.Start();


//_telemetryClient.StartOperation<DependencyTelemetry>("ApplicationInsights.Example");
//var activityListener = new ActivityListener();

//activityListener.ShouldListenTo =
//    activitySource => activitySource.Name.StartsWith("Microsoft.SemanticKernel", StringComparison.Ordinal)
//    || activitySource.Name.StartsWith("AzureChatCompletion", StringComparison.Ordinal);

//ActivitySource.AddActivityListener(activityListener);

app.UseHttpLogging();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    //app.UseForwardedHeaders();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    //app.UseForwardedHeaders();

    app.UseHsts();
}
app.MapBlazorHub();
app.MapHub<BlazorChatHub>(BlazorChatHub.HubUrl);
app.UseHttpsRedirection();

//app.UseStaticFiles();
app.MapStaticAssets();

//app.UseCookiePolicy();
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
    public static string? Username { get; private set; }
    public static string? GptKey { get; private set; }
    public static string? GptUrl { get; private set; }
}