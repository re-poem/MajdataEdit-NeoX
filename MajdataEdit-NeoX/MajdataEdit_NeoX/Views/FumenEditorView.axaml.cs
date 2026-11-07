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
using AvaloniaEdit.TextMate;
using CommunityToolkit.Mvvm.ComponentModel;
using MajdataEdit_NeoX.Base;
using MajdataEdit_NeoX.Input;
using MajdataEdit_NeoX.Models.Fumen;
using MajdataEdit_NeoX.Services;
using PropertyGenerator.Avalonia;
using System;
using System.IO;
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
    }

    [GeneratedStyledProperty(DefaultBindingMode = BindingMode.TwoWay)]
    public partial string? Fumen { get; set; }


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
        var area = Editor.TextArea;
        var hasShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (area.Document is not { } doc)
            return;
        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down))
            return;

        // Ctrl 与方向键组合只是纯导航，不跳词、不跳段
        if (!hasShift && !area.Selection.IsEmpty)
        {
            // 无 Shift 的方向键先把选区塌缩到对应的边界：左/上取起点，右/下取终点
            var segment = area.Selection.SurroundingSegment;
            area.Caret.Offset = e.Key is Key.Left or Key.Up ? segment.Offset : segment.EndOffset;
            area.ClearSelection();
            e.Handled = true;

            // 左右在这里就结束了，上下还要再走一行
            if (e.Key is Key.Left or Key.Right)
            {
                area.Caret.BringCaretToView();
                return;
            }
        }

        // 首行再往上 = 行首，末行再往下 = 行尾
        if (e.Key == Key.Up && area.Caret.Line == 1)
        {
            MoveCaretTo(area, doc.GetLineByNumber(1).Offset, hasShift);
            e.Handled = true;
        }
        else if (e.Key == Key.Down && area.Caret.Line == doc.LineCount)
        {
            MoveCaretTo(area, doc.GetLineByNumber(doc.LineCount).EndOffset, hasShift);
            e.Handled = true;
        }
        else
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

        area.Caret.BringCaretToView();
    }

    private static void ExecuteKey(AvaloniaEdit.RoutedCommand command, TextArea area, KeyEventArgs e)
    {
        if (!command.CanExecute(null, area))
            return;

        command.Execute(null, area);
        e.Handled = true;
    }

    private static void MoveCaretTo(TextArea area, int offset, bool extend)
    {
        if (extend)
        {
            var position = new TextViewPosition(area.Document.GetLocation(offset));
            area.Selection = area.Selection.StartSelectionOrSetEndpoint(area.Caret.Position, position);
            area.Caret.Offset = offset;
        }
        else
        {
            area.Caret.Offset = offset;
            area.ClearSelection();
        }
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