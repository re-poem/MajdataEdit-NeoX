using Avalonia.Media;
using AvaloniaEdit.Document;

namespace MajdataEdit_NeoX.Managers.TextMarkers;

/// <summary>
/// <see cref="ITextMarker"/> 的默认实现：挂在 <see cref="TextSegmentCollection{T}"/>
/// 里跟着文档走（插入/删除时自动平移、被删文本上的标记自动失效）。
/// </summary>
public sealed class TextMarker : TextSegment, ITextMarker
{
    internal TextMarker(TextMarkerManager manager, int startOffset, int length)
    {
        Manager = manager;
        StartOffset = startOffset;
        Length = length;
    }

    /// <summary>所属管理器，删除时回调。</summary>
    internal TextMarkerManager Manager { get; }

    public TextMarkerType MarkerType { get; set; } = TextMarkerType.SquigglyUnderline;

    public Color? MarkerColor { get; set; }

    public string? ToolTip { get; set; }

    /// <summary>标记随文档编辑被整体删除时由管理器置 true。</summary>
    public bool IsDeleted { get; internal set; }

    public void Delete() => Manager.Remove(this);
}
