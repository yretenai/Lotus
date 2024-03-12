// SPDX-License-Identifier: MPL-2.0

using DragonLib.IO;

namespace Lotus.Struct.Cache;

public record struct BlockFrameHeader {
    [BitField(5)] public byte Unknown1 { get; set; }
    [BitField(29)] public int BlockSize { get; set; }
    [BitField(29)] public int CompressedSize { get; set; }

    // This bit sets the compressed size to a negative value on the old format, which is still used in some cases.
    [BitField(1)] public bool UseNewFormat { get; set; }

    public bool IsCompressed => BlockSize != CompressedSize;
}
