// SPDX-License-Identifier: MPL-2.0

namespace Lotus.Structs;

public record struct BlockFrameHeader(ulong Value) {
    // bits 1-5
    public BlockCompressionType CompressionType {
        get => (BlockCompressionType) (Value & 0x1FUL);
        set => Value = (Value & 0xFFFFFFFFFFFFFFE0UL) | ((byte) value & 0x1FUL);
    }

    // bits 6-33
    public int BlockSize {
        get => (int) ((Value >> 5) & 0x1FFFFFFFUL);
        set => Value = (Value & 0xFFFFFFFFE0000000UL) | (((ulong) value & 0x1FFFFFFFUL) << 5);
    }

    // bits 35-63
    public int CompressedSize {
        get => (int) ((Value >> 34) & 0x1FFFFFFFUL);
        set => Value = (Value & 0x80000003FFFFFFFFUL) | (((ulong) value & 0x1FFFFFFFUL) << 34);
    }

    // This bit sets the compressed size to a negative value on the old format, which is still used in some cases.
    // bit 64
    public bool UseNewFormat {
        get => Value >> 63 == 1;
        set => Value = (Value & 0x7FFFFFFFFFFFFFFFUL) | (value ? 0x8000000000000000UL : 0);
    }

    public bool IsCompressed => BlockSize != CompressedSize;
}
