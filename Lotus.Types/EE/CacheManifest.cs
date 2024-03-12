// SPDX-License-Identifier: MPL-2.0

using System.Collections.Generic;
using System.Diagnostics;
using Lotus.Struct;
using Lotus.Struct.Types.EE;

namespace Lotus.Types.EE;

public class CacheManifest : CacheFile {
    public CacheManifest(CursoredMemoryMarshal buffer, string filePath, string config) : base(buffer, filePath, config) {
        var unknown1 = buffer.Read<int>();
        Debug.Assert(unknown1 is 13, "unknown1 is 13");

        var count = buffer.Read<int>();
        Files = new Dictionary<string, CacheManifestEntry>(count);
        for (var i = 0; i < count; ++i) {
            var key = buffer.ReadString();
            var value = buffer.Read<CacheManifestEntry>();
            Files[key] = value;
        }

        count = buffer.Read<int>();
        Packages = new Dictionary<string, CacheManifestEntry>(count);
        for (var i = 0; i < count; ++i) {
            var key = buffer.ReadString();
            var value = buffer.Read<CacheManifestEntry>();
            Packages[key] = value;
        }
    }

    public Dictionary<string, CacheManifestEntry> Files { get; }
    public Dictionary<string, CacheManifestEntry> Packages { get; }
}
