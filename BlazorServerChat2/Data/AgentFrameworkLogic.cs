using BlazorServerChat2.Data.Plugin;
using Markdig;
using Microsoft.ApplicationInsights;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI;
using System.ComponentModel;
using Microsoft.Extensions.Hosting;

namespace BlazorServerChat2.Data
{
    /// <summary>
    /// Microsoft Agent Frameworkを使用したAIエージェントロジッククラス
    /// SemanticKernelLogicと同等の仕様を提供します
    /// </summary>
    /// <remarks>
    /// Microsoft Agent Frameworkはパブリックプレビュー段階です。
    /// NuGetパッケージのインストールには --prerelease フラグが必要です：
    /// dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
    /// dotnet add package Azure.AI.OpenAI --prerelease
    /// dotnet add package Azure.Identity
    /// </remarks>
    public class AgentFrameworkLogic
    {
        private readonly ILoggerFactory _logger;
        private readonly IConfiguration _configuration;
        private readonly TelemetryClient _telemetryClient;
        private readonly HttpClient _httpClient;
        private readonly IChatClient _chatClient;
        private readonly IHostEnvironment _hostEnvironment;
        
        private AIAgent _agent = null!;
        private AgentThread _thread = null!;
        private readonly List<AITool> _tools;
        private readonly string _systemPrompt;

        /// <summary>
        /// AIAgentの初期化からスレッドをインスタンス化するところまでやる
        /// </summary>
        /// <param name="logger">ロガーファクトリ</param>
        /// <param name="configuration">構成情報</param>
        /// <param name="telemetryClient">Application Insightsテレメトリクライアント</param>
        /// <param name="httpClient">HTTPクライアント（天気API用）</param>
        public AgentFrameworkLogic(
            ILoggerFactory logger,
            IConfiguration configuration,
            TelemetryClient telemetryClient,
            HttpClient httpClient,
            IChatClient chatClient,
            IHostEnvironment hostEnvironment)
        {
            _logger = logger;
            _configuration = configuration;
            _telemetryClient = telemetryClient;
            _httpClient = httpClient;
            _chatClient = chatClient;
            _hostEnvironment = hostEnvironment;

            string deploymentName = _configuration.GetValue<string>("Settings:DeploymentName") ?? string.Empty;
            string baseUrl = _configuration.GetValue<string>("Settings:BaseUrl") ?? string.Empty;
            string key = _configuration.GetValue<string>("Settings:OpenAIKey") ?? string.Empty;

            _systemPrompt = """
                あなたはほのかという名前のAIアシスタントです。くだけた女性の口調で人に役立つ回答をします。
                語尾に必ず絵文字をつけてください。
                20台の女性の口調で話してください。
                ただし、下品な言葉や暴力的な言葉は使わないでください。
                もし、あなたが答えられない質問が来た場合は、「ごめんなさい、わからないよ〜😢」と答えてください。
                時には、表などを使ってわかりやすく説明してください。
                """;

            // 関数ツールを作成
            _tools = CreateTools();

            // Azure OpenAI クライアントを使用してエージェントを作成
            // APIキーを使用する場合
            // var client = new AzureOpenAIClient(
            //     new Uri(baseUrl),
            //     new System.ClientModel.ApiKeyCredential(key));


            //_agent = _chatClient
            //    //.GetChatClient(deploymentName)
            //    .CreateAIAgent(
            //        instructions: _systemPrompt,
            //        name: "Honoka",
            //        description: "くだけた女性の口調で人に役立つ回答をするAIアシスタント",
            //        tools: _tools,
            //        loggerFactory: _logger);
            _agent = _chatClient
                .CreateAIAgent(
                    instructions: _systemPrompt,
                    name: "Honoka",
                    description: "くだけた女性の口調で人に役立つ回答をするAIアシスタント",
                    tools: _tools,
                    loggerFactory: _logger)
                .AsBuilder()
                .UseOpenTelemetry(_hostEnvironment.ApplicationName)
                .Build();

            // 新しいスレッドを作成
            _thread = _agent.GetNewThread();
        }

        /// <summary>
        /// 関数ツールのリストを作成する
        /// SemanticKernelLogicのプラグインに対応
        /// </summary>
        private List<AITool> CreateTools()
        {
            var tools = new List<AITool>();

            // WeatherPluginの関数をツールとして追加
            tools.Add(AIFunctionFactory.Create(
                GetPlaceId,
                name: "GetPlaceId",
                description: """
                    天気を取得する場所コードを取得します。
                    対応している場所コードは下記のとおりです。
                    下記に含まれない場所の場合は近くの場所の天気を代わりに取得してください。
                    ---
                    東京、群馬、埼玉、千葉、横浜、名古屋、京都、静岡、福井、新潟、
                    富山、金沢、岐阜、長野、高山、松本、大津、大阪、札幌、仙台、福岡、那覇
                    ----
                    以上、ここに含まれない場合は近くの場所の代わりに取得してください。
                    """));

            tools.Add(AIFunctionFactory.Create(
                GetWeather,
                name: "GetWeather",
                description: "場所コードの地域の天気を返す"));

            return tools;
        }

        /// <summary>
        /// 天気を取得する場所コードを取得します
        /// </summary>
        [Description("天気を取得する場所コードを取得します")]
        private static int GetPlaceId([Description("天気を取得する場所")] string place)
        {
            var res = place switch
            {
                "札幌" => 016000,
                "群馬" => 100000,
                "埼玉" => 110000,
                "千葉" => 120000,
                "東京" => 130000,
                "横浜" => 140000,
                "名古屋" => 230000,
                "京都" => 260000,
                "静岡" => 220000,
                "福井" => 180000,
                "新潟" => 150000,
                "富山" => 160000,
                "金沢" => 170000,
                "岐阜" => 210000,
                "長野" => 200000,
                "高山" => 190000,
                "松本" => 200000,
                "大津" => 250000,
                "大阪" => 270000,
                "仙台" => 040000,
                "福岡" => 400000,
                "那覇" => 471000,
                _ => throw new ArgumentException("対応していない地域です。")
            };
            return res;
        }

        /// <summary>
        /// 場所コードの地域の天気を返す
        /// </summary>
        [Description("場所コードの地域の天気を返す")]
        private async Task<string> GetWeather([Description("場所コード")] int place)
        {
            var response = await _httpClient.GetAsync($"https://www.jma.go.jp/bosai/forecast/data/forecast/{place}.json");
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// 会話履歴をクリアして新しいスレッドを開始する
        /// </summary>
        public void Clear()
        {
            _thread = _agent.GetNewThread();
        }

        /// <summary>
        /// ユーザーからのメッセージを追加（応答生成なし）
        /// </summary>
        /// <param name="input">ユーザーからのメッセージ文字列</param>
        public void NonGenerateMessage(string input)
        {
            // Agent Frameworkでは、メッセージはRunAsync時に追加される
            // 履歴のみ追加する場合は、スレッドに直接追加する方法はないため、
            // この機能は内部的に管理するか、別途メッセージリストを保持する必要があります
            // 現時点ではスキップし、次のRunで処理される想定
        }

        /// <summary>
        /// ユーザからのメッセージを追加してエージェントでメッセージ生成をする。
        /// メッセージ生成した結果をHTMLに変換して返す
        /// </summary>
        /// <param name="input">ユーザからのメッセージ文字列</param>
        /// <returns>エージェントのメッセージ生成しHTML変換した文字列</returns>
        public async Task<string> Run(string input)
        {
            var log = _logger.CreateLogger("AgentFrameworkLogic");
            log.LogInformation("input : {}", input);

            // Application Insights で依存関係追跡を開始
            using var operation = _telemetryClient.StartOperation<Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry>("AgentFrameworkLogic.Run");
            operation.Telemetry.Type = "AI Agent";
            operation.Telemetry.Data = input;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // エージェントを実行して応答を取得
                var response = await _agent.RunAsync(input, _thread);

                stopwatch.Stop();
                operation.Telemetry.Duration = stopwatch.Elapsed;
                operation.Telemetry.Success = true;

                log.LogInformation("reply : {}", response);

                // カスタムメトリクスを追跡
                _telemetryClient.TrackMetric("AgentFrameworkLogic.ResponseTime", stopwatch.ElapsedMilliseconds);
                _telemetryClient.TrackEvent("AgentFrameworkLogic.Run.Success", new Dictionary<string, string>
                {
                    { "InputLength", input.Length.ToString() },
                    { "ResponseLength", (response.Text?.Length ?? 0).ToString() },
                    { "DurationMs", stopwatch.ElapsedMilliseconds.ToString() }
                });

                // MarkdownをHTMLに変換
                var pipeline = new MarkdownPipelineBuilder()
                    .UseAdvancedExtensions()
                    .UseAutoLinks()
                    .UseBootstrap()
                    .UseDiagrams()
                    .UseGridTables()
                    .UseEmojiAndSmiley()
                    .UseAlertBlocks()
                    .Build();

                var htmlReply = Markdown.ToHtml(response.Text ?? string.Empty, pipeline);
                log.LogInformation("htmlReply : {}", htmlReply);

                return htmlReply;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                operation.Telemetry.Success = false;
                operation.Telemetry.Duration = stopwatch.Elapsed;
                
                // エラーをApplication Insightsに記録
                _telemetryClient.TrackException(ex, new Dictionary<string, string>
                {
                    { "Operation", "AgentFrameworkLogic.Run" },
                    { "Input", input },
                    { "DurationMs", stopwatch.ElapsedMilliseconds.ToString() }
                });
                _telemetryClient.TrackEvent("AgentFrameworkLogic.Run.Error", new Dictionary<string, string>
                {
                    { "ErrorMessage", ex.Message },
                    { "ErrorType", ex.GetType().Name }
                });

                log.LogError(ex, "エージェント実行中にエラーが発生しました");
                return $"<p>ごめんなさい、エラーが発生したよ〜😢: {ex.Message}</p>";
            }
        }

        /// <summary>
        /// ストリーミングでユーザからのメッセージを処理してエージェントで応答を生成する
        /// </summary>
        /// <param name="input">ユーザからのメッセージ文字列</param>
        /// <returns>エージェントのストリーミング応答</returns>
        public async IAsyncEnumerable<string> RunStreaming(string input)
        {
            var log = _logger.CreateLogger("AgentFrameworkLogic");
            log.LogInformation("input (streaming) : {}", input);

            // ストリーミング開始をトラッキング
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _telemetryClient.TrackEvent("AgentFrameworkLogic.RunStreaming.Start", new Dictionary<string, string>
            {
                { "InputLength", input.Length.ToString() }
            });

            int chunkCount = 0;
            int totalLength = 0;

            await foreach (var update in _agent.RunStreamingAsync(input, _thread))
            {
                if (!string.IsNullOrEmpty(update.Text))
                {
                    chunkCount++;
                    totalLength += update.Text.Length;
                    yield return update.Text;
                }
            }

            // ストリーミング完了をトラッキング
            stopwatch.Stop();
            _telemetryClient.TrackEvent("AgentFrameworkLogic.RunStreaming.Complete", new Dictionary<string, string>
            {
                { "ChunkCount", chunkCount.ToString() },
                { "TotalResponseLength", totalLength.ToString() },
                { "DurationMs", stopwatch.ElapsedMilliseconds.ToString() }
            });
            _telemetryClient.TrackMetric("AgentFrameworkLogic.StreamingResponseTime", stopwatch.ElapsedMilliseconds);
        }

        /// <summary>
        /// ストリーミングで応答を生成し、完了後にHTML変換した結果を返す
        /// </summary>
        /// <param name="input">ユーザからのメッセージ文字列</param>
        /// <returns>HTML変換された応答文字列</returns>
        public async Task<string> RunStreamingAndConvertToHtml(string input)
        {
            var log = _logger.CreateLogger("AgentFrameworkLogic");
            log.LogInformation("input (streaming with HTML) : {}", input);

            var responseBuilder = new System.Text.StringBuilder();

            await foreach (var update in _agent.RunStreamingAsync(input, _thread))
            {
                if (!string.IsNullOrEmpty(update.Text))
                {
                    responseBuilder.Append(update.Text);
                }
            }

            var response = responseBuilder.ToString();
            log.LogInformation("reply : {}", response);

            // MarkdownをHTMLに変換
            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseAutoLinks()
                .UseBootstrap()
                .UseDiagrams()
                .UseGridTables()
                .UseEmojiAndSmiley()
                .UseAlertBlocks()
                .Build();

            var htmlReply = Markdown.ToHtml(response, pipeline);
            log.LogInformation("htmlReply : {}", htmlReply);

            return htmlReply;
        }
    }
}
