// SPDX-License-Identifier: MPL-2.0

using System.Collections.Generic;
using Lotus.ContentCache.IO;
using Lotus.Types.Config;

namespace Lotus.Types;

public class DependentCacheFile : CacheFile {
    public DependentCacheFile(CursoredMemoryMarshal buffer, string filePath, string config) : base(buffer, filePath, config) {
        var count = buffer.Read<int>();
        Dependencies.EnsureCapacity(count);
        for (var i = 0; i < count; ++i) {
            Dependencies[i] = buffer.ReadString();
        }

        var fileConfigLength = buffer.Read<int>();
        FileConfig = ConfigReader.ReadSections(buffer.ReadString(fileConfigLength), filePath, "");
        if (fileConfigLength > 0) {
            FileConfigCount = (int) buffer.ReadULEB(32);
        }
    }

    public List<string> Dependencies { get; } = [];
    public Dictionary<string, ConfigSection> FileConfig { get; }
    public int FileConfigCount { get; }
}
