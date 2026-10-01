using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.TextMate;
using CommunityToolkit.Mvvm.ComponentModel;
using MajdataEdit_NeoX.Models.Fumen;
using PropertyGenerator.Avalonia;
using System;
using System.IO;
using System.Reflection.Metadata;
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
    }


    [GeneratedStyledProperty(DefaultBindingMode = BindingMode.TwoWay)]
    public partial string? Fumen { get; set; }


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
}