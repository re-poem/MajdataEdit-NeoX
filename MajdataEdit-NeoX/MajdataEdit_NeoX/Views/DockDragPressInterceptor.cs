using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Settings;
using MajdataEdit_NeoX.ViewModels;

namespace MajdataEdit_NeoX.Views;

/// <summary>
/// Dock 12 的 DockControl 把 Ctrl+Shift(+任意修饰键) 的按下当作"选中 dockable"
/// 的诊断手势直接返回(PressedHandler 的 Ctrl&&Shift 分支),导致
/// Ctrl+Shift+Alt 组合键拖拽模式下面板无法开始拖动。
/// 此拦截器在隧道阶段抢先标记原始按下为已处理,并向 DockControl 重放一个
/// 剥去 Ctrl/Shift 修饰键的按下事件,使其按正常路径启动拖拽;
/// 拖放动作因 Shift 判定优先仍为 Move。
/// </summary>
public static class DockDragPressInterceptor
{
    public static void Attach(Window window) =>
        window.AddHandler(InputElement.PointerPressedEvent, OnPreviewPress, RoutingStrategies.Tunnel);

    private static void OnPreviewPress(object? sender, PointerPressedEventArgs e)
    {
        if (!DockDragModeState.Instance.IsActive) return;

        var km = e.KeyModifiers;
        if (!km.HasFlag(KeyModifiers.Control) || !km.HasFlag(KeyModifiers.Shift)) return;

        if (e.Source is not Visual visual) return;
        if (!IsOverDragArea(visual)) return;

        if (visual.FindAncestorOfType<DockControl>(true) is not { } dockControl) return;

        e.Handled = true;
        var replay = new PointerPressedEventArgs(
            dockControl, e.Pointer, dockControl,
            e.GetPosition(dockControl), e.Timestamp, e.Properties,
            km & ~(KeyModifiers.Control | KeyModifiers.Shift));
        dockControl.RaiseEvent(replay);
    }

    private static bool IsOverDragArea(Visual visual)
    {
        for (Visual? node = visual; node is not null; node = node.GetVisualParent())
        {
            if (node is Control control && control.GetValue(DockProperties.IsDragAreaProperty))
            {
                return true;
            }
        }

        return false;
    }
}
