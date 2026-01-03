# BlazorServerChat2

Blazor Server と SignalR を使ったチャットアプリです。Azure OpenAI（Microsoft.Extensions.AI）、Fluent UI、ASP.NET Core Identity を組み合わせて、ログイン後に AI キャラクターとの会話やユーザー同士のリアルタイムチャットを提供します。

## 必要環境

- .NET 10 SDK
- SQL Server（LocalDB でも可）
- Azure OpenAI リソース（エンドポイント・デプロイ名・API キー）

## セットアップ

1. 依存関係を復元します。
   ```bash
   dotnet restore
   ```
2. 接続情報をユーザーシークレットや `appsettings.Development.json` に設定します（例）。
   ```bash
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\\MSSQLLocalDB;Database=BlazorServerChat2;Trusted_Connection=True;MultipleActiveResultSets=true"
   dotnet user-secrets set "Settings:BaseUrl" "https://<your-azure-openai>.openai.azure.com/"
   dotnet user-secrets set "Settings:DeploymentName" "<your-deployment-name>"
   dotnet user-secrets set "Settings:OpenAIKey" "<your-api-key>"
   # オプション
   dotnet user-secrets set "ConnectionStrings:Redis" "<redis-connection-string>"
   dotnet user-secrets set "AzureMonitor:ConnectionString" "<application-insights-connection-string>"
   ```
3. データベースを作成します（初回のみ）。
   ```bash
   dotnet ef database update --project BlazorServerChat2/BlazorServerChat2/BlazorServerChat2.csproj
   ```

## 実行

```bash
dotnet run --project BlazorServerChat2/BlazorServerChat2/BlazorServerChat2.csproj
```

既定では `/` にサインイン画面が表示され、認証後にチャット画面に入室できます。SignalR ハブは `/chat` にマップされています。
