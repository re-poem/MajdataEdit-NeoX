using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;

namespace MajdataEdit_NeoX.Services.TextMarkers;

/// <summary>
/// <see cref="ITextMarker"/> 的默认实现：挂在 <see cref="TextSegmentCollection{T}"/>
/// 里跟着文档走（插入/删除时自动平移、被删文本上的标记自动失效）。
/// </summary>
public sealed class TextMarker : TextSegment, ITextMarker
{
    internal TextMarker(TextMarkerService service, int startOffset, int length)
    {
        Service = service;
        StartOffset = startOffset;
        Length = length;
    }

    /// <summary>所属服务，删除时回调。</summary>
    internal TextMarkerService Service { get; }

    public TextMarkerType MarkerType { get; set; } = TextMarkerType.SquigglyUnderline;

    public Color? MarkerColor { get; set; }

    public string? ToolTip { get; set; }

    /// <summary>标记随文档编辑被整体删除时由服务置 true。</summary>
    public bool IsDeleted { get; internal set; }

    public void Delete() => Service.Remove(this);
}
