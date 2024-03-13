// SPDX-License-Identifier: MPL-2.0

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using DragonLib.IO;
using Lotus.ContentCache.Types;

namespace Lotus.ContentCache;

public static class TableEntryExtensions {
    internal static Memory<byte> Read(this TableEntry entry, Stream cache) {
        if (!cache.CanSeek || !cache.CanRead) {
            return Memory<byte>.Empty;
        }

        if (entry.IsDirectory) {
            return Memory<byte>.Empty;
        }

        var readBuffer = new CursoredMemoryMarshal(new byte[entry.CompressedSize]);
        cache.Seek(entry.Offset, SeekOrigin.Begin);
        cache.ReadExactly(readBuffer.Buffer.Span);
        if (!entry.IsCompressed) {
            return readBuffer.Buffer;
        }

        var file = new CursoredMemoryMarshal(new byte[entry.Size]);
        while (readBuffer.Cursor < entry.CompressedSize) {
            if (readBuffer.Peek<byte>() >> 7 == 1) {
                var frameHeader = BitPacked.Unpack<BlockFrameHeader>(BinaryPrimitives.ReverseEndianness(readBuffer.Read<ulong>()));
                Debug.Assert(frameHeader.UseNewFormat, "frameHeader.UseNewFormat");
                Debug.Assert(frameHeader.Unknown1 == 1, "frameHeader.Unknown1 == 1");
                var buffer = readBuffer.Slice(frameHeader.CompressedSize);
                if (!frameHeader.IsCompressed) {
                    file.Paste(buffer);
                    continue;
                }

                var decompressed = new byte[frameHeader.BlockSize].AsMemory();
                Oodle.Decompress(buffer, decompressed);
                Debug.Assert(decompressed.Length == frameHeader.BlockSize, "decompressed.Length == frameHeader.BlockSize");
                file.Paste(decompressed);
            } else {
                var compressedSize = BinaryPrimitives.ReverseEndianness(readBuffer.Read<short>());
                var size = BinaryPrimitives.ReverseEndianness(readBuffer.Read<short>());
                var buffer = readBuffer.Slice(compressedSize);
                if (size == compressedSize) {
                    file.Paste(buffer);
                    continue;
                }

                var decompressed = new Span<byte>(new byte[size]);
                var bytes = DecompressLZF(buffer.Span, decompressed);
                file.Paste(decompressed[..bytes]);
            }
        }

        Debug.Assert(file.Cursor == entry.Size, "file.Buffer.Length == Size");

        return file.Buffer;
    }

    public static int DecompressLZF(ReadOnlySpan<byte> compressedData, Span<byte> decompressedData) {
        var compPos = 0;
        var decompPos = 0;

        while (compPos < compressedData.Length) {
            var codeWord = compressedData[compPos++];
            if (codeWord <= 0x1F) {
                // Encode literal
                for (int i = codeWord; i >= 0; --i) {
                    decompressedData[decompPos] = compressedData[compPos];
                    ++decompPos;
                    ++compPos;
                }
            } else {
                // Encode dictionary
                var copyLen = codeWord >> 5; // High 3 bits are copy length
                if (copyLen == 7) { // If those three make 7, then there are more bytes to copy (maybe)
                    copyLen += compressedData[compPos++]; // Grab next byte and add 7 to it
                }

                var dictDist = ((codeWord & 0x1f) << 8) | compressedData[compPos]; // 13 bits code lookback offset
                ++compPos;
                copyLen += 2; // Add 2 to copy length

                var decompDistBeginPos = decompPos - 1 - dictDist;

                for (var i = 0; i < copyLen; ++i, ++decompPos) {
                    decompressedData[decompPos] = decompressedData[decompDistBeginPos + i];
                }
            }
        }

        return decompPos;
    }
}
