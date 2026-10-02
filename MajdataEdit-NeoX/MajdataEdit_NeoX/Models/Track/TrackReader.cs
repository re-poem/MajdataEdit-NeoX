using ManagedBass;
using System;
using System.IO;

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
    public double Length { get; }
    public short[] RawWave { get; }

    private readonly short[][] _waveThumbnails = new short[3][];

    public TrackInfo(string path)
    {
        var stream = Bass.CreateStream(path, 0, 0, BassFlags.Decode);
        if (stream == 0)
            throw new Exception($"Audio decode failed: {Bass.LastError}");

        try
        {
            var bytes = Bass.ChannelGetLength(stream);
            Length = Bass.ChannelBytes2Seconds(stream, bytes);

            RawWave = new short[bytes / sizeof(short)];
            var read = Bass.ChannelGetData(stream, RawWave, (int)bytes);

            if (read < 0)
                throw new Exception($"Audio decode failed: {Bass.LastError}");

            GenerateThumbnail(20, 0);
            GenerateThumbnail(50, 1);
            GenerateThumbnail(100, 2);
        }
        finally
        {
            Bass.StreamFree(stream);
        }
    }

    private void GenerateThumbnail(int blockSize, int level)
    {
        var count = (RawWave.Length + blockSize - 1) / blockSize;
        var thumbnail = new short[count];

        for (var i = 0; i < count; i++)
        {
            var max = short.MinValue;
            var start = i * blockSize;
            var end = Math.Min(start + blockSize, RawWave.Length);

            for (var j = start; j < end; j++)
            {
                if (RawWave[j] > max)
                    max = RawWave[j];
            }

            thumbnail[i] = max;
        }

        _waveThumbnails[level] = thumbnail;
    }

    public ReadOnlySpan<short> GetWaveThumbnail(int level = 0)
    {
        level = Math.Clamp(level, 0, _waveThumbnails.Length - 1);
        return _waveThumbnails[level];
    }
}