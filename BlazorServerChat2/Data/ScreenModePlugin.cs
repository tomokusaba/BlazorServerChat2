using BlazorServerChat2;
using BlazorServerChat2.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components.DesignTokens;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace BlazorApp31.Plugin;

public class ScreenModePlugin()
{

    [KernelFunction, Description("画面モードを教えてください。")]
    public string GetScreenMode()
    {
        return MainLayout.DarkMode ? "ダークモード" : "ライトモード";
    }

    [KernelFunction, Description("今日は何月何日？。")]
    public string GetToday()
    {
        return DateTime.Now.ToString("M月d日");
    }

    [KernelFunction, Description("今日は何曜日？。")]
    public string GetDayOfWeek()
    {
        return DateTime.Now.ToString("dddd");
    }

    [KernelFunction, Description("現在時刻は？。")]
    public string GetTime()
    {
        return DateTime.Now.ToString("HH時mm分ss秒");
    }

    [KernelFunction, Description("Muse 大賞の部門は何がある？")]
    public string GetMuseAward()
    {
        return "Muse 大賞には、カリオペ賞（技術部門）、エウテルペ賞（芸術部門）、エラトー賞（ソロ部門）、ポリュームニア賞（アンサンブル部門）、ウーラニア賞（作曲部門）、メルポメーネ賞（感涙部門）、タレイア賞（高揚部門）、テルプシコラ賞（演出部門）、クレイオ賞（新人部門）があります。";
    }

    [KernelFunction, Description("カリオペ賞（技術部門）のノミネート作品")]
    public string GetMuseAwardNominee()
    {
        return "交響曲第２番（エルガー）,つぼみ,レトロ,歌劇「運命の力」序曲";
    }

    [KernelFunction, Description("エウテルペ賞（芸術部門）のノミネート作品")]
    public string GetMuseAwardNominee2()
    {
        return "プニャーニの様式による前奏曲とアレグロ,ツァラトゥストラはかく語りき,歌劇「ピーター・グライムズ」より　４つの海の間奏曲,イスラメイ";
    }

    [KernelFunction, Description("エラトー賞（ソロ部門）のノミネート作品")]
    public string GetMuseAwardNominee3()
    {
        return "アメージング・グレース,Ｐａｒａｇｏｎ　Ｒａｇ,花鳥風月,サクソフォーン四重奏曲（デザンクロ）";
    }

    [KernelFunction, Description("ポリュームニア賞（アンサンブル部門）のノミネート作品")]
    public string GetMuseAwardNominee4()
    {
        return "ラ・ペリ,交響曲第４番（マーラー）,ラバースコンチェルト,スピリティッド・アウェイ";
    }

    [KernelFunction, Description("ウーラニア賞（作曲部門）のノミネート作品")]
    public string GetMuseAwardNominee5()
    {
        return "瞳の中の笑顔,Ｍｏｔｈｅｒ　Ｎａｔｕｒｅ";
    }

    [KernelFunction, Description("メルポメーネ賞（感涙部門）のノミネート作品")]
    public string GetMuseAwardNominee6()
    {
        return "川の流れのように,愛は勝つ,遠くで汽笛を聞きながら";
    }

    [KernelFunction, Description("タレイア賞（高揚部門）のノミネート作品")]
    public string GetMuseAwardNominee7()
    {
        return "ピアノ協奏曲第２番（プロコフィエフ）,アイドル,ターコイズ,バック・トゥー・ザ・フューチャー";
    }

    [KernelFunction, Description("テルプシコラ賞（演出部門）のノミネート作品")]
    public string GetMuseAwardNominee8()
    {
        return "ミサ曲ロ短調（バッハ）,エンジェルドリーム";
    }

    [KernelFunction, Description("クレイオ賞（新人部門）のノミネート作品")]
    public string GetMuseAwardNominee9()
    {
        return "該当作品なし";
    }
}
