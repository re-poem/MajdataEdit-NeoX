using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.PropertyGrid.Controls;
using Avalonia.PropertyGrid.Localization;
using System;
using System.Collections.Generic;
using System.Text;

namespace MajdataEdit_NeoX.Controls;


// 这里为什么不直接对原版PropertyGrid.CustomNameBlock绑定？
// 实际上，PropertyGrid也许是一个嵌套结构，子元素的子元素需要另一个PropertyGrid来装
// 而在装的这个过程中，PropertyGrid会新建一个自己的实例，而CustomNameBlock事件并不会继承
// 所以需要自己搞个控件出来
// 我确实需要吐槽一下，明明文档已经说了属性名的本地化不需要我们自己考虑的。
//
public class LocalizedPropertyGrid : PropertyGrid
{
    public LocalizedPropertyGrid()
    {
        CustomNameBlock += OnCustomNameBlock;
    }

    private void OnCustomNameBlock(object? sender, RoutedEventArgs e)
    {
        if (e is not CustomNameBlockEventArgs args) return;

        var tb = new TextBlock
        {
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        tb.SetLocalizeBinding(TextBlock.TextProperty, args.Context.Property.DisplayName);
        args.CustomNameBlock = tb;
    }
}