using Avalonia.Controls.Primitives;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace MajdataEdit_NeoX.Models.Track;

internal static class TrackReader
{
    public static TrackInfo ReadTrackFromDir(string dirPath)
    {
        if (File.Exists(dirPath))
            return ReadTrack(dirPath);
        else if (Directory.Exists(dirPath))
        {
            string[] files;
            files = Directory.GetFiles(dirPath, "*.mp3");
            if (files.Length > 0)
                return ReadTrack(files[0]);
            files = Directory.GetFiles(dirPath, "*.ogg");
            if (files.Length > 0)
                return ReadTrack(files[0]);

            throw new FileNotFoundException("No .mp3 or .ogg files found in the directory.");
        }
        else
        {
            throw new DirectoryNotFoundException($"The specified path '{dirPath}' does not exist.");
        }
    }
    public static TrackInfo ReadTrack(string path) => new(path);
}

public sealed class TrackInfo
{
    // 解码时先按每 10 帧算一次峰值，再合并成 20 / 50 / 100 帧三档。
    // ceil(ceil(n / 10) / k) == ceil(n / (10 * k))，所以长度和逐档单独算完全一致。
    private const int BaseFrames = 10;
    private static readonly int[] ThumbnailFrames = { 20, 50, 100 };

    // 每次从解码器读取的帧数，必须是 BaseFrames 的整数倍
    private const int BlockFrames = 8000;

    public double Length { get; }
    private readonly short[][] _waveThumbnails = new short[3][];

    public TrackInfo(string path)
    {
        using var decoder = MiniAudio.Decoder.FromFile(path);
        var totalFrames = decoder.GetLengthInPcmFrames();

        if (totalFrames == 0 || decoder.SampleRate == 0)
        {
            Length = 0;
            _waveThumbnails[0] = _waveThumbnails[1] = _waveThumbnails[2] = Array.Empty<short>();
            return;
        }

        Length = totalFrames / (double)decoder.SampleRate;

        // 整个文件只解码一遍
        var basePeaks = ReadBasePeaks(decoder, totalFrames);
        for (int i = 0; i < _waveThumbnails.Length; i++)
            _waveThumbnails[i] = Downsample(basePeaks, ThumbnailFrames[i] / BaseFrames);
    }

    /// <summary>每 BaseFrames 帧取一个峰值（所有声道取最大），范围 0..short.MaxValue。</summary>
    private static short[] ReadBasePeaks(MiniAudio.Decoder decoder, ulong totalFrames)
    {
        var format = decoder.Format;
        var bytesPerFrame = decoder.BytesPerFrame;
        var groupCount = checked((int)((totalFrames + BaseFrames - 1) / BaseFrames));

        var peaks = new short[groupCount];
        var buffer = new byte[BlockFrames * bytesPerFrame];

        var group = 0;
        var remaining = totalFrames;
        while (remaining > 0 && group < groupCount)
        {
            var request = (int)Math.Min((ulong)BlockFrames, remaining);
            var read = (int)decoder.ReadPcmFrames(buffer, (ulong)request);
            if (read == 0) break;

            var totalBytesRead = read * bytesPerFrame;
            for (int frameOffset = 0; frameOffset < read && group < groupCount; frameOffset += BaseFrames)
            {
                var framesInGroup = Math.Min(BaseFrames, read - frameOffset);
                var byteStart = frameOffset * bytesPerFrame;
                var byteLength = framesInGroup * bytesPerFrame;

                if (byteStart + byteLength > totalBytesRead)
                    byteLength = totalBytesRead - byteStart;

                var bytes = buffer.AsSpan(byteStart, byteLength);
                peaks[group++] = PeakOf(bytes, format);
            }

            remaining -= (ulong)read;
        }

        return peaks;
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

    /// <summary>计算一段交错 PCM 数据的绝对值峰值，统一归一化到 0..short.MaxValue。</summary>
    private static short PeakOf(ReadOnlySpan<byte> data, MiniAudio.MaFormat format)
    {
        switch (format)
        {
            case MiniAudio.MaFormat.U8:
                {
                    int max = 0;
                    foreach (var b in data)
                    {
                        var v = Math.Abs((int)b - 128);
                        if (v > max) max = v;
                    }
                    return (short)Math.Min(max << 8, short.MaxValue);
                }
            case MiniAudio.MaFormat.S16:
                {
                    int max = 0;
                    foreach (var s in MemoryMarshal.Cast<byte, short>(data))
                    {
                        var v = Math.Abs((int)s); // 先转 int，避免 Math.Abs(short.MinValue) 溢出
                        if (v > max) max = v;
                    }
                    return (short)Math.Min(max, short.MaxValue);
                }
            case MiniAudio.MaFormat.S24:
                {
                    int max = 0;
                    for (int i = 0; i + 2 < data.Length; i += 3)
                    {
                        int v = data[i] | (data[i + 1] << 8) | ((sbyte)data[i + 2] << 16);
                        v = Math.Abs(v);
                        if (v > max) max = v;
                    }
                    return (short)Math.Min(max >> 8, short.MaxValue);
                }
            case MiniAudio.MaFormat.S32:
                {
                    long max = 0;
                    foreach (var s in MemoryMarshal.Cast<byte, int>(data))
                    {
                        var v = Math.Abs((long)s);
                        if (v > max) max = v;
                    }
                    return (short)Math.Min(max >> 16, short.MaxValue);
                }
            case MiniAudio.MaFormat.F32:
                {
                    float max = 0;
                    foreach (var s in MemoryMarshal.Cast<byte, float>(data))
                    {
                        var v = Math.Abs(s);
                        if (v > max) max = v; // NaN 比较为 false，会被自动忽略
                    }
                    return (short)(Math.Min(max, 1f) * short.MaxValue);
                }
            default:
                throw new NotSupportedException($"Unsupported sample format: {format}");
        }
    }

    public ReadOnlySpan<short> GetWaveThumbnail(int level = 0)
    {
        level = Math.Clamp(level, 0, 2);
        return _waveThumbnails[level];
    }
}