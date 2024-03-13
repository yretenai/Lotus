// SPDX-License-Identifier: MPL-2.0

using System;
using System.Runtime.InteropServices;

namespace Lotus.Types.Structs.EE;

[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 0x14)]
public readonly record struct CacheManifestEntry(Guid Id, uint Flags);
