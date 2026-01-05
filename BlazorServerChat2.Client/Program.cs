using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

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
