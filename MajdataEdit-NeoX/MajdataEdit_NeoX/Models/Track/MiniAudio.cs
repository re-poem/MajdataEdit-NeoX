using System;
using System.IO;
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
        // FromMemory 时固定住托管缓冲区：ma_decoder 会长期持有这个指针，不能用 fixed 块。
        private GCHandle _pinnedData;

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

        /// <summary>
        /// 从文件路径解码。实现上先读进内存再走 <see cref="FromMemory"/>，原因有两个：
        ///
        /// 1. Windows 上 ma_decoder_init_file 走 ANSI 代码页，中日文路径会失败。原本改用
        ///    宽字符版本 ma_decoder_init_file_w，但 _w 变体没有 vorbis 的 file-specific
        ///    backend 分支，会退回通用 VFS 回调，使 stb_vorbis 走 push 模式；而
        ///    ma_stbvorbis_get_length_in_pcm_frames 在 push 模式下固定返回 0，
        ///    结果就是 ogg 的时长和波形全空。
        /// 2. 统一成内存路径后，桌面端与 Android/Browser（本来就没有本地路径）行为一致。
        /// </summary>
        public static Decoder FromFile(string filePath)
        {
            ArgumentNullException.ThrowIfNull(filePath);
            return FromMemory(File.ReadAllBytes(filePath));
        }

        /// <summary>
        /// 直接从内存里的完整文件内容解码。用于拿不到本地路径的存储项
        /// （Android 的 <c>content://</c>、Browser 沙箱），免去把音频落到临时文件。
        /// </summary>
        public static Decoder FromMemory(byte[] data)
        {
            ArgumentNullException.ThrowIfNull(data);
            if (data.Length == 0)
                throw new ArgumentException("Empty audio buffer.", nameof(data));

            var decoder = AllocateDecoder();
            // 先 pin 住：init 之后 ma_decoder 会一直用这个地址读
            var pinned = GCHandle.Alloc(data, GCHandleType.Pinned);

            int result;
            unsafe
            {
                result = Native.ma_decoder_init_memory(
                    (byte*)pinned.AddrOfPinnedObject(), (nuint)data.Length, IntPtr.Zero, decoder);
            }

            if (result != MA_SUCCESS)
            {
                pinned.Free();
                Marshal.FreeHGlobal(decoder);
                throw new InvalidOperationException($"ma_decoder_init_memory failed: {result}");
            }

            return CompleteInit(decoder, pinned);
        }

        private static IntPtr AllocateDecoder()
        {
            var decoder = Marshal.AllocHGlobal(DecoderStructSize);
            unsafe
            {
                new Span<byte>((void*)decoder, DecoderStructSize).Clear();
            }
            return decoder;
        }

        /// <summary>init 成功之后，任何失败都必须先 uninit 再释放内存。</summary>
        private static Decoder CompleteInit(IntPtr decoder, GCHandle pinnedData)
        {
            try
            {
                var result = Native.ma_decoder_get_data_format(decoder, out var format, out var channels, out var sampleRate, IntPtr.Zero, 0);
                if (result != MA_SUCCESS)
                    throw new InvalidOperationException($"ma_decoder_get_data_format failed: {result}");
                if (channels == 0)
                    throw new InvalidOperationException("Decoder reported 0 channels.");

                var bytesPerFrame = GetBytesPerSample(format) * (int)channels;
                return new Decoder(decoder, format, channels, sampleRate, bytesPerFrame)
                {
                    _pinnedData = pinnedData,
                };
            }
            catch
            {
                Native.ma_decoder_uninit(decoder);
                Marshal.FreeHGlobal(decoder);
                if (pinnedData.IsAllocated) pinnedData.Free();
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

            if (_pinnedData.IsAllocated) _pinnedData.Free();
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
        [LibraryImport(LibraryName, EntryPoint = "ma_decoder_init_memory")]
        public static unsafe partial int ma_decoder_init_memory(byte* pData, nuint dataSize, IntPtr pConfig, IntPtr pDecoder);

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