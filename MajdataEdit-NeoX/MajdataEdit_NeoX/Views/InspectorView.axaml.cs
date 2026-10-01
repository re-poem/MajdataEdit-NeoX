using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using PropertyGenerator.Avalonia;

namespace MajdataEdit_NeoX.Views;

public partial class InspectorView : UserControl
{

    // ====== 显示态（one-way）======

    [GeneratedDirectProperty(DefaultBindingMode = BindingMode.OneWay)]
    public partial string DisplayTime { get; set; } = "0:00";

    [GeneratedDirectProperty(DefaultBindingMode = BindingMode.OneWay)]
    public partial string DisplayLineComboText { get; set; } = "L 1  Cb 0";

    [GeneratedDirectProperty(DefaultBindingMode = BindingMode.OneWay)]
    public partial bool IsConnected { get; set; }




    // ====== 索引 / 元数据 ======

    [GeneratedDirectProperty(DefaultBindingMode = BindingMode.TwoWay)]
    public partial int SelectedDifficulty { get; set; }

    [GeneratedDirectProperty(DefaultBindingMode = BindingMode.TwoWay)]
    public partial string? Level { get; set; }

    [GeneratedDirectProperty(DefaultBindingMode = BindingMode.TwoWay)]
    public partial string? Designer { get; set; }

    [GeneratedDirectProperty(DefaultBindingMode = BindingMode.TwoWay)]
    public partial float Offset { get; set; }




    // ====== 播放态 ======

    [GeneratedDirectProperty(DefaultBindingMode = BindingMode.TwoWay)]
    public partial float PlaybackSpeed { get; set; } = 1f;

    [GeneratedDirectProperty(DefaultBindingMode = BindingMode.TwoWay)]
    public partial bool IsFollowCursor { get; set; }



    public InspectorView()
    {
        InitializeComponent();
    }
}