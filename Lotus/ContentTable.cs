// SPDX-License-Identifier: MPL-2.0

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Lotus.Structs;
using Serilog;

namespace Lotus;

public sealed class ContentTable : IDisposable {
    public unsafe ContentTable(Stream stream, Stream cache) {
        Cache = cache;

        var headerBuffer = (stackalloc byte[8]);
        stream.ReadExactly(headerBuffer);
        var header = MemoryMarshal.Cast<byte, uint>(headerBuffer);
        if (header[0] != 0x1867C64E) {
            throw new InvalidDataException("Not a TOC file");
        }

        if (header[1] != 0x14) {
            throw new NotSupportedException($"Version {header[1]} is not supported");
        }

        var bufferSize = (int) (stream.Length - stream.Position);
        using var buffer = MemoryPool<byte>.Shared.Rent(bufferSize);
        if (buffer.Memory.Length < bufferSize) {
            throw new OutOfMemoryException();
        }

        stream.ReadExactly(buffer.Memory.Span[..bufferSize]);
        Entries = new TableEntry[bufferSize / 0x60];
        using var bufferPin = buffer.Memory.Pin();
        for (var offset = 0; offset < bufferSize; offset += 0x60) {
            Entries[offset / 0x60] = Marshal.PtrToStructure<TableEntry>((IntPtr) bufferPin.Pointer + offset);
        }

        var directoryMap = new Dictionary<int, int>();
        Paths = new string[Entries.Length];
        var directoryId = 0;
        for (var index = 0; index < Entries.Length; index++) {
            var entry = Entries[index];
            var parent = "";
            if (entry.Parent > 0) {
                parent = Paths[directoryMap[entry.Parent - 1]];
            }

            if (entry.IsDirectory) {
                directoryMap[directoryId++] = index;
            }

            Paths[index] = parent + "/" + entry.Name;

            if (!entry.IsDirectory) {
                var path = Paths[index];
                if (Files.TryGetValue(path, out var oldEntry)) {
                    if (Entries[oldEntry].Time > entry.Time) {
                        Log.Debug("[{Category}] Trying to overwrite cache entry?", "Lotus/Cache");
                        continue;
                    }
                }

                Log.Verbose("[{Category}] Found TOC Entry {Path}", "Lotus/Cache", path);
                Files[path] = index;
            }
        }
    }

    public TableEntry[] Entries { get; }
    public string[] Paths { get; }
    public Dictionary<string, int> Files { get; } = new();
    public Stream Cache { get; }
    public ContentType Type { get; internal set; }
    public string? Name { get; internal set; }
    public LanguageCode Locale { get; internal set; }

    public IEnumerable<(string Path, TableEntry Entry)> ManagedFiles {
        get {
            foreach (var (path, index) in Files) {
                yield return (path, Entries[index]);
            }
        }
    }

    public void Dispose() {
        Cache.Dispose();
    }
}
