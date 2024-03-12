// SPDX-License-Identifier: MPL-2.0

using System;
using System.Runtime.InteropServices;

namespace Lotus.Struct.Types.EE;

[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 0x14)]
public record struct CacheManifestEntry {
    public Guid Id { get; init; }
    public uint Flags { get; init; }
}
