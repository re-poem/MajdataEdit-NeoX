using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using System;

namespace MajdataEdit_NeoX.Models.Fumen;

public sealed class FumenCompletionItem(string text, object? content = null, object? description = null) : ICompletionData
{
    public IImage? Image => null;
    public string Text => text;
    public object Content => content ?? text;
    public object? Description => description;
    public double Priority => 0;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, text);
    }
}