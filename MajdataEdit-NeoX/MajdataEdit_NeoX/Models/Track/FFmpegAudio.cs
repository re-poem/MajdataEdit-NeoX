using FFmpeg.AutoGen;
using MajdataEdit_NeoX.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace MajdataEdit_NeoX.Models.Track;

/// <summary>
/// 波形解码：用 <see cref="MajEnv.FFmpegLibraryPath"/> 下随 MajdataViewX 分发的 ffmpeg 库把音频解成
/// 单声道 S16 采样，只保留峰值。
/// </summary>
internal static unsafe partial class FFmpegAudio
{
    /// <summary>每多少个采样帧取一个峰值。TrackInfo 在这一档上再下采样出更粗的两档。</summary>
    public const int BaseFrames = 10;

    private const int IoBufferSize = 64 * 1024;

    static FFmpegAudio()
    {
        // 桌面端库随同目录部署的 MajdataViewX 分发（见 MajEnv.FFmpegLibraryPath），必须在任何 ffmpeg 调用之前指定根路径
        ffmpeg.RootPath = MajEnv.FFmpegLibraryPath;

        // 移动端的库在包体里：Android 的 .so 不带 ABI 版本号，iOS 是静态库链进主程序，
        // 默认那套 libavcodec.so.63 的拼法都命中不了
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
            DynamicallyLoadedBindings.FunctionResolver = new PackagedFunctionResolver();
    }

    /// <summary>移动端：包体内的库没有带版本号的文件名（Android）/ 没有独立映像（iOS）。</summary>
    private sealed partial class PackagedFunctionResolver : FunctionResolverBase
    {
        private const int RtldNow = 2;

        // iOS 静态链接后符号在主可执行文件里，dlopen(null) 拿到的就是主映像
        private static readonly IntPtr MainImage = OperatingSystem.IsIOS() ? dlopen(null, RtldNow) : IntPtr.Zero;

        protected override string GetNativeLibraryName(string libraryName, int version) => $"lib{libraryName}.so";

        protected override IntPtr LoadNativeLibrary(string libraryPath) =>
            OperatingSystem.IsIOS() ? MainImage : dlopen(libraryPath, RtldNow);

        protected override IntPtr FindFunctionPointer(IntPtr nativeLibraryHandle, string functionName) =>
            dlsym(nativeLibraryHandle, functionName);

        [LibraryImport("libdl", StringMarshalling = StringMarshalling.Utf8)]
        private static partial IntPtr dlopen(string? fileName, int flag);

        [LibraryImport("libdl", StringMarshalling = StringMarshalling.Utf8)]
        private static partial IntPtr dlsym(IntPtr nativeLibraryHandle, string symbol);
    }

    /// <param name="Peaks">每 <see cref="BaseFrames"/> 帧一个峰值，范围 0..short.MaxValue。</param>
    /// <param name="SampleRate">音频原始采样率。</param>
    /// <param name="TotalFrames">解码出的总帧数（单声道计）。</param>
    public readonly record struct Waveform(short[] Peaks, uint SampleRate, long TotalFrames);

    /// <summary>从内存里的完整音频文件内容解码波形。</summary>
    public static Waveform ReadWaveform(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0)
            throw new ArgumentException("Empty audio buffer.", nameof(data));

        var input = new MemoryInput(data);
        var inputHandle = GCHandle.Alloc(input);
        var peaks = new List<short>(data.Length / 4096);

        AVFormatContext* format = null;
        AVCodecContext* codec = null;
        AVPacket* packet = null;
        AVFrame* frame = null;
        SwrContext* resampler = null;
        AVIOContext* io = null;
        AVChannelLayout inLayout = default;
        AVChannelLayout outLayout = default;

        // 分组状态：解码是流式的，组边界会跨帧，只能挂在循环外累加
        var groupMax = 0;
        var groupFrames = 0;
        long totalFrames = 0;

        void Collect(short* samples, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var value = Math.Abs((int)samples[i]); // 先转 int，避免 Math.Abs(short.MinValue) 溢出
                if (value > groupMax) groupMax = value;

                if (++groupFrames < BaseFrames) continue;
                peaks.Add((short)Math.Min(groupMax, short.MaxValue));
                groupMax = 0;
                groupFrames = 0;
            }

            totalFrames += count;
        }

        // 局部函数捕获不了指针局部变量（取址会变成闭包字段），所以解码相关的指针都靠参数传
        void Convert(AVFrame* decoded, SwrContext* swr)
        {
            // 重采样到单声道 S16，采样率保持不变，所以输出帧数 == 输入帧数
            var capacity = (int)ffmpeg.av_rescale_rnd(
                ffmpeg.swr_get_delay(swr, decoded->sample_rate) + decoded->nb_samples,
                decoded->sample_rate, decoded->sample_rate, AVRounding.AV_ROUND_UP);
            if (capacity <= 0) return;

            var buffer = (short*)ffmpeg.av_malloc((ulong)capacity * sizeof(short));
            try
            {
                byte* output = (byte*)buffer;
                var produced = ffmpeg.swr_convert(swr, &output, capacity, decoded->extended_data, decoded->nb_samples);
                Check(produced);
                Collect(buffer, produced);
            }
            finally
            {
                ffmpeg.av_free(buffer);
            }
        }

        void Drain(AVCodecContext* decoder, AVFrame* decoded, SwrContext* swr)
        {
            while (true)
            {
                var result = ffmpeg.avcodec_receive_frame(decoder, decoded);
                if (result == ffmpeg.AVERROR(ffmpeg.EAGAIN) || result == ffmpeg.AVERROR_EOF) return;
                Check(result);

                try { Convert(decoded, swr); }
                finally { ffmpeg.av_frame_unref(decoded); }
            }
        }

        try
        {
            // 内存 AVIO：Android/Browser 拿不到本地路径，统一走这条路径，免去落临时文件
            io = ffmpeg.avio_alloc_context(
                (byte*)ffmpeg.av_malloc(IoBufferSize), IoBufferSize, 0,
                (void*)GCHandle.ToIntPtr(inputHandle), ReadCallback, null, null);
            if (io is null) throw new OutOfMemoryException("avio_alloc_context failed.");

            format = ffmpeg.avformat_alloc_context();
            if (format is null) throw new OutOfMemoryException("avformat_alloc_context failed.");
            format->pb = io;
            Check(ffmpeg.avformat_open_input(&format, string.Empty, null, null));
            Check(ffmpeg.avformat_find_stream_info(format, null));

            var streamIndex = ffmpeg.av_find_best_stream(format, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, null, 0);
            if (streamIndex < 0) throw new InvalidOperationException("No audio stream.");
            var stream = format->streams[streamIndex];

            var decoder = ffmpeg.avcodec_find_decoder(stream->codecpar->codec_id);
            if (decoder is null) throw new InvalidOperationException($"No decoder for {stream->codecpar->codec_id}.");

            codec = ffmpeg.avcodec_alloc_context3(decoder);
            if (codec is null) throw new OutOfMemoryException("avcodec_alloc_context3 failed.");
            Check(ffmpeg.avcodec_parameters_to_context(codec, stream->codecpar));
            Check(ffmpeg.avcodec_open2(codec, decoder, null));

            var sampleRate = (uint)codec->sample_rate;
            if (sampleRate == 0) throw new InvalidOperationException("Decoder reported 0 sample rate.");

            packet = ffmpeg.av_packet_alloc();
            frame = ffmpeg.av_frame_alloc();

            ffmpeg.av_channel_layout_default(&outLayout, 1);
            Check(ffmpeg.av_channel_layout_copy(&inLayout, &codec->ch_layout));
            Check(ffmpeg.swr_alloc_set_opts2(
                &resampler, &outLayout, AVSampleFormat.AV_SAMPLE_FMT_S16, codec->sample_rate,
                &inLayout, codec->sample_fmt, codec->sample_rate, 0, null));
            Check(ffmpeg.swr_init(resampler));

            while (ffmpeg.av_read_frame(format, packet) >= 0)
            {
                if (packet->stream_index == streamIndex)
                {
                    Check(ffmpeg.avcodec_send_packet(codec, packet));
                    Drain(codec, frame, resampler);
                }

                ffmpeg.av_packet_unref(packet);
            }

            Check(ffmpeg.avcodec_send_packet(codec, null)); // flush
            Drain(codec, frame, resampler);

            return new Waveform([.. peaks], sampleRate, totalFrames);
        }
        finally
        {
            if (frame is not null) ffmpeg.av_frame_free(&frame);
            if (packet is not null) ffmpeg.av_packet_free(&packet);
            if (resampler is not null) ffmpeg.swr_free(&resampler);
            if (codec is not null) ffmpeg.avcodec_free_context(&codec);
            if (inLayout.order != AVChannelOrder.AV_CHANNEL_ORDER_UNSPEC) ffmpeg.av_channel_layout_uninit(&inLayout);
            if (outLayout.order != AVChannelOrder.AV_CHANNEL_ORDER_UNSPEC) ffmpeg.av_channel_layout_uninit(&outLayout);
            // 自定义 IO 的 AVIOContext 归调用方所有，avformat_close_input 不会释放它
            if (format is not null) ffmpeg.avformat_close_input(&format);
            if (io is not null)
            {
                ffmpeg.av_freep(&io->buffer);
                ffmpeg.avio_context_free(&io);
            }

            inputHandle.Free();
        }
    }

    public static Waveform ReadWaveform(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return ReadWaveform(File.ReadAllBytes(path));
    }

    private static void Check(int error)
    {
        if (error < 0) throw new InvalidOperationException($"ffmpeg: {ErrorText(error)}");
    }

    private static string ErrorText(int error)
    {
        var buffer = stackalloc byte[64];
        ffmpeg.av_strerror(error, buffer, 64);
        return Marshal.PtrToStringUTF8((IntPtr)buffer) ?? error.ToString();
    }

    private sealed class MemoryInput(byte[] data)
    {
        public readonly byte[] Data = data;
        public int Position;
    }

    // avio_alloc_context 长期持有这个委托，必须是一个存活的静态字段
    private static readonly avio_alloc_context_read_packet ReadCallback = ReadMemory;

    private static int ReadMemory(void* opaque, byte* buffer, int bufferSize)
    {
        var input = (MemoryInput)GCHandle.FromIntPtr((IntPtr)opaque).Target!;
        var count = Math.Min(bufferSize, input.Data.Length - input.Position);
        if (count <= 0) return ffmpeg.AVERROR_EOF;

        Marshal.Copy(input.Data, input.Position, (IntPtr)buffer, count);
        input.Position += count;
        return count;
    }
}
