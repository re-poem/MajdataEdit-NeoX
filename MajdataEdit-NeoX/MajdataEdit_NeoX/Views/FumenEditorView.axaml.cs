using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using CommunityToolkit.Mvvm.ComponentModel;
using MajdataEdit_NeoX.Base;
using MajdataEdit_NeoX.Input;
using MajdataEdit_NeoX.Models.Fumen;
using MajdataEdit_NeoX.Services;
using MajdataEdit_NeoX.Services.TextMarkers;
using MajSimai.Diagnostics;
using PropertyGenerator.Avalonia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;
using TextMateSharp.Grammars;

namespace MajdataEdit_NeoX.Views;

public partial class FumenEditorView : UserControl, IEditorService
{
    public FumenEditorView()
    {
        InitializeComponent();

        Editor.Options.HighlightCurrentLine = true;
        Editor.Options.EnableTextDragDrop = true;
        InputMethod.SetIsInputMethodEnabled(Editor.TextArea, false);

        var install = TextMate.InstallTextMate(Editor, new RegistryOptions(ThemeName.DarkPlus));
        install.SetGrammarFile(MajEnv.GrammarFile);

        Editor.TextArea.TextEntered += TextArea_TextEntered;
        Editor.TextArea.AddHandler(KeyDownEvent, TextArea_PreviewKeyDown, RoutingStrategies.Tunnel);

        Editor.TextArea.Caret.PositionChanged += (_, _) => CaretOffset = Editor.TextArea.Caret.Offset;

        Editor.TextArea.AddHandler(PointerWheelChangedEvent, TextArea_PointerWheelChanged, RoutingStrategies.Tunnel);

        // 诊断下划线：marker 服务跟文档绑定，外部换 Fumen 时随新文档重建
        AttachMarkerService(Editor.Document);
        Editor.TextArea.TextView.ScrollOffsetChanged += (_, _) => CloseMarkerToolTip();
        Editor.TextArea.PointerMoved += TextArea_PointerMoved;
        Editor.TextArea.PointerExited += (_, _) => CloseMarkerToolTip();
    }


    [GeneratedStyledProperty(DefaultBindingMode = BindingMode.TwoWay)]
    public partial string? Fumen { get; set; }


    [GeneratedStyledProperty(DefaultBindingMode = BindingMode.OneWay)]
    public partial IReadOnlyList<SimaiDiagnostic>? Diagnostics { get; set; }
    partial void OnDiagnosticsPropertyChanged(IReadOnlyList<SimaiDiagnostic>? newValue) =>
        RebuildMarkers();


    [GeneratedStyledProperty(DefaultBindingMode = BindingMode.TwoWay)]
    public partial double EditorFontSize { get; set; }
    partial void OnEditorFontSizePropertyChanged(double newValue) =>
        Editor.FontSize = newValue;

    [GeneratedStyledProperty(DefaultBindingMode = BindingMode.TwoWay)]
    public partial bool WordWrap { get; set; }


    [GeneratedStyledProperty(DefaultBindingMode = BindingMode.TwoWay)]
    public partial int CaretOffset { get; set; }
    partial void OnCaretOffsetPropertyChanged(int newValue)
    {
        if (Editor.TextArea.Caret.Offset != newValue)
        {
            Editor.TextArea.Caret.Offset = Math.Clamp(newValue,
                0, Editor.Document.TextLength);
            Editor.TextArea.Caret.BringCaretToView();
            Editor.TextArea.Caret.Show();
        }
    }


    private TextDocument? _doc;
    private bool _pushing;

    // 外部改了：换一个新文档
    partial void OnFumenPropertyChanged(string? newValue)
    {
        if (_pushing) return;
        _doc?.TextChanged -= OnDocChanged;
        _doc = new TextDocument(newValue ?? string.Empty);
        Editor.Document = _doc;
        _doc.TextChanged += OnDocChanged;
        // 文档换了，marker 服务也得跟着换（旧服务绑定在旧文档上）
        AttachMarkerService(_doc);
    }
    // 编辑器内改了：写回 Fumen
    private void OnDocChanged(object? s, EventArgs e)
    {
        _pushing = true;
        try
        {
            Fumen = _doc!.Text;
        }
        finally
        {
            _pushing = false;
        }
    }


    // ======== 诊断下划线 ========

    private TextMarkerService _markerService = null!;
    private ITextMarker? _tooltipMarker;



    private void AttachMarkerService(TextDocument doc)
    {
        _markerService?.RemoveFromTextView(Editor.TextArea.TextView);
        _markerService = new TextMarkerService(doc);
        _markerService.AddToTextView(Editor.TextArea.TextView);
        RebuildMarkers();
    }

    // 按最新一份诊断重建全部下划线；同一位置的多条诊断合并到一个 marker 的 tooltip 里。
    // SimaiDiagnostic 只带 1-based 的 Line/Column 和出错内容 Content，没有偏移量，
    // 这里换算成文档 offset，Content 能对上就按它的长度画，对不上至少画一个字符。
    private void RebuildMarkers()
    {
        if (_markerService is null) return;
        CloseMarkerToolTip();
        _markerService.RemoveAll();

        var doc = Editor.Document;
        var diagnostics = Diagnostics;
        if (diagnostics is null) return;

        foreach (var group in diagnostics
            .Select(d => (Diag: d, Offset: LineColumnToOffset(doc, d.Line, d.Column)))
            .Where(t => t.Offset >= 0)
            .GroupBy(t => t.Offset))
        {
            var offset = group.Key;
            var first = group.First().Diag;
            var len = 1;
            if (!string.IsNullOrEmpty(first.Content) &&
                offset + first.Content.Length <= doc.TextLength &&
                doc.GetText(offset, first.Content.Length) == first.Content)
            {
                len = first.Content.Length;
            }

            var marker = _markerService.Create(offset, len);
            marker.MarkerType = TextMarkerType.SquigglyUnderline;
            marker.MarkerColor = DiagnosticPalette.Of(first.Severity);
            marker.ToolTip = string.Join("\n", group.Select(t => t.Diag.Message));
        }
    }

    // 行列（1-based，列允许指向行尾）转 offset；行越界返回 -1
    private static int LineColumnToOffset(TextDocument doc, int line, int column)
    {
        if (line < 1 || line > doc.LineCount)
            return -1;
        var docLine = doc.GetLineByNumber(line);
        return docLine.Offset + Math.Clamp(column, 1, docLine.Length + 1) - 1;
    }

    // 悬停到带下划线的文本上时弹出 tooltip
    private void TextArea_PointerMoved(object? sender, PointerEventArgs e)
    {
        var textView = Editor.TextArea.TextView;
        var marker = HitTestMarker(e.GetPosition(textView));
        if (ReferenceEquals(marker, _tooltipMarker))
            return;

        _tooltipMarker = marker;
        if (marker is null)
        {
            ToolTip.SetIsOpen(textView, false);
            return;
        }
        ToolTip.SetTip(textView, marker.ToolTip);
        ToolTip.SetShowDelay(textView, 0);
        ToolTip.SetIsOpen(textView, true);
    }

    private ITextMarker? HitTestMarker(Point pos)
    {
        if (Editor.TextArea.TextView.GetPosition(pos) is not { } position)
            return null;
        var offset = LineColumnToOffset(Editor.Document, position.Line, position.Column);
        if (offset < 0)
            return null;
        return _markerService.GetMarkersAtOffset(offset).FirstOrDefault();
    }

    private void CloseMarkerToolTip()
    {
        ToolTip.SetIsOpen(Editor.TextArea.TextView, false);
        _tooltipMarker = null;
    }


    // Completion
    void TextArea_TextEntered(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text)) return;
        var items = Completions.Get(e.Text);
        if (items.Length == 0) return;

        var window = new CompletionWindow(Editor.TextArea);
        foreach (var item in items)
            window.CompletionList.CompletionData.Add(item);
        window.Show();
    }

    // 接管狗屎 AvaloniaEdit 的部分按键行为
    private void TextArea_PreviewKeyDown(object? sender, KeyEventArgs e)
    {
        var area = Editor.TextArea;
        var hasCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var hasShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down))
            return;

        if (!hasShift && !area.Selection.IsEmpty)
        {
            // 无 Shift 的方向键先把选区塌缩到对应的边界：左/上取起点，右/下取终点
            var segment = area.Selection.SurroundingSegment;
            area.Caret.Offset = e.Key is Key.Left or Key.Up ? segment.Offset : segment.EndOffset;
            area.ClearSelection();
            e.Handled = true;
        }
        else if (hasCtrl)
        {
            ExecuteKey((hasShift, e.Key) switch
            {
                (false, Key.Left) => EditingCommands.MoveLeftByCharacter,
                (false, Key.Right) => EditingCommands.MoveRightByCharacter,
                (false, Key.Up) => EditingCommands.MoveUpByLine,
                (false, _) => EditingCommands.MoveDownByLine,
                (true, Key.Left) => EditingCommands.SelectLeftByCharacter,
                (true, Key.Right) => EditingCommands.SelectRightByCharacter,
                (true, Key.Up) => EditingCommands.SelectUpByLine,
                _ => EditingCommands.SelectDownByLine,
            }, area, e);
            e.Handled = true;
        }

        area.Caret.BringCaretToView();
    }
    private static void ExecuteKey(AvaloniaEdit.RoutedCommand command, TextArea area, KeyEventArgs e)
    {
        if (!command.CanExecute(null, area))
            return;

        command.Execute(null, area);
        e.Handled = true;
    }


    private void TextArea_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;

        EditorFontSize = Math.Clamp(EditorFontSize + Math.Sign(e.Delta.Y), 1, 100);
        e.Handled = true;
    }


    public string GetSelectedText() => Editor.SelectedText;
    public void SetSelectedText(string value) => Editor.SelectedText = value;
}
