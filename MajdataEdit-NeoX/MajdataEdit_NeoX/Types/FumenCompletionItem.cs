using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using System;

namespace MajdataEdit_NeoX.Types;

public sealed class FumenCompletionItem((string text, object? content, object? description) data) : ICompletionData
{
    public IImage? Image => null;
    public string Text => data.text;
    public object Content => data.content ?? data.text;
    public object? Description => data.description;
    public double Priority => 0;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, data.text);
    }
}