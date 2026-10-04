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
            WindowDecorations = WindowDecorations.BorderOnly
        };
        host.Resources["DockDocumentControlTabStripVisible"] = true;
        return host;
    }
}
