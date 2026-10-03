using System;
using System.Runtime.InteropServices;

namespace MajdataEdit_NeoX.Models.Track;

internal static partial class MiniAudio
{
#if IOS
    private const string LibraryName = "__Internal";
#else
    private const string LibraryName = "miniaudio";
#endif

    // 以下数值取自 miniaudio.h，升级 miniaudio 版本时请对照核实
    public const int MA_SUCCESS = 0;
    public const int MA_AT_END = -17;
    public const int MA_NO_DATA_AVAILABLE = -32;

    // ma_decoder 对 C# 来说是不透明结构体，按足够大的块分配（需 >= sizeof(ma_decoder)）
    private const int DecoderStructSize = 8192;

    public enum MaFormat : uint
    {
        Unknown = 0,
        U8 = 1,
        S16 = 2,
        S24 = 3,
        S32 = 4,
        F32 = 5,
    }

    public sealed class Decoder : IDisposable
    {
        private IntPtr _decoder;
        private bool _disposed;

        public MaFormat Format { get; }
        public uint Channels { get; }
        public uint SampleRate { get; }

        /// <summary>解码器原生输出格式下，每一帧（所有声道）占用的字节数。</summary>
        public int BytesPerFrame { get; }

        private Decoder(IntPtr decoder, MaFormat format, uint channels, uint sampleRate, int bytesPerFrame)
        {
            _decoder = decoder;
            Format = format;
            Channels = channels;
            SampleRate = sampleRate;
            BytesPerFrame = bytesPerFrame;
        }

        public static Decoder FromFile(string filePath)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            var decoder = Marshal.AllocHGlobal(DecoderStructSize);
            unsafe
            {
                new Span<byte>((void*)decoder, DecoderStructSize).Clear();
            }

            // Windows 上 ma_decoder_init_file 走 ANSI 代码页，中日文路径会失败，改用宽字符版本
            var result = OperatingSystem.IsWindows()
                ? Native.ma_decoder_init_file_w(filePath, IntPtr.Zero, decoder)
                : Native.ma_decoder_init_file(filePath, IntPtr.Zero, decoder);

            if (result != MA_SUCCESS)
            {
                Marshal.FreeHGlobal(decoder);
                throw new InvalidOperationException($"ma_decoder_init_file failed: {result} for '{filePath}'");
            }

            // init 成功之后，任何失败都必须先 uninit 再释放内存
            try
            {
                result = Native.ma_decoder_get_data_format(decoder, out var format, out var channels, out var sampleRate, IntPtr.Zero, 0);
                if (result != MA_SUCCESS)
                    throw new InvalidOperationException($"ma_decoder_get_data_format failed: {result}");
                if (channels == 0)
                    throw new InvalidOperationException("Decoder reported 0 channels.");

                var bytesPerFrame = GetBytesPerSample(format) * (int)channels;
                return new Decoder(decoder, format, channels, sampleRate, bytesPerFrame);
            }
            catch
            {
                Native.ma_decoder_uninit(decoder);
                Marshal.FreeHGlobal(decoder);
                throw;
            }
        }

        public (MaFormat Format, uint Channels, uint SampleRate) GetFormat()
        {
            ThrowIfDisposed();
            return (Format, Channels, SampleRate);
        }

        public ulong GetLengthInPcmFrames()
        {
            ThrowIfDisposed();
            var result = Native.ma_decoder_get_length_in_pcm_frames(_decoder, out var length);
            if (result != MA_SUCCESS)
                throw new InvalidOperationException($"ma_decoder_get_length_in_pcm_frames failed: {result}");
            return length;
        }

        public void SeekToPcmFrame(ulong frameIndex)
        {
            ThrowIfDisposed();
            var result = Native.ma_decoder_seek_to_pcm_frame(_decoder, frameIndex);
            if (result != MA_SUCCESS)
                throw new InvalidOperationException($"ma_decoder_seek_to_pcm_frame failed: {result}");
        }

        /// <summary>
        /// 以解码器原生格式（见 <see cref="Format"/>）读取 PCM 帧，交错排列。
        /// buffer 至少需要 frameCount * <see cref="BytesPerFrame"/> 字节。
        /// </summary>
        /// <returns>实际读到的帧数（到达文件末尾时可能小于 frameCount）。</returns>
        public ulong ReadPcmFrames(Span<byte> buffer, ulong frameCount)
        {
            ThrowIfDisposed();
            if (frameCount == 0)
                return 0;
            if ((ulong)buffer.Length < frameCount * (ulong)BytesPerFrame)
                throw new ArgumentException(
                    $"Buffer too small: need {frameCount * (ulong)BytesPerFrame} bytes, got {buffer.Length}.",
                    nameof(buffer));

            unsafe
            {
                fixed (byte* ptr = buffer)
                {
                    var result = Native.ma_decoder_read_pcm_frames(_decoder, ptr, frameCount, out var framesRead);
                    if (result != MA_SUCCESS && result != MA_AT_END && result != MA_NO_DATA_AVAILABLE)
                        throw new InvalidOperationException($"ma_decoder_read_pcm_frames failed: {result}");
                    return framesRead;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_decoder != IntPtr.Zero)
            {
                Native.ma_decoder_uninit(_decoder);
                Marshal.FreeHGlobal(_decoder);
                _decoder = IntPtr.Zero;
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed,this);
        }

        private static int GetBytesPerSample(MaFormat format) => format switch
        {
            MaFormat.U8 => 1,
            MaFormat.S16 => 2,
            MaFormat.S24 => 3,
            MaFormat.S32 => 4,
            MaFormat.F32 => 4,
            _ => throw new NotSupportedException($"Unsupported sample format: {format}"),
        };
    }

    private static partial class Native
    {
        [LibraryImport(LibraryName, EntryPoint = "ma_decoder_init_file", StringMarshalling = StringMarshalling.Utf8)]
        public static partial int ma_decoder_init_file(string pFilePath, IntPtr pConfig, IntPtr pDecoder);

        // wchar_t 在 Windows 上是 UTF-16，所以只在 Windows 调用这个
        [LibraryImport(LibraryName, EntryPoint = "ma_decoder_init_file_w", StringMarshalling = StringMarshalling.Utf16)]
        public static partial int ma_decoder_init_file_w(string pFilePath, IntPtr pConfig, IntPtr pDecoder);

        [LibraryImport(LibraryName, EntryPoint = "ma_decoder_uninit")]
        public static partial int ma_decoder_uninit(IntPtr pDecoder);

        [LibraryImport(LibraryName, EntryPoint = "ma_decoder_read_pcm_frames")]
        public static unsafe partial int ma_decoder_read_pcm_frames(IntPtr pDecoder, byte* pFramesOut, ulong frameCount, out ulong pFramesRead);

        [LibraryImport(LibraryName, EntryPoint = "ma_decoder_seek_to_pcm_frame")]
        public static partial int ma_decoder_seek_to_pcm_frame(IntPtr pDecoder, ulong frameIndex);

        [LibraryImport(LibraryName, EntryPoint = "ma_decoder_get_length_in_pcm_frames")]
        public static partial int ma_decoder_get_length_in_pcm_frames(IntPtr pDecoder, out ulong pLength);

        [LibraryImport(LibraryName, EntryPoint = "ma_decoder_get_data_format")]
        public static partial int ma_decoder_get_data_format(IntPtr pDecoder, out MaFormat pFormat, out uint pChannels, out uint pSampleRate, IntPtr pChannelMap, nuint channelMapCap);
    }
}