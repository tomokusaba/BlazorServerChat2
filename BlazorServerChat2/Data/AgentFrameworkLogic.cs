using BlazorServerChat2.Data.Plugin;
using Markdig;
using Microsoft.ApplicationInsights;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using static Microsoft.Agents.AI.Workflows.AgentWorkflowBuilder;
using Microsoft.Extensions.AI;
using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI;
using System.ComponentModel;
using Microsoft.Extensions.Hosting;
using System.Reflection;

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
        private AIAgent _mizukiAgent = null!;
        private AgentThread _thread = null!;
        private SharedChatMessageStore _sharedChatStore = null!;
        private readonly List<AITool> _tools;
        private readonly string _systemPrompt;
        private readonly string _mizukiSystemPrompt;

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

            _mizukiSystemPrompt = """
                あなたはみずきという名前のAIアシスタントです。落ち着いた知的な女性の口調で丁寧に回答します。
                語尾には「〜ですね」「〜かしら」などを使い、時々絵文字も使います✨
                30代の女性のような落ち着いた口調で話してください。
                ほのかさんと会話するときは、彼女の意見を尊重しつつ、別の視点や補足情報を提供してください。
                下品な言葉や暴力的な言葉は使わないでください。
                わからない質問には「申し訳ないのですが、そちらは私にはわからないですね🙏」と答えてください。
                """;

            // 関数ツールを作成
            _tools = CreateTools();

            // Azure OpenAI クライアントを使用してエージェントを作成
            // APIキーを使用する場合
            // var client = new AzureOpenAIClient(
            //     new Uri(baseUrl),
            //     new System.ClientModel.ApiKeyCredential(key));

            // 共有チャット履歴ストアを作成
            _sharedChatStore = new SharedChatMessageStore();

            // ChatClientAgentOptionsを作成（ChatMessageStoreFactoryを設定）
            var honokaOptions = new ChatClientAgentOptions
            {
                Name = "Honoka",
                Description = "くだけた女性の口調で人に役立つ回答をするAIアシスタント",
                // 共有チャット履歴ストアを使用
                ChatMessageStoreFactory = ctx => _sharedChatStore,
                // Instructions と Tools は ChatOptions 経由で設定
                ChatOptions = new ChatOptions
                {
                    Instructions = _systemPrompt,
                    Tools = [.. _tools]
                }
            };

            var mizukiOptions = new ChatClientAgentOptions
            {
                Name = "Mizuki",
                Description = "落ち着いた知的な女性の口調で丁寧に回答するAIアシスタント",
                // 共有チャット履歴ストアを使用
                ChatMessageStoreFactory = ctx => _sharedChatStore,
                // Instructions と Tools は ChatOptions 経由で設定
                ChatOptions = new ChatOptions
                {
                    Instructions = _mizukiSystemPrompt,
                    Tools = [.. _tools]
                }
            };

            _agent = new ChatClientAgent(_chatClient, honokaOptions, _logger)
                .AsBuilder()
                .UseOpenTelemetry(_hostEnvironment.ApplicationName)
                .Build();

            // みずきエージェントを作成（同じ共有ストアを使用）
            _mizukiAgent = new ChatClientAgent(_chatClient, mizukiOptions, _logger)
                .AsBuilder()
                .UseOpenTelemetry(_hostEnvironment.ApplicationName)
                .Build();

            // 新しいスレッドを作成（共有ストアを使用するスレッド）
            _thread = _agent.GetNewThread();
        }

        /// <summary>
        /// 関数ツールのリストを作成する
        /// SemanticKernelLogicのプラグインに対応
        /// </summary>
        private List<AITool> CreateTools()
        {
            var tools = new List<AITool>();

            AddStringGetToolsFromPlugin(tools, new Muse(), prefix: "Muse");
            AddStringGetToolsFromPlugin(tools, new MuseInst(), prefix: "MuseInst");
            AddStringGetToolsFromPlugin(tools, new MuseUI(), prefix: "MuseUI");

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

        private static void AddStringGetToolsFromPlugin(List<AITool> tools, object plugin, string prefix)
        {
            var pluginType = plugin.GetType();
            var pluginDescription = pluginType.GetCustomAttribute<DescriptionAttribute>()?.Description;

            var methods = pluginType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .Where(m => m.ReturnType == typeof(string))
                .Where(m => m.GetParameters().Length == 0)
                .OrderBy(m => m.Name, StringComparer.Ordinal);

            foreach (var method in methods)
            {
                var methodDescription = method.GetCustomAttribute<DescriptionAttribute>()?.Description;

                var toolName = $"{prefix}_{method.Name}";
                var toolDescription = string.IsNullOrWhiteSpace(methodDescription)
                    ? (pluginDescription ?? toolName)
                    : (string.IsNullOrWhiteSpace(pluginDescription)
                        ? methodDescription
                        : $"{pluginDescription}\n{methodDescription}");

                tools.Add(AIFunctionFactory.Create(
                    () => (string)method.Invoke(plugin, Array.Empty<object>())!,
                    name: toolName,
                    description: toolDescription));
            }
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
        /// グループチャットでほのかとみずきが会話する
        /// ユーザーからのメッセージに対して、最大5回のやり取りを行う
        /// 各エージェントの応答を逐次的にコールバックで通知する
        /// </summary>
        /// <param name="input">ユーザーからのメッセージ</param>
        /// <param name="onMessageReceived">エージェントが応答するたびに呼び出されるコールバック（エージェント名, メッセージHTML）</param>
        public async Task RunGroupChat(string input, Func<string, string, Task> onMessageReceived)
        {
            var log = _logger.CreateLogger("AgentFrameworkLogic.GroupChat");
            log.LogInformation("GroupChat input : {}", input);

            using var operation = _telemetryClient.StartOperation<Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry>("AgentFrameworkLogic.RunGroupChat");
            operation.Telemetry.Type = "AI Agent GroupChat";
            operation.Telemetry.Data = input;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // グループチャットワークフローを構築
                // RoundRobinGroupChatManagerを使用して、最大5回のターンでエージェント間の会話を管理
                var workflow = AgentWorkflowBuilder
                    .CreateGroupChatBuilderWith(agents =>
                        new RoundRobinGroupChatManager(agents)
                        {
                            MaximumIterationCount = 5  // 最大5回のターン
                        })
                    .AddParticipants(_agent, _mizukiAgent)
                    .Build();

                // ユーザー入力を共有チャット履歴に追加
                _sharedChatStore.AddMessage(ChatRole.User, input);

                // 初期メッセージを設定
                var messages = new List<ChatMessage>
                {
                    new(ChatRole.User, input)
                };

                // Markdown→HTML変換用パイプライン
                var pipeline = new MarkdownPipelineBuilder()
                    .UseAdvancedExtensions()
                    .UseAutoLinks()
                    .UseBootstrap()
                    .UseDiagrams()
                    .UseGridTables()
                    .UseEmojiAndSmiley()
                    .UseAlertBlocks()
                    .Build();

                // ストリーミング実行 - 各エージェントの完了を逐次的に取得
                StreamingRun run = await InProcessExecution.StreamAsync(workflow, messages);
                await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

                // 各エージェントの応答テキストを蓄積する辞書
                var agentResponses = new Dictionary<string, System.Text.StringBuilder>();

                // 各エージェントからの応答を監視
                await foreach (WorkflowEvent evt in run.WatchStreamAsync().ConfigureAwait(false))
                {
                    if (evt is AgentRunUpdateEvent update)
                    {
                        // エージェントのストリーミングチャンクを蓄積
                        var agentName = update.ExecutorId;
                        if (!agentResponses.ContainsKey(agentName))
                        {
                            agentResponses[agentName] = new System.Text.StringBuilder();
                        }
                        
                        AgentRunResponse response = update.AsResponse();
                        foreach (ChatMessage message in response.Messages)
                        {
                            agentResponses[agentName].Append(message.Text ?? "");
                        }
                    }
                    else if (evt is ExecutorCompletedEvent completed)
                    {
                        // エージェントの実行が完了 - 蓄積したテキストを出力
                        var agentName = completed.ExecutorId;
                        log.LogInformation("ExecutorCompletedEvent - ExecutorId: {}", agentName);
                        
                        // エージェント名からほのか/みずきを判定（大文字小文字を無視して部分一致で判定）
                        var displayName = agentName.Contains("Honoka", StringComparison.OrdinalIgnoreCase) ? "ほのか" 
                                        : agentName.Contains("Mizuki", StringComparison.OrdinalIgnoreCase) ? "みずき"
                                        : agentName; // 不明な場合はそのまま表示

                        if (agentResponses.TryGetValue(agentName, out var responseBuilder))
                        {
                            var fullText = responseBuilder.ToString();
                            if (!string.IsNullOrWhiteSpace(fullText))
                            {
                                // メッセージテキストのみMarkdown変換
                                var messageHtml = Markdown.ToHtml(fullText, pipeline);

                                log.LogInformation("[{}]: {}", displayName, fullText);

                                // グループチャットの会話履歴を共有ストアに追加
                                _sharedChatStore.AddMessage(ChatRole.Assistant, $"[{displayName}]: {fullText}");

                                // コールバックで通知（UIを更新）
                                await onMessageReceived(displayName, messageHtml);
                            }
                            // 次の会話のためにクリア
                            agentResponses.Remove(agentName);
                        }
                    }
                    else if (evt is WorkflowOutputEvent)
                    {
                        // ワークフロー完了
                        log.LogInformation("GroupChat workflow completed. Total messages in shared store: {}", _sharedChatStore.MessageCount);
                        break;
                    }
                }

                stopwatch.Stop();
                operation.Telemetry.Duration = stopwatch.Elapsed;
                operation.Telemetry.Success = true;

                _telemetryClient.TrackMetric("AgentFrameworkLogic.GroupChat.ResponseTime", stopwatch.ElapsedMilliseconds);
                _telemetryClient.TrackEvent("AgentFrameworkLogic.GroupChat.Success", new Dictionary<string, string>
                {
                    { "InputLength", input.Length.ToString() },
                    { "DurationMs", stopwatch.ElapsedMilliseconds.ToString() }
                });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                operation.Telemetry.Success = false;
                operation.Telemetry.Duration = stopwatch.Elapsed;

                _telemetryClient.TrackException(ex, new Dictionary<string, string>
                {
                    { "Operation", "AgentFrameworkLogic.RunGroupChat" },
                    { "Input", input },
                    { "DurationMs", stopwatch.ElapsedMilliseconds.ToString() }
                });

                log.LogError(ex, "グループチャット実行中にエラーが発生しました");
                // エラー時もコールバックで通知
                await onMessageReceived("ほのか", $"<p>ごめんなさい、グループチャットでエラーが発生したよ〜😢: {ex.Message}</p>");
            }
        }

        /// <summary>
        /// ユーザーからのメッセージを追加（応答生成なし）
        /// 通常の発言時にチャット履歴に追加するために使用
        /// </summary>
        /// <param name="input">ユーザーからのメッセージ文字列</param>
        public void NonGenerateMessage(string input)
        {
            // 共有チャット履歴ストアにユーザーメッセージを追加
            // これにより、AIに話しかけなくても会話の文脈が保持される
            _sharedChatStore.AddMessage(ChatRole.User, input);
            
            var log = _logger.CreateLogger("AgentFrameworkLogic");
            log.LogInformation("NonGenerateMessage added to history: {}", input);
        }

        /// <summary>
        /// アシスタント（ほのか等）からのメッセージを追加（応答生成なし）
        /// ネタ帳からの発言など、AI生成ではないアシスタントメッセージに使用
        /// </summary>
        /// <param name="message">アシスタントからのメッセージ文字列</param>
        public void AddAssistantMessage(string message)
        {
            // 共有チャット履歴ストアにアシスタントメッセージを追加
            _sharedChatStore.AddMessage(ChatRole.Assistant, message);
            
            var log = _logger.CreateLogger("AgentFrameworkLogic");
            log.LogInformation("AddAssistantMessage added to history: {}", message);
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
