using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;

namespace MajdataEdit_NeoX.ViewModels;

public class MainDockFactory : Factory
{
    private readonly MainViewModel _context;

    public MainDockFactory(MainViewModel context)
    {
        _context = context;
    }

    public override IRootDock CreateLayout()
    {
        // 全部面板统一为 Document:tab 常驻且可拖拽停靠;组合键遮罩作为
        // "按住面板任意位置拖动"的补充方式(CanDrag 保持 true 供两者共用)
        var inspector = new Document
        {
            Id = "Inspector",
            Title = "Inspector",
            CanClose = false,
            CanDrag = true
        };

        var editor = new Document
        {
            Id = "Editor",
            Title = "Editor",
            CanClose = false,
            CanDrag = true
        };

        var visualizer = new Document
        {
            Id = "Visualizer",
            Title = "Visualizer",
            CanClose = false,
            CanDrag = true
        };

        var leftDock = new DocumentDock
        {
            Id = "LeftDock",
            Proportion = 0.19,
            VisibleDockables = CreateList<IDockable>(inspector),
            ActiveDockable = inspector,
            CanCreateDocument = false
        };

        var documentDock = new DocumentDock
        {
            Id = "DocumentDock",
            VisibleDockables = CreateList<IDockable>(editor),
            ActiveDockable = editor,
            CanCreateDocument = false
        };

        var bottomDock = new DocumentDock
        {
            Id = "BottomDock",
            Proportion = 0.2,
            VisibleDockables = CreateList<IDockable>(visualizer),
            ActiveDockable = visualizer,
            CanCreateDocument = false
        };

        var mainLayout = new ProportionalDock
        {
            Id = "MainLayout",
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(
                leftDock,
                new ProportionalDock
                {
                    Id = "RightLayout",
                    Orientation = Orientation.Vertical,
                    VisibleDockables = CreateList<IDockable>(
                        documentDock,
                        bottomDock
                    )
                }
            )
        };

        var rootDock = CreateRootDock();
        rootDock.Id = "Root";
        rootDock.Title = "Root";
        rootDock.VisibleDockables = CreateList<IDockable>(mainLayout);
        rootDock.ActiveDockable = mainLayout;
        rootDock.DefaultDockable = mainLayout;

        return rootDock;
    }

    public override void InitLayout(IDockable layout)
    {
        ContextLocator = new Dictionary<string, Func<object?>>
        {
            ["Inspector"] = () => _context,
            ["Editor"] = () => _context,
            ["Visualizer"] = () => _context,
            ["Settings"] = () => _context,
            ["ChartInfo"] = () => _context
        };

        // 浮动窗口宿主:没有 locator 时拖出的面板会进入无宿主的浮动布局,直接"消失"。
        // ShowActivated=false 避免浮动窗口抢焦点(否则主窗口 Deactivated 会打断组合键拖拽);
        // WindowDecorations.None = 无边框;浮动窗口的 Resources 把 tab 条覆盖回可见,
        // 用户通过拖动 tab 把面板停靠回主窗口。
        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = CreateFloatHost
        };
        DefaultHostWindowLocator = CreateFloatHost;

        base.InitLayout(layout);
    }

    private static HostWindow CreateFloatHost()
    {
        var host = new HostWindow
        {
            ShowActivated = false,
            WindowDecorations = WindowDecorations.BorderOnly,
            // chrome 占满窗口:tab 条贴顶,配合 DocumentTabStrip.EnableWindowDrag
            // 实现拖 tab 条移动窗口位置
            DocumentChromeControlsWholeWindow = true
        };
        host.Resources["DockDocumentControlTabStripVisible"] = true;
        return host;
    }

    /// <summary>
    /// 主窗口 dock 没有 tab,无法在两个面板间切换,因此禁止同一 dock 堆叠两个面板:
    /// 目标 dock 属于主布局且已有可见面板(不含正被移动的面板自身)时拒绝本次停靠。
    /// 浮动窗口的 dock 显示 tab,不受此限制。
    /// </summary>
    private bool IsInMainLayout(IDock targetDock)
    {
        for (IDockable? node = targetDock; node is not null; node = node.Owner)
        {
            if (ReferenceEquals(node, _context.Layout))
            {
                return true;
            }
        }

        return false;
    }

    private bool WouldStackInMainLayout(IDock targetDock, IDockable? incoming)
    {
        if (!IsInMainLayout(targetDock)) return false;

        var visible = targetDock.VisibleDockables;
        if (visible is null) return false;

        var others = 0;
        foreach (var d in visible)
        {
            if (!ReferenceEquals(d, incoming)) others++;
        }

        return others > 0;
    }

    public override void AddDockable(IDock dock, IDockable dockable)
    {
        if (WouldStackInMainLayout(dock, dockable)) return;
        base.AddDockable(dock, dockable);
    }

    public override void InsertDockable(IDock dock, IDockable dockable, int index)
    {
        if (WouldStackInMainLayout(dock, dockable)) return;
        base.InsertDockable(dock, dockable, index);
    }

    public override void MoveDockable(IDock sourceDock, IDock targetDock, IDockable sourceDockable, IDockable? targetDockable)
    {
        if (WouldStackInMainLayout(targetDock, sourceDockable)) return;
        base.MoveDockable(sourceDock, targetDock, sourceDockable, targetDockable);
    }

    public override void MoveDockable(IDock dock, IDockable sourceDockable, IDockable targetDockable)
    {
        if (WouldStackInMainLayout(dock, sourceDockable)) return;
        base.MoveDockable(dock, sourceDockable, targetDockable);
    }

    /// <summary>
    /// 主布局的 dock 即使清空也不折叠:空 dock 仍是拖放目标,
    /// 面板全部浮出后才能把浮动窗口的 tab 拖回主窗口重新停靠。
    /// </summary>
    public override void RemoveDockable(IDockable dockable, bool collapse)
    {
        if (collapse && dockable.Owner is IDock ownerDock && IsInMainLayout(ownerDock))
        {
            collapse = false;
        }

        base.RemoveDockable(dockable, collapse);
    }

    /// <summary>
    /// 把一个新面板直接浮出(不占据主窗口 dock):先挂到宿主 dock 再立即浮动,
    /// 内部直连基类绕过单占守卫,面板最终只存在于浮动窗口中。
    /// </summary>
    public void FloatDetached(IDock host, IDockable dockable)
    {
        base.AddDockable(host, dockable);
        FloatDockable(dockable);
    }
}
