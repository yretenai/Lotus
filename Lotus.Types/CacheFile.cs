// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using Lotus.ContentCache;
using Lotus.Types.Config;

namespace Lotus.Types;

public class CacheFile(CursoredMemoryMarshal buffer, string filePath, string config) {
    public Guid Id { get; } = buffer.Read<Guid>();
    public string FilePath { get; } = filePath;
    public Dictionary<string, ConfigSection> Config { get; } = ConfigReader.ReadSections(config, "Lotus", "/EE/Types/Applet");
}
