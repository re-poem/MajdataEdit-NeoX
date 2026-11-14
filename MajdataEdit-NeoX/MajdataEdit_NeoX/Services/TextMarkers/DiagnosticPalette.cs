using Avalonia.Media;
using MajSimai.Diagnostics;

namespace MajdataEdit_NeoX.Services.TextMarkers;

/// <summary>VS（DarkPlus 主题）风格的诊断配色。</summary>
public static class DiagnosticPalette
{
    public static readonly Color Error = Color.Parse("#F14C4C");
    public static readonly Color Warning = Color.Parse("#CCA700");
    public static readonly Color Information = Color.Parse("#3794FF");

    /// <summary>按严重级别取色，未知级别按 Information 处理。</summary>
    public static Color Of(SimaiDiagnosticSeverity severity) => severity switch
    {
        SimaiDiagnosticSeverity.Error => Error,
        SimaiDiagnosticSeverity.Warning => Warning,
        _ => Information,
    };

    /// <summary>字符串版（XAML 转换器用），避免转换器依赖枚举成员名。</summary>
    public static Color OfString(string? severity) => severity switch
    {
        "Error" => Error,
        "Warning" => Warning,
        _ => Information,
    };
}
