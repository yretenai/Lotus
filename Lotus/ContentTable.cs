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
        var count = bufferSize / 0x60;
        EntriesPooled = MemoryPool<TableEntry>.Shared.Rent(count);
        var entriesSpan = EntriesPooled.Memory.Span;
        using var bufferPin = buffer.Memory.Pin();
        for (var offset = 0; offset < count; ++offset) {
            entriesSpan[offset] = Marshal.PtrToStructure<TableEntry>((IntPtr) bufferPin.Pointer + offset * 0x60);
        }

        var directoryMap = new Dictionary<int, int>();
        var paths = ArrayPool<string>.Shared.Rent(count);
        var directoryId = 0;
        Files.EnsureCapacity(count);
        for (var index = 0; index < count; index++) {
            var entry = entriesSpan[index];
            var parent = "";
            if (entry.Parent > index) {
                throw new InvalidOperationException();
            }

            if (entry.Parent > 0) {
                parent = paths[directoryMap[entry.Parent - 1]];
            }

            if (entry.IsDirectory) {
                directoryMap[directoryId++] = index;
            }

            var path = paths[index] = parent + "/" + entry.Name;

            if (!entry.IsDirectory) {
                if (Files.TryGetValue(path, out var oldEntry)) {
                    if (entriesSpan[oldEntry].Time > entry.Time) {
                        Log.Debug("[{Category}] Trying to overwrite cache entry?", "Lotus/Cache");
                        continue;
                    }
                }

                Log.Verbose("[{Category}] Found TOC Entry {Path}", "Lotus/Cache", path);
                Files[path] = index;
            }
        }
    }

    private IMemoryOwner<TableEntry> EntriesPooled { get; }
    public Span<TableEntry> Entries => EntriesPooled.Memory.Span;
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
        EntriesPooled.Dispose();
        Cache.Dispose();
    }
}
