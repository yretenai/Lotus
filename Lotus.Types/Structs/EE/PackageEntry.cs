// SPDX-License-Identifier: MPL-2.0

namespace Lotus.Types.Structs.EE;

public record PackageEntry(string PackageName, string FileName, string ParentType, string Content) {
    public int Unknown1 { get; init; }
    public int Unknown2 { get; init; }
    public int Unknown3 { get; init; }
    public string FullName => PackageName + FileName;
}

public record PackageRef(string Package, int Unknown);
