// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;

namespace Lotus.Types.Structs.EE;

[Flags]
public enum LanguageFlags : ushort {
    HasParams = 0x1,
    HasReference = 0x2,
    UnknownFlag4 = 0x4,
    UnknownFlag8 = 0x8,
    UnknownFlag16 = 0x10,
    UnknownFlag32 = 0x20,
    UnknownFlag64 = 0x40,
    UnknownFlag128 = 0x80,
    UnknownFlag256 = 0x100,
    Compressed = 0x200,
}

public record LanguageEntry(string Key, string Value, LanguageFlags Flags);

public record LanguageSectionEntry(string Name, List<LanguageEntry> Entries);
