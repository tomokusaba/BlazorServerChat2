using Azure.AI.OpenAI;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using BlazorApp31.Plugin;
using BlazorServerChat2;
using BlazorServerChat2.Areas.Identity;
using BlazorServerChat2.Data;
using BlazorServerChat2.Components.Account;
using BlazorServerChat2.Hubs;
using BlazorServerChat2.Mcp;
using BlazorServerChat2.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore;
using OpenTelemetry.Metrics;
//using StackExchange.Redis;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
Username = builder.Configuration.GetSection("AppConfiguration")["UserName"];
GptKey = builder.Configuration.GetValue<string>("Settings:OpenAIKey");
GptUrl = builder.Configuration.GetValue<string>("Settings:OpenAIEndPoint") ?? string.Empty;

// DataProtection設定（IIS OutOfProcess対応）🔐
// 認証Cookieの暗号化キーをファイルに永続化
// appsettings.jsonの DataProtection:KeysFolder でパスを指定可能（省略時はContentRootPath/keys）
var keysFolder = builder.Configuration["DataProtection:KeysFolder"] 
    ?? Path.Combine(builder.Environment.ContentRootPath, "keys");
Directory.CreateDirectory(keysFolder);
builder.Services.AddDataProtection()
    .SetApplicationName("BlazorServerChat2")
    .PersistKeysToFileSystem(new DirectoryInfo(keysFolder));

// CORS設定（WASMからのAPI呼び出しに必要）
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true) // 同一オリジンを許可
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // 認証Cookie送信を許可
    });
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString), ServiceLifetime.Scoped);

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
    // パスキーサポートのためスキーマバージョン3を使用
    options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// BasePath取得（IISサブアプリケーション対応）
var basePath = builder.Configuration["BasePath"]?.TrimEnd('/') ?? string.Empty;

// Cookie設定（IISサブアプリケーション対応）
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "BlazorChat.Auth";
    // UsePathBaseと組み合わせるため、パスは "/" に設定
    options.Cookie.Path = "/";
    options.Cookie.SameSite = SameSiteMode.Lax; // クロスサイトリクエストでもCookie送信
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// JWT認証を追加（Cookie認証と併用） 🔐
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "BlazorServerChat2-JWT-Secret-Key-2026-SuperSecure-256bit!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "BlazorServerChat2";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "BlazorServerChat2-Client";

// JWTをAPIアクセス用に追加（デフォルトスキームはIdentityのCookieのまま）
builder.Services.AddAuthentication()
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

// 認証ポリシー: Cookie認証が優先（デフォルト）、APIはJWTも許可
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("ApiPolicy", policy =>
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
              .RequireAuthenticatedUser());

// JWTサービスを登録
builder.Services.AddSingleton<JwtService>();

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

// Web API コントローラーのサポート
builder.Services.AddControllers();

builder.Services.AddRazorPages();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();  // Interactive Auto サポート

// カスタムSignalRハブ用のサービス追加
builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.HandshakeTimeout = TimeSpan.FromSeconds(30);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpClient();
builder.Services.AddFluentUIComponents(options =>
{
    //options.HostingModel = BlazorHostingModel.Server;
});

// InteractiveAuto対応の認証状態プロバイダー（WASMへの永続化サポート） 🔐
builder.Services.AddScoped<AuthenticationStateProvider, PersistingRevalidatingAuthenticationStateProvider>();
builder.Services.AddSingleton<WeatherForecastService>();
builder.Services.AddScoped<ClientHub>();
builder.Services.AddSingleton<Room>();
builder.Services.AddSingleton<HttpClient>();
builder.Services.AddSingleton<UserChatSettingCache>();

// InteractiveAuto用のHttpClient認証サポート
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddScoped<BlazorServerChat2.Services.AuthenticatedHttpClientFactory>();

// InteractiveAuto用のJWTトークンプロバイダー（サーバー→クライアント受け渡し）🔐
builder.Services.AddScoped<BlazorServerChat2.Services.JwtTokenProvider>();

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
    .UseOpenTelemetry(sourceName: openTelemetrySourceName, configure: (cfg) => cfg.EnableSensitiveData = true)
    .Build();
builder.Services.AddSingleton<IChatClient>(client);
builder.Services.AddSingleton<AgentFrameworkLogic>();
builder.Services.AddScoped<ScreenModePlugin>();
builder.Services.AddScoped<WeatherPlugin>();

// MCPサーバーを追加
// Streamable HTTP トランスポートでMCPエンドポイントを公開
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();
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

// IISサブアプリケーション対応: PathBaseを設定（最初に配置）🔧
var appBasePath = app.Configuration["BasePath"]?.TrimEnd('/');
if (!string.IsNullOrEmpty(appBasePath))
{
    app.UsePathBase(appBasePath);
}

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
app.UseHttpsRedirection();

//app.UseStaticFiles();
app.MapStaticAssets();

//app.UseCookiePolicy();
app.UseRouting();

// CORSを有効化（UseRoutingの後、UseAuthenticationの前）
app.UseCors();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// SignalRハブはUseAuthentication/UseAuthorizationの後にマップ
app.MapHub<BlazorChatHub>(BlazorChatHub.HubUrl);

app.MapControllers();
app.MapRazorPages();

// パスキーエンドポイントをマップ
app.MapAdditionalIdentityEndpoints();

// MCPエンドポイントをマップ (Streamable HTTP)
// エンドポイント: /mcp
app.MapMcp("/mcp");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()  // Interactive Auto サポート
    .AddAdditionalAssemblies(typeof(BlazorServerChat2.Client._Imports).Assembly);  // Client アセンブリを追加

app.Run();

partial class Program
{
    public static string? Username { get; private set; }
    public static string? GptKey { get; private set; }
    public static string? GptUrl { get; private set; }
}