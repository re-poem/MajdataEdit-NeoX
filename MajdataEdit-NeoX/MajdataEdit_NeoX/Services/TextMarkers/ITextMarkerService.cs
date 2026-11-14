using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using System;
using System.Collections.Generic;

namespace MajdataEdit_NeoX.Services.TextMarkers;

/// <summary>下划线的画法。</summary>
public enum TextMarkerType
{
    /// <summary>VS 风格的波浪下划线。</summary>
    SquigglyUnderline,

    /// <summary>普通的实线下划线。</summary>
    SolidUnderline,
}

/// <summary>
/// 文档上的一段标记（类似 VS 的 error tag）：
/// 位置随文档编辑自动平移，颜色决定下划线颜色，ToolTip 供悬停展示。
/// </summary>
public interface ITextMarker : ISegment
{
    TextMarkerType MarkerType { get; set; }

    Color? MarkerColor { get; set; }

    string? ToolTip { get; set; }

    bool IsDeleted { get; }

    void Delete();
}

/// <summary>
/// 仿 AvalonEdit 经典示例的 <c>ITextMarkerService</c>：
/// 管理文档上的全部标记，同时兼任背景渲染器把标记画到 TextView 上。
/// </summary>
public interface ITextMarkerService : IBackgroundRenderer, ITextViewConnect
{
    /// <summary>标记增删时触发。</summary>
    event EventHandler? Changed;

    IEnumerable<ITextMarker> Markers { get; }

    ITextMarker Create(int startOffset, int length);

    void Remove(ITextMarker marker);

    void RemoveAll();

    IEnumerable<ITextMarker> GetMarkersAtOffset(int offset);
}
