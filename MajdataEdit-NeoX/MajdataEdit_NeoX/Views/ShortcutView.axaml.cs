using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.PropertyGrid.Localization;
using MajdataEdit_NeoX.Input;
using System;

namespace MajdataEdit_NeoX.Views;

public partial class ShortcutView : UserControl
{
    public ShortcutView()
    {
        InitializeComponent();
    }
}

public sealed class LocalizedShortcutTextBlock : TextBlock
{
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is ShortcutDefinition definition)
        {
            this.SetLocalizeBinding(
                TextBlock.TextProperty,
                definition.FunctionKey);
        }
    }
}