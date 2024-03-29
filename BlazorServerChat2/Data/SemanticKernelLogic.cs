﻿using BlazorApp31.Plugin;
using Markdig;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Build.Logging;
using Microsoft.IdentityModel.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletion;
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
            KernelBuilder builder = new();
            builder.Services.AddAzureOpenAIChatCompletion(deploymentName, serviceId, baseUrl, key);
            //.WithAzureChatCompletionService(deploymentName, baseUrl, key)
            builder.Plugins.AddFromType<ScreenModePlugin>();

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
            };

            OpenAIPromptExecutionSettings? setting2 = new()
            {
                FunctionCallBehavior = FunctionCallBehavior.AutoInvokeKernelFunctions
            };

            var result = await kernel.InvokePromptAsync(input, new(setting2));

            string reply = await GptChat4.GetChatMessageContentAsync(chatHistory, setting);
            if (result != null)
            {
                log.LogInformation("result : {}", result);
                reply = result.ToString();
            }
            log.LogInformation("reply : {}", reply);
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseAutoLinks().UseBootstrap().UseDiagrams().UseGridTables().Build();
            var htmlReply = Markdown.ToHtml(reply, pipeline);
            log.LogInformation("htmlReply : {}", htmlReply);
            chatHistory.AddAssistantMessage(reply);
            return htmlReply;
        }

    }
}
