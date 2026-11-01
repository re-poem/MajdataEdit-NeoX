using System;
using System.IO;

namespace MajdataEdit_NeoX.Models.Track;

internal static class TrackReader
{
    public static TrackInfo ReadTrack(string path) => new(path);

    /// <summary>从内存中的完整音频文件内容读取（拿不到本地路径的存储项）。</summary>
    public static TrackInfo ReadTrack(byte[] data) => new(data);
}

public sealed class TrackInfo
{
    // 解码时先按每 BaseFrames 帧算一次峰值，再合并成 20 / 50 / 100 帧三档。
    // ceil(ceil(n / 10) / k) == ceil(n / (10 * k))，所以长度和逐档单独算完全一致。
    private static readonly int[] ThumbnailFrames = [20, 50, 100];

    public double Length { get; }
    private readonly short[][] _waveThumbnails = new short[3][];

    public TrackInfo(string path)
        : this(FFmpegAudio.ReadWaveform(path))
    {
    }

    /// <summary>从内存中的完整音频文件内容构建（拿不到本地路径的存储项）。</summary>
    public TrackInfo(byte[] data)
        : this(FFmpegAudio.ReadWaveform(data))
    {
    }

    private TrackInfo(FFmpegAudio.Waveform waveform)
    {
        if (waveform.TotalFrames == 0 || waveform.SampleRate == 0)
        {
            Length = 0;
            _waveThumbnails[0] = _waveThumbnails[1] = _waveThumbnails[2] = [];
            return;
        }

        Length = waveform.TotalFrames / (double)waveform.SampleRate;

        for (int i = 0; i < _waveThumbnails.Length; i++)
            _waveThumbnails[i] = Downsample(waveform.Peaks, ThumbnailFrames[i] / FFmpegAudio.BaseFrames);
    }

    private static short[] Downsample(short[] source, int factor)
    {
        var count = (source.Length + factor - 1) / factor;
        var result = new short[count];

        for (int i = 0; i < count; i++)
        {
            var start = i * factor;
            var end = Math.Min(start + factor, source.Length);

            short max = 0;
            for (int j = start; j < end; j++)
                if (source[j] > max) max = source[j];
            result[i] = max;
        }

        return result;
    }

    public ReadOnlySpan<short> GetWaveThumbnail(int level = 0)
    {
        level = Math.Clamp(level, 0, 2);
        return _waveThumbnails[level];
    }
}
