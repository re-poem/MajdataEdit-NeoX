using Avalonia.PropertyGrid.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MajdataEdit_NeoX.Lang;
using System;
using System.Globalization;
using System.Threading;

namespace MajdataEdit_NeoX.ViewModels;

/// <summary>
/// 进度窗口的数据上下文（由 <see cref="Types.MessageBox.RunWithProgressAsync"/> 托管，调用方不直接碰）。
/// 关掉窗口或点取消等于取消这趟活，已做了一半的部分交给业务自己回滚或忽略。
///
/// 进度是「已做多少 / 一共多少」两个绝对值，跟调用方自己的单位（字节、条数……）一致；
/// 百分比只在这里算给显示用，业务侧不用自己换算。
/// </summary>
public partial class ProgressViewModel() : ObservableObject
{
    private readonly CancellationTokenSource _cancel = new();

    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;

    /// <summary>总数（与进度同一单位）。0 表示总量未知，进度条改走不确定态。</summary>
    public long Total { get; init; }

    /// <summary>把绝对值写成给人看的文本（字节数、条数都行）；不给就照原样打数字。</summary>
    public Func<long, string> Formatter { get; init; } = static value => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>总量未知时进度条只是转圈。</summary>
    public bool HasNoTotal => Total <= 0;

    public CancellationToken Token => _cancel.Token;

    [ObservableProperty]
    public partial long Progress { get; set; }

    [ObservableProperty]
    public partial string Detail { get; set; } = string.Empty;

    /// <summary>进度回调（后台线程）→ UI 线程更新绑定。参数是绝对值，跟 <see cref="Total"/> 同一单位。</summary>
    public void Report(long progress)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Progress = progress;
            Detail = Total > 0
                ? $"{Percent(progress)}% · {Formatter(progress)} / {Formatter(Total)}"
                : Formatter(progress);
        });
    }

    [RelayCommand]
    public void Cancel()
    {
        if (_cancel.IsCancellationRequested) return;

        _cancel.Cancel();
        Detail = LocalizationService.Default[LangKeys.Gui_Cancel] + "…";
    }

    /// <summary>0-100 的整数百分比；总量未知时算 0。</summary>
    private int Percent(long progress) =>
        Total > 0 ? (int)Math.Clamp(progress * 100 / Total, 0, 100) : 0;

    /// <summary>字节数的默认排版，下载之类的调用方直接拿去当 <see cref="Formatter"/>。</summary>
    public static string FormatBytes(long bytes) =>
        bytes >= 1024 * 1024
            ? $"{bytes / 1024.0 / 1024.0:0.0} MB"
            : $"{Math.Max(bytes, 0) / 1024.0:0.0} KB";
}
