using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.TextMate;
using CommunityToolkit.Mvvm.ComponentModel;
using MajdataEdit_NeoX.Input;
using MajdataEdit_NeoX.Models.Fumen;
using PropertyGenerator.Avalonia;
using System;
using System.IO;
using System.Windows.Input;
using TextMateSharp.Grammars;

namespace MajdataEdit_NeoX.Views;

public partial class FumenEditorView : UserControl
{
    public FumenEditorView()
    {
        InitializeComponent();

        Editor.Options.HighlightCurrentLine = true;
        Editor.Options.EnableTextDragDrop = true;
        InputMethod.SetIsInputMethodEnabled(Editor.TextArea, false);

        var install = TextMate.InstallTextMate(Editor, new RegistryOptions(ThemeName.DarkPlus));
        install.SetGrammarFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "simai.tmLanguage.json"));

        Editor.TextArea.TextEntered += TextArea_TextEntered;
        Editor.TextArea.AddHandler(KeyDownEvent, TextArea_PreviewKeyDown, RoutingStrategies.Tunnel);

        Editor.TextArea.Caret.PositionChanged += (_, _) => CaretOffset = Editor.TextArea.Caret.Offset;
    }


    [GeneratedStyledProperty(DefaultBindingMode = BindingMode.TwoWay)]
    public partial string? Fumen { get; set; }


    [GeneratedStyledProperty(DefaultBindingMode = BindingMode.TwoWay)]
#pragma warning disable CS0108 // 成员隐藏继承的成员；缺少关键字 new
    public partial double FontSize { get; set; }
#pragma warning restore CS0108

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
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;

        var area = Editor.TextArea;
        var hasShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (!area.Selection.IsEmpty && !hasShift)
        {
            if (e.Key == Key.Right)
            {
                area.Caret.Offset = area.Selection.SurroundingSegment.EndOffset;
                area.ClearSelection();
                e.Handled = true;
            }
            else if (e.Key == Key.Left)
            {
                area.Caret.Offset = area.Selection.SurroundingSegment.Offset;
                area.ClearSelection();
                e.Handled = true;
            }
        }

        if (!hasShift)
        {
            if (e.Key == Key.Up && area.Caret.Line > 1)
            {
                var prevLine = Editor.Document.GetLineByNumber(area.Caret.Line - 1);
                var col = Math.Min(area.Caret.Column, prevLine.Length + 1);
                area.Caret.Offset = prevLine.Offset + Math.Max(0, col - 1);
                e.Handled = true;
            }
            else if (e.Key == Key.Down && area.Caret.Line < Editor.Document.LineCount)
            {
                var nextLine = Editor.Document.GetLineByNumber(area.Caret.Line + 1);
                var col = Math.Min(area.Caret.Column, nextLine.Length + 1);
                area.Caret.Offset = nextLine.Offset + Math.Max(0, col - 1);
                e.Handled = true;
            }
        }

        switch (e.Key)
        {
            case Key.Left when hasShift:
                area.Caret.Offset = Math.Max(0, area.Caret.Offset - 1);
                e.Handled = true; break;
            case Key.Right when hasShift:
                area.Caret.Offset = Math.Min(Editor.Document.TextLength, area.Caret.Offset + 1);
                e.Handled = true; break;
            case Key.Up when hasShift:
                if (area.Caret.Line > 1)
                {
                    var prevLine = Editor.Document.GetLineByNumber(area.Caret.Line - 1);
                    var col = Math.Min(area.Caret.Column, prevLine.Length + 1);
                    area.Caret.Offset = prevLine.Offset + Math.Max(0, col - 1);
                    e.Handled = true;
                }
                break;
            case Key.Down when hasShift:
                if (area.Caret.Line < Editor.Document.LineCount)
                {
                    var nextLine = Editor.Document.GetLineByNumber(area.Caret.Line + 1);
                    var col = Math.Min(area.Caret.Column, nextLine.Length + 1);
                    area.Caret.Offset = nextLine.Offset + Math.Max(0, col - 1);
                    e.Handled = true;
                }
                break;
            case Key.Left:
                area.Caret.Offset = Math.Max(0, area.Caret.Offset - 1);
                e.Handled = true; break;
            case Key.Right:
                area.Caret.Offset = Math.Min(Editor.Document.TextLength, area.Caret.Offset + 1);
                e.Handled = true; break;
        }
    }
}