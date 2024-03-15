// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;

namespace Lotus.Structs.EE;

[Flags]
public enum LanguageFlags : ushort {
    HasParams = 0x1,
    HasReference = 0x2,
    Compressed = 0x200,
}

public record LanguageEntry(string Key, string Value, LanguageFlags Flags);

public record LanguageSectionEntry(string Name, List<LanguageEntry> Entries);
