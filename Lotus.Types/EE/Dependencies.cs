// SPDX-License-Identifier: MPL-2.0

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Lotus.ContentCache;
using Lotus.Types.Structs.EE;

namespace Lotus.Types.EE;

public class Dependencies : CacheFile {
    public Dependencies(CursoredMemoryMarshal buffer, string filePath, string config) : base(buffer, filePath, config) {
        var unknown1 = buffer.Read<int>();
        Debug.Assert(unknown1 is 20, "unknown1 is 20");
        var unknown2 = buffer.Read<int>();
        Debug.Assert(unknown2 is 82, "unknown2 is 82");
        var unknown3 = buffer.Read<int>();
        Debug.Assert(unknown3 is 1, "unknown3 is 1");

        var dependencyIds = MemoryMarshal.Cast<byte, DependencyRef>(buffer.Slice(buffer.Read<int>() << 2).Span);

        var count = buffer.Read<int>();
        for (var i = 0; i < count; ++i) {
            var package = buffer.ReadString();
            var entries = new List<DependencyEntry>();
            var entryCount = buffer.Read<int>();
            for (var j = 0; j < entryCount; ++j) {
                var path = package + buffer.ReadString();
                var depStart = buffer.Read<int>();
                var depCount = buffer.Read<int>();
                entries.Add(new DependencyEntry(path, depCount > 0 ? dependencyIds.Slice(depStart, depCount).ToArray() : []));
            }

            Dependency.Add(entries);
        }
    }

    public List<List<DependencyEntry>> Dependency { get; } = [];
}
