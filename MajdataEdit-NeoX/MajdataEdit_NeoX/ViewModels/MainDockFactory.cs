using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using Dock.Settings;
using FluentAvalonia.UI.Controls;
using MajdataEdit_NeoX.Controls;
using MajdataEdit_NeoX.Views;
using System;
using System.Collections.Generic;
using System.Linq;
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

public class ShortcutTool : Tool
{
    public ShortcutTool()
    {
        Id = "Shortcut";
        Title = "Shortcut";
        CanClose = true;
        CanDrag = true;
    }
}


public class MainDockFactory : Factory
{
    private readonly MainViewModel mainVm;

    public MainDockFactory(MainViewModel mainVm)
    {
        this.mainVm = mainVm;
        RegisterToolDataTemplates();
    }

    private IRootDock RootDock => mainVm.Layout;

    #region 工具视图模板（原 App.axaml 的 DataTemplate DataType="vm:XxxTool"）

    private static bool _toolDataTemplatesRegistered;

    /// <summary>
    /// 以纯代码方式注册各 Tool 的视图模板，取代 App.axaml 里的
    /// <c>&lt;DataTemplate DataType="vm:XxxTool"&gt;</c>。模板里手动把视图的
    /// Avalonia 属性绑到 MainViewModel（即 <see cref="IDockable.Context"/>）上，
    /// 绑定模式沿用各属性自身的 DefaultBindingMode（与 XAML 省略 Mode 时一致）。
    /// </summary>
    private void RegisterToolDataTemplates()
    {
        if (_toolDataTemplatesRegistered) return;
        if (Application.Current is not { } app) return;
        _toolDataTemplatesRegistered = true;
        if (app.DataTemplates.Any(t => t is FuncDataTemplate<InspectorTool>)) return;
        app.DataTemplates.Add(new FuncDataTemplate<InspectorTool>((tool, _) => CreateInspectorView(tool)));
        app.DataTemplates.Add(new FuncDataTemplate<VisualizerTool>((tool, _) => CreateVisualizerView(tool)));
        app.DataTemplates.Add(new FuncDataTemplate<EditorTool>((tool, _) => CreateEditorView(tool)));
        app.DataTemplates.Add(new FuncDataTemplate<ChartInfoTool>((tool, _) => CreateChartInfoView(tool)));
        app.DataTemplates.Add(new FuncDataTemplate<SettingsTool>((tool, _) => CreateSettingsView(tool)));
        app.DataTemplates.Add(new FuncDataTemplate<ShortcutTool>((tool, _) => new ShortcutView()));
    }

    /// <summary>
    /// 原 XAML 的 <c>DataContext="{Binding Context}"</c>：视图的数据上下文是
    /// InitLayout 里 DefaultContextLocator 给出的 MainViewModel。
    /// </summary>
    private static void BindToolContext(Control view, Tool tool)
    {
        view.Bind(StyledElement.DataContextProperty,
            new Binding(nameof(Tool.Context)) { Source = tool });
    }

    private static InspectorView CreateInspectorView(InspectorTool tool)
    {
        var view = new InspectorView();
        BindToolContext(view, tool);

        view.Bind(InspectorView.DisplayTimeProperty, new Binding(nameof(MainViewModel.PlayheadTime)));
        view.Bind(InspectorView.DisplayComboProperty, new Binding(nameof(MainViewModel.CaretCombo)));
        view.Bind(InspectorView.IsConnectedProperty, new Binding(nameof(MainViewModel.IsConnected)));
        view.Bind(InspectorView.ActiveConnectionCountProperty, new Binding(nameof(MainViewModel.ActiveConnectionCount)));
        view.Bind(InspectorView.SelectedDifficultyProperty, new Binding(nameof(MainViewModel.SelectedDifficulty)));
        view.Bind(InspectorView.LevelProperty, new Binding(nameof(MainViewModel.Level)));
        view.Bind(InspectorView.DesignerProperty, new Binding(nameof(MainViewModel.Designer)));
        view.Bind(InspectorView.OffsetProperty, new Binding(nameof(MainViewModel.Offset)));
        view.Bind(InspectorView.PlaybackSpeedProperty, new Binding(nameof(MainViewModel.PlaybackSpeed)));
        view.Bind(InspectorView.IsFollowCursorProperty, new Binding(nameof(MainViewModel.IsFollowCursor)));

        return view;
    }

    private static SimaiVisualizerControl CreateVisualizerView(VisualizerTool tool)
    {
        var view = new SimaiVisualizerControl();
        BindToolContext(view, tool);

        view.Bind(SimaiVisualizerControl.TimeProperty, new Binding(nameof(MainViewModel.PlayheadTime)));
        view.Bind(SimaiVisualizerControl.ZoomLevelProperty, new Binding(nameof(MainViewModel.ZoomLevel)));
        view.Bind(SimaiVisualizerControl.TrackInfoProperty, new Binding(nameof(MainViewModel.TrackInfo)));
        view.Bind(SimaiVisualizerControl.ChartProperty, new Binding(nameof(MainViewModel.Chart)));
        view.Bind(SimaiVisualizerControl.OffsetProperty, new Binding(nameof(MainViewModel.Offset)));
        view.Bind(SimaiVisualizerControl.CaretTimeProperty, new Binding(nameof(MainViewModel.CaretTime)));
        view.Bind(SimaiVisualizerControl.IsAnimatedProperty, new Binding(nameof(MainViewModel.WaveAnimated)));
        view.Bind(SimaiVisualizerControl.SeekProperty, new Binding(nameof(MainViewModel.SeekPlayheadTimeCommand)));

        return view;
    }

    private static FumenEditorView CreateEditorView(EditorTool tool)
    {
        var view = new FumenEditorView();
        BindToolContext(view, tool);

        view.Bind(FumenEditorView.FumenProperty, new Binding(nameof(MainViewModel.Fumen)));
        view.Bind(FumenEditorView.EditorFontSizeProperty, new Binding(nameof(MainViewModel.FontSize)));
        view.Bind(FumenEditorView.WordWrapProperty, new Binding(nameof(MainViewModel.WordWrap)));
        view.Bind(FumenEditorView.CaretOffsetProperty, new Binding(nameof(MainViewModel.CaretOffset)));
        // 依赖注入
        if (tool?.Context is MainViewModel vm)
        {
            view.AttachedToVisualTree += (s, e) => vm.Editor = view;
            view.DetachedFromVisualTree += (s, e) =>
            {
                if (ReferenceEquals(vm.Editor, view)) vm.Editor = null;
            };
        }

        return view;
    }

    private static ChartInfoView CreateChartInfoView(ChartInfoTool tool)
    {
        var view = new ChartInfoView();
        BindToolContext(view, tool);

        view.Bind(ChartInfoView.MaidataProperty, new Binding(nameof(MainViewModel.Maidata)));
        view.Bind(ChartInfoView.CoverProperty, new Binding(nameof(MainViewModel.Cover)));
        view.Bind(ChartInfoView.ImportBgCoverProperty, new Binding(nameof(MainViewModel.ImportBgCoverCommand)));
        view.Bind(ChartInfoView.ImportBgVideoProperty, new Binding(nameof(MainViewModel.ImportBgVideoCommand)));

        return view;
    }

    private static SettingsView CreateSettingsView(SettingsTool tool)
    {
        var view = new SettingsView();
        BindToolContext(view, tool);

        view.Bind(SettingsView.SettingsProperty, new Binding(nameof(MainViewModel.Settings)));
        view.Bind(SettingsView.SettingsChangedProperty, new Binding(nameof(MainViewModel.NotifySettingsChangedCommand)));

        return view;
    }

    #endregion
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
        AddFloat(RootDock, tool);
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
