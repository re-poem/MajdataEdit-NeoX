using System;
using System.Collections.Generic;
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
        var inspector = new Tool
        {
            Id = "Inspector",
            Title = "Inspector",
            CanClose = false
        };

        var editor = new Document
        {
            Id = "Editor",
            Title = "Editor",
            CanClose = false
        };

        var visualizer = new Tool
        {
            Id = "Visualizer",
            Title = "Visualizer",
            CanClose = false
        };

        var leftDock = new ToolDock
        {
            Id = "LeftDock",
            Proportion = 0.19,
            Alignment = Alignment.Left,
            VisibleDockables = CreateList<IDockable>(inspector),
            ActiveDockable = inspector
        };

        var documentDock = new DocumentDock
        {
            Id = "DocumentDock",
            VisibleDockables = CreateList<IDockable>(editor),
            ActiveDockable = editor,
            CanCreateDocument = false
        };

        var bottomDock = new ToolDock
        {
            Id = "BottomDock",
            Proportion = 0.2,
            Alignment = Alignment.Bottom,
            VisibleDockables = CreateList<IDockable>(visualizer),
            ActiveDockable = visualizer
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

        base.InitLayout(layout);
    }
}
