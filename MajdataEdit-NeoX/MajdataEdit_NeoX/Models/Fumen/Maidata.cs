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
    public partial bool IsDirty { get; private set; }



    private MaidataFile()
    {
        Charts = new(7, _ => OnPropertyChanged(nameof(Charts)));
        Levels = new(7, _ => { OnPropertyChanged(nameof(Levels)); MarkDirty(); });
        Designers = new(7, _ => { OnPropertyChanged(nameof(Designers)); MarkDirty(); });
        Fumens = new(7, OnFumenChanged);

        Commands.CollectionChanged += OnCommandsChanged;
    }
    void OnFumenChanged(int i)
    {
        OnPropertyChanged(nameof(Fumens));
        MarkDirty();
        try
        {
            // 运行时编辑 fumen → 自动重建对应难度的 chart。
            // 维持不变量：_charts[i] 永远是 Fumens[i] 的最新 parse 结果。
            // 解析失败时保留旧 chart（silent），不让脏数据毁掉一个仍能用的 chart。
            // _charts 写入触发 onChanged → OnPropertyChanged(nameof(Charts))，无需手动 fire。
            var old = Charts[i];
            Charts[i] = SimaiChart.Parse(Fumens[i] ?? string.Empty);
            old?.Dispose();
        }
        catch
        {
        }
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




    public ObservableArray<string?> Levels { get; }
    public ObservableArray<string?> Designers { get; }
    public ObservableArray<string?> Fumens { get; }
    /// <summary>仅供 timings 数据访问</summary>
    public ObservableArray<SimaiChart?> Charts = null!;


    public ObservableCollection<MutSimaiCommand> Commands { get; } = new();








    public void Dispose()
    {
        if (Charts is null) return;
        for (var i = 0; i < Charts.Length; i++)
            Charts[i]?.Dispose();
        Charts = null!;
        IsDirty = false;
    }




    //=========== 解析 / 序列化 ===========

    public static MaidataFile New()
    {
        var file = new MaidataFile
        {
            _title = string.Empty,
            _artist = string.Empty,
            _designer = string.Empty,
            _offset = 0
        };
        file.MarkDirty();
        return file;
    }

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
            // File Parse包含timings数据，但是为了代码统一，这里通过OnFumenChanged来写入chart数据
            file.Fumens[i] = Encoding.UTF8.GetString(chart.Fumen);
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