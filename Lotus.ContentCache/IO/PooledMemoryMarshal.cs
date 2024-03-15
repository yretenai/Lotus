using System;
using System.Buffers;

namespace Lotus.ContentCache.IO;

public sealed class PooledMemoryMarshal : CursoredMemoryMarshal, IDisposable {
    public PooledMemoryMarshal(int size, bool dispose = true, int cursor = 0) {
        Owner = MemoryPool<byte>.Shared.Rent(size);
        if (Owner.Memory.Length < size) {
            throw new OutOfMemoryException();
        }

        SetBuffer(Owner.Memory[..size]);
        Cursor = cursor;
        ShouldDispose = dispose;
    }

    public IMemoryOwner<byte> Owner { get; private set; }
    private bool ShouldDispose { get; }

    public void Dispose() {
        if (ShouldDispose) {
            Owner.Dispose();
            Owner = null!;
        }
    }

    public override void EnsureSpace(int size) {
        if (ReadOnly) {
            return;
        }

        if (Buffer.Length >= size) {
            return;
        }

        var tmp = MemoryPool<byte>.Shared.Rent(size);
        if (tmp.Memory.Length < size) {
            throw new OutOfMemoryException();
        }

        Buffer.CopyTo(tmp.Memory);
        Owner.Dispose();
        Owner = tmp;
        SetBuffer(Owner.Memory[..size]);
    }
}
