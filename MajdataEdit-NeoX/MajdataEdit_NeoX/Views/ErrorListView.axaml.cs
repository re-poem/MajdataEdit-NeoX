using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using MajSimai.Diagnostics;
using PropertyGenerator.Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace MajdataEdit_NeoX.Views;

public partial class ErrorListView : UserControl
{
    public ErrorListView()
    {
        InitializeComponent();
    }

    [GeneratedDirectProperty(DefaultBindingMode = BindingMode.OneWay)]
    public partial IReadOnlyList<SimaiDiagnostic>? Diagnostics { get; set; }
    partial void OnDiagnosticsPropertyChanged(IReadOnlyList<SimaiDiagnostic>? newValue) => Rebuild();
    private void OnFilterChanged(object? sender, RoutedEventArgs e) => Rebuild();

    [GeneratedDirectProperty(DefaultBindingMode = BindingMode.TwoWay)]
    public partial SimaiDiagnostic? SelectedDiagnostic { get; set; }

    /// <summary>跳转命令（参数是 <see cref="SimaiDiagnostic"/>），由 MainViewModel 注入。</summary>
    [GeneratedDirectProperty(DefaultBindingMode = BindingMode.OneWay)]
    public partial ICommand? GotoDiagnostic { get; set; }

    private void Rebuild()
    {
        var all = Diagnostics ?? [];

        int errors = 0, warnings = 0, infos = 0;

        foreach (var d in all)
        {
            switch (d.Severity)
            {
                case SimaiDiagnosticSeverity.Error:
                    errors++;
                    break;
                case SimaiDiagnosticSeverity.Warning:
                    warnings++;
                    break;
                default:
                    infos++;
                    break;
            }
        }

        var rows = all.Where(d => d.Severity switch
        {
            SimaiDiagnosticSeverity.Error => ErrorFilter.IsChecked == true,
            SimaiDiagnosticSeverity.Warning => WarningFilter.IsChecked == true,
            //SimaiDiagnosticSeverity.Info => InfoFilter.IsChecked == true,
            _ => true
        }).ToList();

        List.ItemsSource = rows;
        EmptyText.IsVisible = rows.Count == 0;
        ErrorCount.Text = errors.ToString();
        WarningCount.Text = warnings.ToString();
        InfoCount.Text = infos.ToString();
    }

    private void TableView_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (SelectedDiagnostic is not null && GotoDiagnostic?.CanExecute(SelectedDiagnostic) == true)
            GotoDiagnostic.Execute(SelectedDiagnostic);
    }
}
