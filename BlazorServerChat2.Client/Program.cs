using BlazorServerChat2.Client;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Fluent UI サービスを登録
builder.Services.AddFluentUIComponents();

// WASM側の認証サポート 🔐
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<AuthenticationStateProvider, PersistentAuthenticationStateProvider>();

// HttpClientをDIに登録（WASM側のコンポーネントがAPIを呼ぶ際に使用）
// WASMでは HttpClientHandler は使えないため、シンプルに設定
builder.Services.AddScoped(sp =>
{
    var client = new HttpClient 
    { 
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) 
    };
    return client;
});

await builder.Build().RunAsync();
