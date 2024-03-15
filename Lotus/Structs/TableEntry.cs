// SPDX-License-Identifier: MPL-2.0

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DragonLib.Compression;
using Lotus.Compression;
using Lotus.IO;

namespace Lotus.Structs;

[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 0x60)]
public record struct TableEntry {
    public long Offset;
    public long Time;
    public int CompressedSize;
    public int Size;
    public int Flags;
    public int Parent;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x40)]
    public string Name;

    public bool IsDirectory => Offset == -1 || Size <= 0;
    public bool IsCompressed => CompressedSize != Size;

    public IMemoryOwner<byte>? Read(Stream cache) {
        if (!cache.CanSeek || !cache.CanRead || IsDirectory) {
            return null;
        }

        using var reader = new PooledMemoryMarshal(CompressedSize, IsCompressed);
        cache.Seek(Offset, SeekOrigin.Begin);
        cache.ReadExactly(reader.Buffer.Span);
        if (!IsCompressed) {
            return reader.Owner;
        }

        using var file = new PooledMemoryMarshal(Size, false);
        while (reader.Cursor < CompressedSize) {
            if (reader.Peek<byte>() >> 7 == 1) { // IsNewFormat bit.
                var frameHeaderRaw = BinaryPrimitives.ReverseEndianness(reader.Read<ulong>());
                var frameHeader = Unsafe.As<ulong, BlockFrameHeader>(ref frameHeaderRaw);
                Debug.Assert(frameHeader.UseNewFormat, "frameHeader.UseNewFormat");
                Debug.Assert(frameHeader.CompressionType == BlockCompressionType.Oodle, "frameHeader.CompressionType == BlockCompressionType.Oodle");
                Debug.Assert(frameHeader.BlockSize <= 0x40000, "frameHeader.BlockSize <= 0x40000");
                var buffer = reader.Slice(frameHeader.CompressedSize);
                if (!frameHeader.IsCompressed) {
                    file.Paste(buffer);
                    continue;
                }

                using var decompressed = MemoryPool<byte>.Shared.Rent(frameHeader.BlockSize);
                if (decompressed.Memory.Length < frameHeader.BlockSize) {
                    throw new OutOfMemoryException();
                }

                var bytes = Oodle.Decompress(buffer, decompressed.Memory[..frameHeader.BlockSize]);
                if (bytes != frameHeader.BlockSize) {
                    throw new InvalidOperationException("Failed to decompress block");
                }

                file.Paste(decompressed.Memory[..bytes]);
            } else {
                var compressedSize = BinaryPrimitives.ReverseEndianness(reader.Read<short>());
                var size = BinaryPrimitives.ReverseEndianness(reader.Read<short>());
                var buffer = reader.Slice(compressedSize);
                if (size == compressedSize) {
                    file.Paste(buffer);
                    continue;
                }

                using var decompressed = MemoryPool<byte>.Shared.Rent(size);
                if (decompressed.Memory.Length < size) {
                    throw new OutOfMemoryException();
                }

                var (_, bytes) = LempelZiv.DecompressLZF(buffer.Span, decompressed.Memory[..size].Span);
                if (bytes != size) {
                    throw new InvalidOperationException("Failed to decompress block");
                }

                file.Paste(decompressed.Memory[..bytes]);
            }
        }

        Debug.Assert(file.Cursor == Size, "file.Buffer.Length == Size");

        return file.Owner;
    }
}
