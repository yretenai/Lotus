// SPDX-License-Identifier: MPL-2.0

using System;
using System.IO;
using Lotus.Config;
using Lotus.IO;

namespace Lotus.Types;

public record CacheFile(Guid Guid, string FilePath, ConfigSections Config) {
    protected CacheFile(CursoredMemoryMarshal buffer, string FilePath, string Config) : this(buffer.Read<Guid>(), FilePath, ConfigReader.ReadSections(Config)) { }

    public string Package => Path.GetFileName(FilePath);
    public string Name => Path.GetDirectoryName(FilePath) ?? "/";
}
