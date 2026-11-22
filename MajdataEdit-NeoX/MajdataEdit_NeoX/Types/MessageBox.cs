using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MajdataEdit_NeoX.Lang;
using MajdataEdit_NeoX.ViewModels;
using MajdataEdit_NeoX.Views;
using MsBox.Avalonia;
using MsBox.Avalonia.Base;
using MsBox.Avalonia.Enums;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MajdataEdit_NeoX.Types;

internal static class IMsBoxExtension
{
    private static Window? Owner => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    extension(IMsBox<ButtonResult> box)
    {
        /// <summary>
        /// Shows the message box as a dialog owned by the main window,
        /// or as a standalone window if no main window is available.
        /// </summary>
        public async Task<ButtonResult> ShowWindowDialogAsync()
        {
            if (Owner is null)
            {
                return await box.ShowWindowAsync();
            }
            else
            {
                return await box.ShowWindowDialogAsync(Owner);
            }
        }
    }
}
internal static class WindowExtension
{
    private static Window? Owner => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    extension(Window window)
    {
        /// <summary>
        /// Shows the window as a dialog owned by the main window,
        /// or as a standalone window if no main window is available.
        /// </summary>
        public void ShowDialog()
        {
            if (Owner is null)
            {
                window.Show();
            }
            else
            {
                window.ShowDialog(Owner);
            }
        }
    }
}

internal static class MessageBox
{
    /// <summary>提示一条必须让用户知道的信息，等其确认后返回。</summary>
    public static async Task ShowAsync(
        string content,
        string title,
        ButtonEnum button = ButtonEnum.Ok,
        Icon icon = Icon.None)
    {
        try
        {
            await MessageBoxManager.GetMessageBoxStandard(title, content, button, icon).ShowWindowDialogAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MessageBox] show: {ex.Message}");
        }
    }


    /// <summary>是 / 否询问。返回用户是否选了"是"；弹窗失败按"否"。</summary>
    public static async Task<bool> ConfirmAsync(string content, string title) =>
        await TryConfirmAsync(content, title) ?? false;

    /// <summary>
    /// 是 / 否询问，但把「弹窗本身没弹出来」和「用户点了否」区分开：
    /// 前者返回 <c>null</c>，调用方可以据此决定不要把「已经问过了」记下来。
    /// </summary>
    public static async Task<bool?> TryConfirmAsync(string content, string title)
    {
        try
        {
            var result = await MessageBoxManager
                .GetMessageBoxStandard(title, content, ButtonEnum.YesNo, Icon.Info)
                .ShowWindowDialogAsync();
            return result == ButtonResult.Yes;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MessageBox] confirm: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 三选一：「保存 / 不保存 / 取消」。按钮沿用宿主的 Yes / No / Cancel 标签。
    /// 弹窗失败按 <see cref="SaveChoice.Cancel"/>——宁可留住现场，也别默默丢掉改动。
    /// </summary>
    public static async Task<SaveChoice> AskSaveAsync(string content, string title)
    {
        try
        {
            var result = await MessageBoxManager
                .GetMessageBoxStandard(title, content, ButtonEnum.YesNoCancel, Icon.Warning)
                .ShowWindowDialogAsync();
            return result switch
            {
                ButtonResult.Yes => SaveChoice.Save,
                ButtonResult.No => SaveChoice.Discard,
                _ => SaveChoice.Cancel,
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MessageBox] ask save: {ex.Message}");
            return SaveChoice.Cancel;
        }
    }

    /// <summary>
    /// 带进度条跑一段活，窗口关掉或点取消即取消 <paramref name="work"/>。
    /// 窗口起不来（无桌面宿主、弹窗失败）就直接跑，不让提示挡业务。
    /// </summary>
    /// <param name="work">要跑的活，收到进度回调（已经做了多少，跟 <paramref name="total"/> 同一单位）与取消令牌。</param>
    /// <param name="total">一共多少；0 表示算不出来，进度条只转圈。</param>
    /// <param name="formatter">把「做了多少 / 一共多少」写成给人看的文本，不给就照原样打数字。</param>
    public static async Task RunWithProgressAsync(
        string title,
        string content,
        Func<Action<long>, CancellationToken, Task> work,
        long total = 0,
        Func<long, string>? formatter = null)
    {
        ProgressDialog? dialog = null;
        ProgressViewModel? progress = null;

        try
        {
            progress = new ProgressViewModel
            {
                Title = title,
                Content = content,
                Total = total,
                Formatter = formatter ?? (static value => value.ToString()),
            };
            dialog = new ProgressDialog { DataContext = progress };

            // ShowDialog 的 Task 要等窗口关掉才完成，这里不能 await——否则活要等关窗才开始跑。
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MessageBox] progress: {ex.Message}");
            dialog?.Close();
            dialog = null;
        }

        // 窗口起没起来都照跑到底；没窗口时给个空进度和不可取消的 token。
        var report = progress is null ? (Action<long>)(_ => { }) : progress.Report;
        var token = progress?.Token ?? CancellationToken.None;

        try
        {
            await work(report, token);
        }
        finally
        {
            dialog?.Close();
        }
    }
}

/// <summary>「有未保存的改动」时用户的选择。</summary>
internal enum SaveChoice
{
    /// <summary>先保存再继续。</summary>
    Save,
    /// <summary>不保存，直接丢弃改动。</summary>
    Discard,
    /// <summary>中止本次操作，留在当前谱面。</summary>
    Cancel,
}
