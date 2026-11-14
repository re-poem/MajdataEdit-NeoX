using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MajdataEdit_NeoX.Services.TextMarkers;

/// <summary>
/// <see cref="ITextMarkerService"/> 的默认实现：标记放在
/// <see cref="TextSegmentCollection{T}"/> 里跟着文档走，
/// 并作为 <see cref="IBackgroundRenderer"/> 画在 <see cref="KnownLayer.Background"/>。
/// </summary>
public sealed class TextMarkerService : ITextMarkerService
{
    private readonly TextSegmentCollection<TextMarker> _markers;
    private readonly List<TextView> _textViews = [];

    public TextMarkerService(TextDocument document)
    {
        _markers = new TextSegmentCollection<TextMarker>(document);
    }

    public event EventHandler? Changed;

    public IEnumerable<ITextMarker> Markers => _markers;

    public ITextMarker Create(int startOffset, int length)
    {
        var marker = new TextMarker(this, startOffset, length);
        _markers.Add(marker);
        Redraw(marker);
        OnChanged();
        return marker;
    }

    public void Remove(ITextMarker marker)
    {
        if (marker is not TextMarker m || !_markers.Remove(m))
            return;
        Redraw(m);
        OnChanged();
    }

    public void RemoveAll()
    {
        foreach (var marker in _markers.ToArray())
        {
            _markers.Remove(marker);
            Redraw(marker);
        }
        OnChanged();
    }

    public IEnumerable<ITextMarker> GetMarkersAtOffset(int offset) =>
        _markers.FindSegmentsContaining(offset);

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private void Redraw(ISegment segment)
    {
        foreach (var textView in _textViews)
            textView.Redraw(segment.Offset, segment.EndOffset - segment.Offset);
    }

    // ===== IBackgroundRenderer =====

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid)
            return;
        var visualLines = textView.VisualLines;
        if (visualLines.Count == 0)
            return;

        // 只画视口内碰到的那部分标记
        var viewStart = visualLines[0].FirstDocumentLine.Offset;
        var viewEnd = visualLines[^1].LastDocumentLine.EndOffset;

        foreach (var marker in _markers.FindOverlappingSegments(viewStart, viewEnd - viewStart))
        {
            if (marker.MarkerColor is not { } color)
                continue;
            DrawUnderline(textView, marker, color, drawingContext);
        }
    }

    private static void DrawUnderline(TextView textView, TextMarker marker, Color color, DrawingContext drawingContext)
    {
        var brush = new SolidColorBrush(color);
        var pen = new Pen(brush, 1);
        foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, marker))
        {
            switch (marker.MarkerType)
            {
                case TextMarkerType.SolidUnderline:
                    drawingContext.FillRectangle(brush, new Rect(rect.X, rect.Bottom - 1, rect.Width, 1));
                    break;

                case TextMarkerType.SquigglyUnderline:
                default:
                    DrawSquiggly(drawingContext, pen, rect);
                    break;
            }
        }
    }

    /// <summary>
    /// VS 风格的波浪下划线：贴着行底部的锯齿波。
    /// 周期 3px、上下各偏一点，宽度不足一个周期时至少画一小段，保证零长标记也可见。
    /// </summary>
    private static void DrawSquiggly(DrawingContext drawingContext, Pen pen, Rect rect)
    {
        const double period = 3;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var top = rect.Bottom - 2;
            var bottom = rect.Bottom - 0.5;
            var y = top;

            var x = rect.X;
            var end = Math.Max(rect.Right, rect.X + period);
            ctx.BeginFigure(new Point(x, y), false);
            while (x < end)
            {
                x = Math.Min(x + period, end);
                y = y == top ? bottom : top;
                ctx.LineTo(new Point(x, y));
            }
        }
        drawingContext.DrawGeometry(null, pen, geometry);
    }

    // ===== ITextViewConnect =====

    public void AddToTextView(TextView textView)
    {
        if (_textViews.Contains(textView))
            return;
        _textViews.Add(textView);
        textView.BackgroundRenderers.Add(this);
    }

    public void RemoveFromTextView(TextView textView)
    {
        if (!_textViews.Remove(textView))
            return;
        textView.BackgroundRenderers.Remove(this);
    }
}
