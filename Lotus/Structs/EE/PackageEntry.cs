// SPDX-License-Identifier: MPL-2.0

namespace Lotus.Structs.EE;

public record PackageEntry(string PackageName, string FileName, string ParentType, string Content) {
    public int Flags { get; init; }
    public int Flags2 { get; init; }
    public string FullName => PackageName + FileName;
}

public record PackageRef(string Package, int Flags);
