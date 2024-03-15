// SPDX-License-Identifier: MPL-2.0

using System.Collections.Generic;
using System.Diagnostics;
using Lotus.IO;
using Lotus.Structs.EE;

namespace Lotus.Types.EE;

public record Cache : CacheFile {
    public Cache(CursoredMemoryMarshal buffer, string filePath, string config) : base(buffer, filePath, config) {
        Version = buffer.Read<int>();

        if (Version < 13 && !Debugger.IsAttached) {
            return;
        }

        var count = buffer.Read<int>();
        Files.EnsureCapacity(count);
        for (var i = 0; i < count; ++i) {
            var key = buffer.ReadString();
            var value = buffer.Read<CacheManifestEntry>();
            Files[key] = value;
        }

        count = buffer.Read<int>();
        Packages.EnsureCapacity(count);
        for (var i = 0; i < count; ++i) {
            var key = buffer.ReadString();
            var value = buffer.Read<CacheManifestEntry>();
            Packages[key] = value;
        }
    }

    public int Version { get; }
    public Dictionary<string, CacheManifestEntry> Files { get; } = [];
    public Dictionary<string, CacheManifestEntry> Packages { get; } = [];
}
