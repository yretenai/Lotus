// SPDX-License-Identifier: MPL-2.0

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using DragonLib;

namespace Lotus.IO;

public class CursoredMemoryMarshal {
    protected CursoredMemoryMarshal() { }

    public CursoredMemoryMarshal(Memory<byte> buffer, int cursor = 0) {
        SetBuffer(buffer);
        FlushBits();
        Cursor = cursor;
    }

    public int Bits { get; private set; } = -1;
    public Memory<byte> Buffer { get; private set; }
    public int Cursor { get; set; }
    public int Shift { get; private set; }

    public int Left => Buffer.Length - Cursor;

    protected void SetBuffer(Memory<byte> buffer) {
        Buffer = buffer;
    }

    public T Read<T>(int alignment = 0) where T : struct {
        if (Left < Unsafe.SizeOf<T>()) {
            return default;
        }

        var value = MemoryMarshal.Read<T>(Buffer[Cursor..].Span);
        Cursor += Unsafe.SizeOf<T>();
        if (alignment > 0) {
            Cursor = Cursor.Align(alignment);
        }

        return value;
    }

    public void FlushBits() {
        Shift = int.MaxValue;
    }

    public ulong ReadBits(int bits) {
        if (bits > 64) {
            throw new NotSupportedException();
        }

        var returnValue = 0UL;

        while (bits > 0) {
            if (Shift >= 8) {
                Bits = Read<byte>();
                Shift = 0;
            }

            var localBits = Math.Min(8 - Shift, bits);
            var mask = (1 << localBits) - 1;
            var value = (Bits >> Shift) & mask;
            Shift += localBits;

            returnValue |= (byte) value;
            bits -= localBits;
            if (bits > 0) {
                returnValue <<= localBits;
            }
        }

        return returnValue;
    }

    public bool ReadBit() {
        if (Shift >= 8) {
            Bits = Read<byte>();
            Shift = 0;
        }

        var value = (Bits >> Shift) & 1;
        Shift++;

        return value == 1;
    }

    public Memory<byte> Slice(int size) {
        var slice = Buffer.Slice(Cursor, size);
        Cursor += size;
        return slice;
    }

    public CursoredMemoryMarshal Part(int size) {
        var slice = Buffer.Slice(Cursor, size);
        Cursor += size;
        return new CursoredMemoryMarshal(slice);
    }

    public Memory<byte> Slice() => Buffer[Cursor..];

    public CursoredMemoryMarshal Part() => new(Buffer[Cursor..]);

    public T Peek<T>() where T : struct => MemoryMarshal.Read<T>(Buffer[Cursor..].Span);

    public string ReadString(int size) {
        if (size <= 0) {
            return string.Empty;
        }

        var slice = Buffer.Slice(Cursor, size);
        Cursor += size;
        return Encoding.UTF8.GetString(slice.Span);
    }

    public string ReadString() => ReadString(Read<int>());

    public ulong ReadULEB(int maxBits = 64) {
        ulong result = 0;
        byte byteReadJustNow;

        for (var shift = 0; shift < maxBits - 1; shift += 7) {
            byteReadJustNow = Buffer.Span[Cursor++];
            result |= (byteReadJustNow & 0x7Ful) << shift;

            if (byteReadJustNow <= 0x7Fu) {
                return result;
            }
        }

        byteReadJustNow = Buffer.Span[Cursor++];
        result |= (ulong) byteReadJustNow << (maxBits - 1);
        return result;
    }

    public MemoryHandle Pin() => Buffer[Cursor..].Pin();
}
