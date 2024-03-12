// SPDX-License-Identifier: MPL-2.0

namespace Lotus.Struct.Types.EE;

public record PackageEntity(string PackageName, string FileName, string ParentFile, string Content) {
    public int Unknown1 { get; init; }
    public int Unknown2 { get; init; }
    public int Unknown3 { get; init; }
}

public record PackageRef(string Package, int Unknown);
