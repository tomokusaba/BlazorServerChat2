using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace BlazorServerChat2.Data
{
    public class WeatherPlugin(HttpClient client)
    {
        private readonly HttpClient _client = client;

        [KernelFunction, Description("""
        天気を取得する場所コードを取得します。
        対応している場所コードは下記のとおりです。
        下記に含まれない場所の場合は近くの場所の天気を代わりに取得してください。
        ---
        東京
        横浜
        名古屋
        京都
        静岡
        福井
        新潟
        富山
        金沢
        岐阜
        長野
        高山
        松本
        大津
        大阪
        札幌
        仙台
        福岡
        那覇
        """)]
        public static int GetPlaceId([Description("天気を取得する場所")] string place)
        {
            var res = place switch
            {
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
                "札幌" => 016000,
                "仙台" => 040000,
                "福岡" => 400000,
                "那覇" => 471000,
                _ => throw new ArgumentException("対応していない地域です。")
            };
            return res;
        }

        [KernelFunction, Description("場所コードの地域の天気を返す")]
        public async Task<string> Weather([Description("場所コード")] int place)
        {
            return (await _client.GetAsync($"https://www.jma.go.jp/bosai/forecast/data/forecast/{place}.json")).Content.ReadAsStringAsync().Result;
        }

    }
}
