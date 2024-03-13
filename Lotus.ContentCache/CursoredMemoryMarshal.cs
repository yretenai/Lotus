// SPDX-License-Identifier: MPL-2.0

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using DragonLib;

namespace Lotus.ContentCache;

public class CursoredMemoryMarshal {
    public int Bits = -1;
    public Memory<byte> Buffer;
    public int Cursor;
    public int Shift = int.MaxValue;

    public CursoredMemoryMarshal() { }

    public CursoredMemoryMarshal(Memory<byte> buffer, int cursor = 0) {
        Buffer = buffer;
        Cursor = cursor;
    }

    public int Left => Buffer.Length - Cursor;

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

    public byte ReadBits(int bits) {
        if (Shift >= 8) {
            Bits = Read<byte>();
            Shift = 0;
        }

        var mask = (1 << bits) - 1;
        var value = (Bits >> Shift) & mask;
        Shift += bits;
        return (byte) value;
    }

    public ReadOnlySpan<T> Cast<T>(int count, int alignment = 0) where T : struct {
        var size = Unsafe.SizeOf<T>() * count;
        var value = MemoryMarshal.Cast<byte, T>(Buffer[Cursor..(Cursor + size)].Span);
        Cursor += size;
        if (alignment > 0) {
            Cursor = Cursor.Align(alignment);
        }

        return value;
    }

    public ReadOnlySpan<T> CastInter<T>(int count, int alignment = 0) where T : struct {
        Span<T> array = new T[count];
        for (var i = 0; i < count; ++i) {
            array[i] = Read<T>(alignment);
        }

        return array;
    }

    public void EnsureSpace(int size) {
        if (Buffer.Length >= size) {
            return;
        }

        var tmp = new Memory<byte>(new byte[size]);
        Buffer.CopyTo(tmp);
        Buffer = tmp;
    }

    public void Paste(Memory<byte> buffer) {
        EnsureSpace(Cursor + buffer.Length);
        buffer.CopyTo(Buffer[Cursor..]);
        Cursor += buffer.Length;
    }

    public void Paste(Span<byte> buffer) {
        EnsureSpace(Cursor + buffer.Length);
        buffer.CopyTo(Buffer[Cursor..].Span);
        Cursor += buffer.Length;
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

    // TODO: Map SpanHelper methods to CursoredMemoryMarshal.
    public MemoryHandle Pin() => Buffer[Cursor..].Pin();
}
