using Avalonia;
using Avalonia.Controls;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using Dock.Settings;
using FluentAvalonia.UI.Controls;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;

namespace MajdataEdit_NeoX.ViewModels;


public class InspectorTool : Tool
{
    public InspectorTool()
    {
        Id = "Inspector";
        Title = "Inspector";
        CanClose = false;
        CanDrag = true;
    }
}
public class EditorTool : Tool
{
    public EditorTool()
    {
        Id = "Editor";
        Title = "Editor";
        CanClose = false;
        CanDrag = true;
    }
}
public class VisualizerTool : Tool
{
    public VisualizerTool()
    {
        Id = "Visualizer";
        Title = "Visualizer";
        CanClose = false;
        CanDrag = true;
    }
}
public class ChartInfoTool : Tool
{
    public ChartInfoTool()
    {
        Id = "ChartInfo";
        Title = "ChartInfo";
        CanClose = true;
        CanDrag = true;
    }
}
public class SettingsTool : Tool
{
    public SettingsTool()
    {
        Id = "Settings";
        Title = "Settings";
        CanClose = true;
        CanDrag = true;
    }
}


public class MainDockFactory
    (MainViewModel mainVm) : Factory
{
    private IRootDock _rootDock => mainVm.Layout;
    // 默认Dock写在这里
    public override IRootDock CreateLayout()
    {
        ProportionalDock mainLayout;
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        {
            var inspector = new InspectorTool();
            var editor = new EditorTool();
            var visualizer = new VisualizerTool();

            mainLayout = new ProportionalDock
            {
                Id = "MainLayout",
                Orientation = Orientation.Horizontal,
                VisibleDockables = CreateList<IDockable>(
                    new ToolDock
                    {
                        Id = "InspectorDock",
                        Proportion = 0.2,
                        VisibleDockables = CreateList<IDockable>(inspector),
                        ActiveDockable = inspector,
                    },
                    new ProportionalDockSplitter(),
                    new ProportionalDock
                    {
                        Id = "RightLayout",
                        Orientation = Orientation.Vertical,
                        VisibleDockables = CreateList<IDockable>(
                            new ToolDock
                            {
                                Id = "EditorDock",
                                Proportion = 0.75,
                                VisibleDockables = CreateList<IDockable>(editor),
                                ActiveDockable = editor,
                            },
                            new ProportionalDockSplitter(),
                            new ToolDock
                            {
                                Id = "VisualizerDock",
                                Proportion = 0.25,
                                VisibleDockables = CreateList<IDockable>(visualizer),
                                ActiveDockable = visualizer,
                            }
                        )
                    }
                )
            };
        }
        else
        {
            var inspector = new InspectorTool();
            var editor = new EditorTool();
            var visualizer = new VisualizerTool();

            mainLayout = new ProportionalDock
            {
                Id = "MainLayout",
                Orientation = Orientation.Horizontal,
                VisibleDockables = CreateList<IDockable>(
                    new ToolDock
                    {
                        Id = "InspectorDock",
                        Proportion = 0.2,
                        VisibleDockables = CreateList<IDockable>(inspector),
                        ActiveDockable = inspector,
                    },
                    new ProportionalDockSplitter(),
                    new ProportionalDock
                    {
                        Id = "RightLayout",
                        Orientation = Orientation.Vertical,
                        VisibleDockables = CreateList<IDockable>(
                            new ToolDock
                            {
                                Id = "EditorDock",
                                Proportion = 0.75,
                                VisibleDockables = CreateList<IDockable>(editor),
                                ActiveDockable = editor,
                            },
                            new ProportionalDockSplitter(),
                            new ToolDock
                            {
                                Id = "VisualizerDock",
                                Proportion = 0.25,
                                VisibleDockables = CreateList<IDockable>(visualizer),
                                ActiveDockable = visualizer,
                            }
                        )
                    }
                )
            };
        }

        var rootDock = CreateRootDock();
        rootDock.Id = "Root";
        rootDock.VisibleDockables = CreateList<IDockable>(mainLayout);
        rootDock.ActiveDockable = mainLayout;
        rootDock.DefaultDockable = mainLayout;

        return rootDock;
    }


    public override void InitLayout(IDockable layout)
    {
        // Context：全都绑主vm
        DefaultContextLocator = () => mainVm;
        // HostWindow
        DefaultHostWindowLocator = () =>
        {
            var host = new HostWindow
            {
                WindowDecorations = WindowDecorations.BorderOnly,
                DocumentChromeControlsWholeWindow = true
            };

            return host;
        };

        base.InitLayout(layout);
    }


    private bool IsInMainLayout(IDock targetDock)
    {
        for (IDockable? node = targetDock; node is not null; node = node.Owner)
        {
            if (ReferenceEquals(node, mainVm.Layout))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 主窗口 dock 没有 tab,无法在两个面板间切换,因此禁止同一 dock 堆叠两个面板:
    /// 目标 dock 属于主布局且已有可见面板(不含正被移动的面板自身)时拒绝本次停靠。
    /// 浮动窗口的 dock 显示 tab,不受此限制。
    /// </summary>
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
    /// <summary>
    /// 把一个新面板直接浮出(不占据主窗口 dock):先挂到宿主 dock 再立即浮动,
    /// 内部直连基类绕过单占守卫,面板最终只存在于浮动窗口中。
    /// </summary>
    public void AddFloat(IDock host, IDockable dockable)
    {
        base.AddDockable(host, dockable);
        FloatDockable(dockable, new() { OwnerMode = DockWindowOwnerMode.RootWindow });
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


    public void FloatOrActivateTool<T>() where T : Tool, new()
    {
        var existing = FindToolAnywhere<T>();
        if (existing is not null)
        {
            SetActiveDockable(existing);
            return;
        }

        var tool = new T();
        InitDockable(tool, null);
        AddFloat(_rootDock, tool);
    }

    private IDockable? FindToolAnywhere<T>() where T : Tool
    {
        var inMain = FindDockable(mainVm.Layout, d => d is T);
        if (inMain is not null) return inMain;

        foreach (var host in HostWindows)
        {
            var layout = host.Window?.Layout;
            if (layout is null) continue;
            var found = FindDockable(layout, d => d is T);
            if (found is not null) return found;
        }

        return null;
    }
}
