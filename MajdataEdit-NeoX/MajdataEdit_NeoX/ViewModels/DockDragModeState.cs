using System.ComponentModel;

namespace MajdataEdit_NeoX.ViewModels;

/// <summary>
/// 全局 dock 拖拽模式开关。
/// 面板遮罩层绑定此静态状态,使主窗口与浮动窗口中的所有 DockControl 保持同步,
/// 避免浮动窗口中取不到 MainViewModel 时遮罩状态失控。
/// </summary>
public sealed class DockDragModeState : INotifyPropertyChanged
{
    public static DockDragModeState Instance { get; } = new();

    private bool _isActive;

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value) return;
            _isActive = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
