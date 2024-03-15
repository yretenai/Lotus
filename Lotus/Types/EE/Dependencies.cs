// SPDX-License-Identifier: MPL-2.0

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Lotus.IO;
using Lotus.Structs.EE;

namespace Lotus.Types.EE;

public record Dependencies : CacheFile {
    public Dependencies(CursoredMemoryMarshal buffer, string filePath, string config) : base(buffer, filePath, config) {
        HeaderSize = buffer.Read<int>();
        Version = buffer.Read<int>();
        Flags = buffer.Read<uint>();

        if (Version < 81 && !Debugger.IsAttached) {
            return;
        }

        Debug.Assert(HeaderSize is 20, "HeaderSize is 20");
        Debug.Assert(Flags is 1, "Flags is 1");

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

    public int HeaderSize { get; }
    public int Version { get; }
    public uint Flags { get; }

    public List<List<DependencyEntry>> Dependency { get; } = [];
}
