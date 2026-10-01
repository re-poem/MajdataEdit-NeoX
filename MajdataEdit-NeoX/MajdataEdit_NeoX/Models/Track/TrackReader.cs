using Avalonia.Controls;
using MajdataEdit_NeoX.Base;
using ManagedBass;
using System;
using System.IO;

namespace MajdataEdit_NeoX.Models.Track;


internal static class TrackReader
{
    private static int _bgmStream = 0;

    public static TrackInfo ReadTrack(string dirpath)
    {
        var useOgg = File.Exists(dirpath + "/track.ogg");
        var filePath = dirpath + "/track" + (useOgg ? ".ogg" : ".mp3");
        if (_bgmStream is not 0)
            Bass.StreamFree(_bgmStream);
        var bgmDecode = Bass.CreateStream(filePath, 0L, 0L, BassFlags.Decode);
        _bgmStream = Bass.CreateStream(filePath, 0, 0, BassFlags.Prescan);
        var bgmSample = Bass.SampleLoad(filePath, 0, 0, 1, BassFlags.Default);
        try
        {
            var songLength = Bass.ChannelBytes2Seconds(bgmDecode, Bass.ChannelGetLength(bgmDecode));
            var bgmInfo = Bass.SampleGetInfo(bgmSample);
            var freq = bgmInfo.Frequency;
            var sampleCount = (long)(songLength * freq * 2);
            var bgmRAW = new short[sampleCount];
            Bass.SampleGetData(bgmSample, bgmRAW);
            return new TrackInfo(songLength, bgmRAW);

        }
        catch (Exception e)
        {
            throw new Exception(
                "mp3/ogg解码失败。\nMP3/OGG Decode fail.\n" + e.Message + Bass.LastError,
                e);
        }
        finally
        {
            Bass.StreamFree(bgmDecode);
            Bass.SampleFree(bgmSample);
        }
    }
}

public class TrackInfo
{
    public double Length { get; }
    public short[] RawWave { get; } = [];
    private readonly short[][] _waveThumbnails = new short[3][];
    public short[] GetWaveThumbnails(int thumbLevel = 0)
    {
        if (thumbLevel < 0) return _waveThumbnails[0];
        if (thumbLevel > 2) return _waveThumbnails[2];
        return _waveThumbnails[thumbLevel];
    }
    public TrackInfo(double length, short[] rawWave)
    {
        if (length == 0 || rawWave.Length == 0) throw new Exception("Music Wave Load Error");
        Length = length;
        RawWave = rawWave;

        var sampleCount = rawWave.Length;

        _waveThumbnails[0] = new short[sampleCount / 20 + 1];
        for (var i = 0; i < sampleCount; i += 20) _waveThumbnails[0][i / 20] = RawWave[i];
        _waveThumbnails[1] = new short[sampleCount / 50 + 1];
        for (var i = 0; i < sampleCount; i += 50) _waveThumbnails[1][i / 50] = RawWave[i];
        _waveThumbnails[2] = new short[sampleCount / 100 + 1];
        for (var i = 0; i < sampleCount; i += 100) _waveThumbnails[2][i / 100] = RawWave[i];
    }
}
