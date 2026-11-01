using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.PropertyGrid.Services;
using MajdataEdit_NeoX.Lang;
using System;
using System.Threading.Tasks;

namespace MajdataEdit_NeoX.Services;

/// <summary>
/// <see cref="IMessageService"/> 的 Avalonia 实现：在 <see cref="TopLevel"/> 的
/// <c>OverlayLayer</c> 上盖一层遮罩 + 卡片，只有一个「确定」按钮。
///
/// 不用 <see cref="Window"/> 做对话框：Android 上根本没有窗口实现
/// （<c>IWindowingPlatform.CreateWindow</c> 直接抛 <c>NotSupportedException</c>），
/// 而 <c>OverlayLayer</c> 在窗口、单视图、浏览器里都成立。
///
/// 和 <see cref="AvaloniaFileDialogService"/> 一样按需取 <see cref="TopLevel"/>：
/// 组合根构造本服务时视图还没挂到 visual tree。
/// </summary>
public sealed class AvaloniaMessageService(Func<TopLevel?> topLevelAccessor) : IMessageService
{
    public async Task ShowAsync(string title, string message)
    {
        var topLevel = topLevelAccessor();
        // 设计时 / 还没挂到 visual tree：没有能承载遮罩的宿主，静默降级。
        if (topLevel is null) return;
        if (OverlayLayer.GetOverlayLayer(topLevel) is not { } overlay) return;

        var completion = new TaskCompletionSource();
        var card = BuildCard(title, message, () => completion.TrySetResult());

        overlay.Children.Add(card);
        try
        {
            await completion.Task;
        }
        finally
        {
            overlay.Children.Remove(card);
        }
    }

    private static Control BuildCard(string title, string message, Action close)
    {
        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };
        titleText.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("SystemControlForegroundBaseHighBrush"));

        var messageText = new TextBlock
        {
            Text = message,
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        messageText.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("SystemControlForegroundBaseHighBrush"));

        var ok = new Button
        {
            Content = LocalizationService.Default[LangKeys.Gui_Ok],
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 88,
            Margin = new Thickness(0, 16, 0, 0),
            IsDefault = true,
        };
        ok.Click += (_, _) => close();

        var panel = new StackPanel();
        panel.Children.Add(titleText);
        panel.Children.Add(messageText);
        panel.Children.Add(ok);

        var card = new Border
        {
            Child = panel,
            Padding = new Thickness(20),
            Margin = new Thickness(24),
            MaxWidth = 360,
            CornerRadius = new CornerRadius(8),
            BoxShadow = BoxShadows.Parse("0 4 16 0 #55000000"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        card.Bind(Border.BackgroundProperty, new DynamicResourceExtension("SystemControlBackgroundAltHighBrush"));

        // 遮罩自身要有背景才拦得住底下编辑器的触摸；点遮罩或按 Esc / Enter 都能关。
        var scrim = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#99000000")),
            Child = card,
        };
        scrim.PointerPressed += (_, e) =>
        {
            // 只有点在卡片之外才关，点在卡片里交给卡片自己处理。
            if (!ReferenceEquals(e.Source, scrim)) return;
            close();
        };
        scrim.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Escape or Key.Enter)
            {
                e.Handled = true;
                close();
            }
        };
        scrim.AttachedToVisualTree += (_, _) => ok.Focus();

        return scrim;
    }
}
