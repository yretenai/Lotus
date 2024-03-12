// lotus project
// Copyright (c) 2023 <https://github.com/yretenai/lotus>
// SPDX-License-Identifier: MPL-2.0

using System.Runtime.InteropServices;

namespace Lotus.Struct.Cache;

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
}
