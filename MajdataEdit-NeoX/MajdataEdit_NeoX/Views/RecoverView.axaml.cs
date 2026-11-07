using Avalonia;
using Avalonia.Controls;
using MajdataEdit_NeoX.ViewModels;
using System.Threading.Tasks;

namespace MajdataEdit_NeoX.Views;

/// <summary>
/// 自动保存恢复面板。视图只做两件事：附加后把 VM 装配起来，
/// 以及把难度选中项换算成预览里的滚动位置。
/// </summary>
public partial class RecoverView : UserControl
{
    private bool _initialized;

    public RecoverView() => InitializeComponent();

    /// <summary>由 <c>MainDockFactory</c> 在视图附加到树之后调用。</summary>
    public async Task InitializeAsync(RecoverViewModel vm)
    {
        if (_initialized) return;
        _initialized = true;

        DataContext = vm;
        await vm.RefreshAsync();
    }

    private async void RefreshButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is RecoverViewModel viewModel)
            await viewModel.RefreshAsync();
    }

    private void DifficultyList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not RecoverViewModel viewModel) return;
        if ((sender as ListBox)?.SelectedItem is not RecoverDifficulty difficulty) return;

        var offset = OffsetOfLine(viewModel.Preview, difficulty.LineNumber);
        PreviewText.SelectionStart = offset;
        PreviewText.SelectionEnd = offset + difficulty.DisplayName.Length;

        // 等宽 13 号字行高约 17px，按行号估个滚动位置就够用。
        PreviewScroll.Offset = new Vector(0, (difficulty.LineNumber - 1) * 17);
    }

    private static int OffsetOfLine(string text, int lineNumber)
    {
        var offset = 0;
        for (var line = 1; line < lineNumber; line++)
        {
            var next = text.IndexOf('\n', offset);
            if (next < 0) return text.Length;
            offset = next + 1;
        }

        return offset;
    }
}
