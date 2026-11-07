using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MajdataEdit_NeoX.Types;

/// <summary>
/// <c>MessageBox.Avalonia</c> 的薄包装。它自己就是跨平台的（每个平台各自实现宿主），
/// 所以项目里不再需要一层 <c>IMessageService</c> 抽象，调用方直接来这里。
///
/// 统一兜底：弹窗本身失败（没有可用宿主等）只记日志，不把异常甩进业务流程。
/// </summary>
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
            await MessageBoxManager.GetMessageBoxStandard(title, content, button, icon).ShowAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MessageBox] show: {ex.Message}");
        }
    }

    /// <summary>是 / 否询问。返回用户是否选了"是"；弹窗失败按"否"。</summary>
    public static async Task<bool> ConfirmAsync(string content, string title)
    {
        try
        {
            var result = await MessageBoxManager
                .GetMessageBoxStandard(title, content, ButtonEnum.YesNo, Icon.Info)
                .ShowAsync();
            return result == ButtonResult.Yes;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MessageBox] confirm: {ex.Message}");
            return false;
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
                .ShowAsync();
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
