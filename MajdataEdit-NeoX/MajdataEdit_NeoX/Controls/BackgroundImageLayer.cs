using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PropertyGenerator.Avalonia;

namespace MajdataEdit_NeoX.Controls;


/// <summary>
/// 背景图片层:图片 + 虚化 + 偏移。
/// 纯代码控件,不需要 axaml / ControlTheme。放在 Grid 最底层,DockControl 等内容叠在它上面。
/// 本控件自身不接收鼠标事件(IsHitTestVisible = false)。
/// </summary>
public partial class BackgroundImageLayer : Panel
{
    // ───────────────────────── 属性 ─────────────────────────

    [GeneratedStyledProperty]
    public partial string? ImagePath { get; set; }

    [GeneratedStyledProperty(DefaultValue = 1.0)]
    public partial double ImageOpacity { get; set; }

    [GeneratedStyledProperty]
    public partial double BlurRadius { get; set; }

    [GeneratedStyledProperty(DefaultValue = Stretch.UniformToFill)]
    public partial Stretch Stretch { get; set; }

    [GeneratedStyledProperty(DefaultValue = 1920)]
    public partial int DecodeWidth { get; set; }

    [GeneratedStyledProperty]
    public partial double OffsetX { get; set; }

    [GeneratedStyledProperty]
    public partial double OffsetY { get; set; }

    /// <summary>图片加载失败时触发(路径不存在、格式不支持等),此时图片会被清空。</summary>
    public event EventHandler<Exception>? ImageLoadFailed;

    // ───────────────────────── 内部元素 ─────────────────────────

    private readonly Image _image = new();
    private readonly ScaleTransform _overscan = new(1, 1); // 虚化时轻微放大,盖住边缘变暗
    private readonly TranslateTransform _offset = new();
    private int _loadVersion;                               // 防止快速切换图片时旧结果覆盖新结果

    public BackgroundImageLayer()
    {
        ClipToBounds = true;
        IsHitTestVisible = false;

        _image.RenderTransform = new TransformGroup
        {
            Children = { _overscan, _offset }
        };
        Children.Add(_image);

        // 属性的默认值不会触发 change hooks,这里手动应用一遍
        UpdateStretch();
        UpdateImageOpacity();
        UpdateBlur();
        UpdateOffset();
        LoadImageAsync(ImagePath, DecodeWidth);
    }

    // ───────────────────────── 属性变化 ─────────────────────────

    partial void OnPropertyChangedOverride(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == ImagePathProperty || change.Property == DecodeWidthProperty)
            LoadImageAsync(ImagePath, DecodeWidth);
        else if (change.Property == ImageOpacityProperty)
            UpdateImageOpacity();
        else if (change.Property == BlurRadiusProperty)
            UpdateBlur();
        else if (change.Property == StretchProperty)
        {
            UpdateStretch();
            UpdateOverscan();
        }
        else if (change.Property == OffsetXProperty || change.Property == OffsetYProperty)
            UpdateOffset();
        else if (change.Property == BoundsProperty)
            UpdateOverscan();
    }

    private void UpdateStretch() => _image.Stretch = Stretch;

    private void UpdateOffset()
    {
        _offset.X = OffsetX;
        _offset.Y = OffsetY;
    }

    private void UpdateImageOpacity() => _image.Opacity = Math.Clamp(ImageOpacity, 0, 1);

    private void UpdateBlur()
    {
        var radius = Math.Max(0, BlurRadius);
        // 每次新建 BlurEffect,保证一定会触发重绘
        _image.Effect = radius > 0 ? new BlurEffect { Radius = radius } : null;
        UpdateOverscan();
    }

    /// <summary>
    /// 虚化会把图片边缘与透明区域混合,四周出现暗边。
    /// 做法:图片按虚化半径略微放大,多出的部分被 ClipToBounds 裁掉。
    /// 只在"铺满"类拉伸方式下启用(Uniform 之类本来就有留白,放大没意义)。
    /// </summary>
    private void UpdateOverscan()
    {
        var radius = Math.Max(0, BlurRadius);
        var minSide = Math.Min(Bounds.Width, Bounds.Height);
        var fills = Stretch is Stretch.UniformToFill or Stretch.Fill;

        var scale = 1.0;
        if (radius > 0 && fills && minSide > 0)
            scale = 1 + 2 * (radius * 3) / minSide; // 每边多出约 3 倍半径

        _overscan.ScaleX = scale;
        _overscan.ScaleY = scale;
    }

    // ───────────────────────── 图片加载 ─────────────────────────

    private async void LoadImageAsync(string? path, int decodeWidth)
    {
        var version = ++_loadVersion;

        if (string.IsNullOrWhiteSpace(path))
        {
            SetBitmap(null);
            return;
        }

        try
        {
            // 解码放到后台线程,大图不卡 UI(Bitmap 可以在非 UI 线程创建)
            var bitmap = await Task.Run(() => LoadBitmap(path, decodeWidth));

            if (version != _loadVersion) // 期间又换了图,丢弃这次结果
            {
                bitmap.Dispose();
                return;
            }

            SetBitmap(bitmap);
        }
        catch (Exception ex)
        {
            if (version != _loadVersion) return;
            SetBitmap(null);
            ImageLoadFailed?.Invoke(this, ex);
        }
    }

    private static Bitmap LoadBitmap(string path, int decodeWidth)
    {
        using Stream stream =
            Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme == "avares"
                ? AssetLoader.Open(uri)
                : File.OpenRead(path);

        return decodeWidth > 0
            ? Bitmap.DecodeToWidth(stream, decodeWidth, BitmapInterpolationMode.HighQuality)
            : new Bitmap(stream);
    }

    private void SetBitmap(Bitmap? bitmap)
    {
        var old = _image.Source as Bitmap;
        _image.Source = bitmap;
        old?.Dispose(); // 先换上新图再释放旧图
    }
}