using Azure.AI.OpenAI;
using BlazorApp31.Plugin;
using Markdig;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Build.Logging;
using Microsoft.IdentityModel.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using NuGet.Packaging.Core;
using System.Diagnostics.Metrics;

namespace BlazorServerChat2.Data
{
    public class SemanticKernelLogic
    {
        public Kernel kernel;
        public IChatCompletionService GptChat4 { get; set; }
        private readonly ILoggerFactory _logger;
        private readonly IConfiguration _configuration;
        private readonly TelemetryClient _telemetryClient;
        private ChatHistory chatHistory;

        /// <summary>
        /// kernelの初期化からOpenAIChatHistoryをインスタンス化するところまでやる
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="configuration"></param>
        public SemanticKernelLogic(ILoggerFactory logger, IConfiguration configuration, TelemetryClient telemetryClient)
        {
            _logger = logger;
            _configuration = configuration;
            _telemetryClient = telemetryClient;
            string serviceId = _configuration.GetValue<string>("Settings:ServiceId") ?? string.Empty;
            string deploymentName = _configuration.GetValue<string>("Settings:DeploymentName") ?? string.Empty;
            string baseUrl = _configuration.GetValue<string>("Settings:BaseUrl") ?? string.Empty;
            string key = _configuration.GetValue<string>("Settings:OpenAIKey") ?? string.Empty;
            var meterListener = new MeterListener();

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
            //    if (Instrument.Meter.Name.StartsWith("AzureChatCompletion", StringComparison.Ordinal))
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

            //kernel = new KernelBuilder().Configure(c =>
            //{
            //    c.AddAzureChatCompletionService( deploymentName, baseUrl, key);

            //}).WithLogger(_logger).Build();
            IKernelBuilder builder = Kernel.CreateBuilder();
            builder.AddAzureOpenAIChatCompletion(deploymentName, baseUrl, key);
            builder.Services.AddLogging(c => c.AddOpenTelemetry().SetMinimumLevel(LogLevel.Trace));
            //.WithAzureChatCompletionService(deploymentName, baseUrl, key)
            builder.Plugins.AddFromType<ScreenModePlugin>();
            builder.Plugins.AddFromType<WeatherPlugin>();
            builder.Services.AddScoped<HttpClient>();
            kernel = builder.Build();
            GptChat4 = kernel.GetRequiredService<IChatCompletionService>();

            chatHistory = new ChatHistory("あなたはほのかという名前のAIアシスタントです。くだけた女性の口調で人に役立つ回答をします。");

        }

        public void Clear()
        {
            chatHistory = new ChatHistory("あなたはほのかという名前のAIアシスタントです。くだけた女性の口調で人に役立つ回答をします。");

        }

        public void NonGenerateMessage(string input)
        {

            chatHistory.AddUserMessage(input);
        }

        /// <summary>
        /// ユーザからのメッセージを追加してChatGPTでメッセージ生成をする。
        /// メッセージ生成した結果をHTMLに変換して返す
        /// </summary>
        /// <param name="input">ユーザからのメッセージ文字列</param>
        /// <returns>ChatGPTのメッセージ生成しHTML変換した文字列</returns>
        public async Task<string> Run(string input)
        {
            var log = _logger.CreateLogger("SemanticKernelLogic");
            log.LogInformation("input : {}", input);
            chatHistory.AddUserMessage(input);
            var setting = new OpenAIPromptExecutionSettings()
            {
                MaxTokens = 2000,
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            };

#pragma warning disable SKEXP0010 // 種類は、評価の目的でのみ提供されています。将来の更新で変更または削除されることがあります。続行するには、この診断を非表示にします。
            OpenAIPromptExecutionSettings? setting2 = new()
            {
                //FunctionCallBehavior = FunctionCallBehavior.AutoInvokeKernelFunctions
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                ChatSystemPrompt = "あなたはほのかという名前のAIアシスタントです。くだけた女性の口調で人に役立つ回答をします。",
                MaxTokens = 2000,
            };
#pragma warning restore SKEXP0010 // 種類は、評価の目的でのみ提供されています。将来の更新で変更または削除されることがあります。続行するには、この診断を非表示にします。

            //FunctionResult result = await kernel.InvokePromptAsync(input, new(setting2));
            //FunctionResult result = null!;
            var reply = await GptChat4.GetChatMessageContentAsync(chatHistory, setting, kernel);
            //if (result != null)
            //{
            //    log.LogInformation("result : {}", result);
            //    reply.InnerContent = result.ToString();
            //}
            log.LogInformation("reply : {}", reply);
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseAutoLinks().UseBootstrap().UseDiagrams().UseGridTables().Build();
            var htmlReply = Markdown.ToHtml(reply.ToString(), pipeline);
            log.LogInformation("htmlReply : {}", htmlReply);
            chatHistory.AddAssistantMessage(reply.InnerContent?.ToString() ?? string.Empty);
            return htmlReply;
        }

    }
}
