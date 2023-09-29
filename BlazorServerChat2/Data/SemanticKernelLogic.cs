using Markdig;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Build.Logging;
using Microsoft.IdentityModel.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.Tokenizers;
using System.Diagnostics.Metrics;

namespace BlazorServerChat2.Data
{
    public class SemanticKernelLogic
    {
        public IKernel kernel;
        public IChatCompletion GptChat4 { get; set; }
        private readonly ILoggerFactory _logger;
        private readonly IConfiguration _configuration;
        private readonly TelemetryClient _telemetryClient;
        private OpenAIChatHistory chatHistory;

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
            //string serviceId = _configuration.GetValue<string>("Settings:ServiceId") ?? string.Empty;
            string deploymentName = _configuration.GetValue<string>("Settings:DeploymentName") ?? string.Empty;
            string baseUrl = _configuration.GetValue<string>("Settings:BaseUrl") ?? string.Empty;
            string key = _configuration.GetValue<string>("Settings:OpenAIKey") ?? string.Empty;
            var meterListener = new MeterListener();

            meterListener.InstrumentPublished = (Instrument, listener) =>
            {
                if (Instrument.Meter.Name.StartsWith("Microsoft.SemanticKernel", StringComparison.Ordinal))
                {
                    listener.EnableMeasurementEvents(Instrument);
                }
                if (Instrument.Meter.Name.StartsWith("SemanticKernelLogic", StringComparison.Ordinal))
                {
                    listener.EnableMeasurementEvents(Instrument);
                }
                if (Instrument.Meter.Name.StartsWith("AzureChatCompletion", StringComparison.Ordinal))
                {
                    listener.EnableMeasurementEvents(Instrument);
                }
            };

            meterListener.SetMeasurementEventCallback<double>((instrument, measurment, tags, state) =>
            {
                _telemetryClient.GetMetric(instrument.Name).TrackValue(measurment);
            });

            meterListener.Start();

            

            _telemetryClient.StartOperation<DependencyTelemetry>("ApplicationInsights.Example");

            //kernel = new KernelBuilder().Configure(c =>
            //{
            //    c.AddAzureChatCompletionService( deploymentName, baseUrl, key);

            //}).WithLogger(_logger).Build();
            kernel = new KernelBuilder()
                .WithAzureChatCompletionService(deploymentName, baseUrl, key)
                .WithLoggerFactory(_logger)
                
                .Build();
            GptChat4 = kernel.GetService<IChatCompletion>();


            chatHistory = (OpenAIChatHistory)GptChat4.CreateNewChat("あなたはほのかという名前のAIアシスタントです。くだけた女性の口調で人に役立つ回答をします。");

        }

        public void Clear()
        {
            chatHistory = (OpenAIChatHistory)GptChat4.CreateNewChat("あなたはほのかという名前のAIアシスタントです。くだけた女性の口調で人に役立つ回答をします。");

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
            var setting = new ChatRequestSettings
            {
                Temperature = 0.8,
                MaxTokens = 2000,
                FrequencyPenalty = 0.5,

            };
            
            string reply = await GptChat4.GenerateMessageAsync(chatHistory, setting);
            log.LogInformation("reply : {}", reply);
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseAutoLinks().UseBootstrap().UseDiagrams().UseGridTables().Build();
            var htmlReply = Markdown.ToHtml(reply, pipeline);
            log.LogInformation("htmlReply : {}", htmlReply);
            chatHistory.AddAssistantMessage(reply);
            return htmlReply;
        }

    }
}
