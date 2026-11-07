using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.PropertyGrid.Services;
using MajdataEdit_NeoX.Lang;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MajdataEdit_NeoX.Views;

/// <summary>
/// BPM 测速。照 Neo 的 <c>BpmTapWindow</c> 搬过来：连点算两次点击的间隔，
/// 攒够样本后去掉一个最快和一个最慢再取平均，显示一位小数。
///
/// 不需要 ViewModel——状态只属于这个面板，也不跟谱面交互，所以就是一个纯 Tool 视图。
/// </summary>
public partial class BpmTapView : UserControl
{
    private readonly List<double> _bpms = [];
    private DateTime _lastTap = DateTime.MinValue;

    public BpmTapView()
    {
        InitializeComponent();

        // 上树之后再聚焦，空格/回车能直接触发 Tap。
        AttachedToVisualTree += (_, _) => TapButton.Focus();
    }

    private void TapButton_Click(object? sender, RoutedEventArgs e)
    {
        var now = DateTime.Now;
        if (_lastTap != DateTime.MinValue)
            _bpms.Add(60d / (now - _lastTap).TotalSeconds);

        _lastTap = now;
        if (_bpms.Count == 0) return;

        // 样本够多时剔掉一个极端快、一个极端慢，剩下取平均。
        if (_bpms.Count > 20)
        {
            _bpms.Remove(_bpms.Min());
            _bpms.Remove(_bpms.Max());
        }

        TapText.Content = _bpms.Average().ToString("N1");
    }

    private void ResetButton_Click(object? sender, RoutedEventArgs e)
    {
        _bpms.Clear();
        _lastTap = DateTime.MinValue;
        TapText.Content = LocalizationService.Default[LangKeys.Gui_Tap];
    }
}
