using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Cimai;
using CommunityToolkit.Mvvm.ComponentModel;
using MajdataEdit_NeoX.Types;

namespace MajdataEdit_NeoX.Models.Fumen;

public sealed partial class MaidataFile : ObservableObject, IDisposable
{
    [ObservableProperty]
    public partial bool IsDirty { get; set; }



    private MaidataFile()
    {
        Levels = new(7, _ => { OnPropertyChanged(nameof(Levels)); MarkDirty(); });
        Designers = new(7, _ => { OnPropertyChanged(nameof(Designers)); MarkDirty(); });
        Fumens = new(7, _ => { OnPropertyChanged(nameof(Fumens)); MarkDirty(); });

        Commands.CollectionChanged += OnCommandsChanged;
    }
    private void OnCommandsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (MutSimaiCommand c in e.OldItems) c.PropertyChanged -= OnCommandItemChanged;
        if (e.NewItems != null)
            foreach (MutSimaiCommand c in e.NewItems) c.PropertyChanged += OnCommandItemChanged;

        MarkDirty();
    }
    private void OnCommandItemChanged(object? sender, PropertyChangedEventArgs e) => MarkDirty();




    public void MarkAsSaved()
    {
        if (!IsDirty) return;
        IsDirty = false;
    }

    /// <summary>新建谱面等"还没存过盘"的场景用：显式翻成脏。</summary>
    public void MarkAsDirty() => MarkDirty();

    private void MarkDirty()
    {
        if (IsDirty) return;
        IsDirty = true;
    }


    // ======数据======

    private string _title = string.Empty;
    public string Title { get => _title; set => Set(ref _title, value); }

    private string _artist = string.Empty;
    public string Artist { get => _artist; set => Set(ref _artist, value); }

    private string _designer = string.Empty;
    public string Designer { get => _designer; set => Set(ref _designer, value); }

    private float _offset;
    public float Offset { get => _offset; set => Set(ref _offset, value); }
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);
        MarkDirty();
    }


    // 持有timings数据
    private SimaiChart?[] _charts = new SimaiChart?[7];

    public ObservableArray<string?> Levels { get; }
    public ObservableArray<string?> Designers { get; }
    public ObservableArray<string?> Fumens { get; }


    public ObservableCollection<MutSimaiCommand> Commands { get; } = new();






    /// <summary>只用来保持引用传递的 timings 数据访问</summary>
    public SimaiChart GetChart(int difficulty)
        => _charts[difficulty] is { IsDisposed: false } c ? c : SimaiChart.Empty;



    /// <summary>Fumen 改动后调用：替换指定难度的 Chart 并释放旧实例。</summary>
    internal void ReplaceChart(int difficulty, SimaiChart chart)
    {
        var old = _charts[difficulty];
        _charts[difficulty] = chart;
        old?.Dispose();
    }



    public void Dispose()
    {
        if (_charts is null) return;
        foreach (var chart in _charts)
            chart?.Dispose();
        _charts = null!;
        IsDirty = false;
    }




    //=========== 解析 / 序列化 ===========

    public static MaidataFile Parse(string text)
    {
        using var simai = SimaiFile.Parse(text);

        var file = new MaidataFile
        {
            _title = Encoding.UTF8.GetString(simai.Title),
            _artist = Encoding.UTF8.GetString(simai.Artist),
            _designer = Encoding.UTF8.GetString(simai.Des),
            _offset = simai.Offset
        };
        foreach (var cmd in simai.Commands)
            file.Commands.Add(new MutSimaiCommand(Encoding.UTF8.GetString(cmd.Key), Encoding.UTF8.GetString(cmd.Value)));

        for (int i = 0; i < (int)SimaiDifficulty.DIFFICULTY_COUNT; i++)
        {
            var chart = simai.Charts[i];
            if (chart == null) continue;
            file.Levels[i] = Encoding.UTF8.GetString(chart.Level);
            file.Designers[i] = Encoding.UTF8.GetString(chart.Des);
            file.Fumens[i] = Encoding.UTF8.GetString(chart.Fumen);
            // Parse包含timings数据，但File free时会清理掉它们，要给它们独立一下
            simai.DetachChart((SimaiDifficulty)i);
            file.ReplaceChart(i, chart);
        }

        file.MarkAsSaved();
        return file;
    }

    public static string Deparse(MaidataFile file)
    {
        var sb = new StringBuilder()
            .Append("&title=").AppendLine(file.Title)
            .Append("&artist=").AppendLine(file.Artist)
            .Append("&des=").AppendLine(file.Designer)
            .Append("&first=").AppendLine(file.Offset.ToString(CultureInfo.InvariantCulture));

        for (var i = 0; i < 7; i++)
        {
            if (!string.IsNullOrEmpty(file.Designers[i]))
                sb.Append("&des_").Append(i + 1).Append('=').AppendLine(file.Designers[i]);
            if (!string.IsNullOrEmpty(file.Levels[i]))
                sb.Append("&lv_").Append(i + 1).Append('=').AppendLine(file.Levels[i]);
        }

        foreach (var cmd in file.Commands)
            sb.Append('&').Append(cmd.Key).Append('=').AppendLine(cmd.Value);

        for (var i = 0; i < 7; i++)
        {
            var fumen = file.Fumens[i];
            if (!string.IsNullOrEmpty(fumen))
                sb.Append("&inote_").Append(i + 1).Append('=').Append(fumen).AppendLine();
        }

        return sb.ToString();
    }
}

/// <summary>可变命令项 (&amp;key=value)。Deparse 时展开成 maidata 文本。</summary>
public sealed class MutSimaiCommand : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public MutSimaiCommand() { }
    public MutSimaiCommand(string key, string value)
    {
        _key = key; _value = value;
    }

    private string _key = string.Empty;
    public string Key { get => _key; set => Set(ref _key, value); }

    private string _value = string.Empty;
    public string Value { get => _value; set => Set(ref _value, value); }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}