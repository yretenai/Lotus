// SPDX-License-Identifier: MPL-2.0

using System.Runtime.InteropServices;
using Lotus.ContentCache;

namespace Lotus.Types.Structs.EE;

[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 4)]
public readonly record struct DependencyRef(uint Value) {
    public int PackageId => (int) (Value & 0xFFFF);
    public int EntryId => (int) ((Value >> 16) & 0x1FFF);
    public int Flags => (int) (Value >> 29);
    public DependencyEntry? Resolved => TypeFactory.Instance.Dependencies?.Dependency.SafelyGet(PackageId)?.SafelyGet(EntryId);
}

public record DependencyEntry(string Path, DependencyRef[] Dependencies);
