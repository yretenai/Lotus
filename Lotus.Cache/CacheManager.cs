// lotus project
// Copyright (c) 2023 <https://github.com/yretenai/lotus>
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Lotus.Struct.Cache;

namespace Lotus.Cache;

public sealed class CacheManager : IDisposable, IEnumerable<CacheManager.CacheManagerEntry> {
    public static CacheManager Instance { get; } = new();

    public IEnumerable<string> Names {
        get {
            foreach (var ((_, _, path), _) in Entries) {
                yield return path;
            }
        }
    }

    public IEnumerable<CacheManagerKey> Keys => Entries.Keys;

    private Dictionary<string, ContentTable> Tables { get; } = new();
    private Dictionary<CacheManagerKey, CacheManagerValue> Entries { get; } = new();

    public void Dispose() {
        foreach (var (_, table) in Tables) {
            table.Dispose();
        }

        Tables.Clear();
        Entries.Clear();
    }

    public IEnumerator<CacheManagerEntry> GetEnumerator() {
        foreach (var ((contentType, locale, path), value) in Entries) {
            var (data, entry) = ReadFile(value);
            yield return new CacheManagerEntry(path, contentType, locale, data, entry);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void LoadTable(string path) {
        var fullName = Path.GetFileNameWithoutExtension(path);
        var namePart = fullName.Split('_');
        var type = (ContentType) namePart[0][0];
        var name = namePart[0][2..];
        var language = namePart.Length > 1 ? namePart[1] : "global";
        var table = new ContentTable(File.OpenRead(path), File.OpenRead(Path.ChangeExtension(path, ".cache"))) {
            Locale = language,
            Name = name,
            Type = type,
        };
        Tables[fullName] = table;

        foreach (var (filePath, index) in table.Files) {
            Entries[new CacheManagerKey(type, language, filePath)] = new CacheManagerValue(fullName, index);
        }
    }

    public (Memory<byte> Data, TableEntry Entry) ReadHeader(string path, string locale = "global") {
        while (true) {
            var (data, entry) = ReadFile(new CacheManagerKey(ContentType.Header, locale, path));
            if (data.IsEmpty && locale == "global") {
                locale = "en";
                continue;
            }

            return (data, entry);
        }
    }

    public (Memory<byte> Data, TableEntry Entry, bool FullRes) ReadData(string path, string locale = "global") {
        while (true) {
            var (data, entry) = ReadFile(new CacheManagerKey(ContentType.Full, locale, path));
            var full = !data.IsEmpty;
            if (data.IsEmpty) {
                (data, entry) = ReadFile(new CacheManagerKey(ContentType.Base, locale, path));
            }

            if (data.IsEmpty && locale == "global") {
                locale = "en";
                continue;
            }

            return (data, entry, full);
        }
    }

    private (Memory<byte> Data, TableEntry Entry) ReadFile(CacheManagerKey key) => !Entries.TryGetValue(key, out var value) ? (Memory<byte>.Empty, default) : ReadFile(value);

    private (Memory<byte> Data, TableEntry Entry) ReadFile(CacheManagerValue value) {
        var (name, index) = value;
        if (!Tables.TryGetValue(name, out var table) || index >= table.Entries.Length) {
            return (Memory<byte>.Empty, default);
        }

        var entry = table.Entries[index];

        return entry.IsDirectory ? (Memory<byte>.Empty, entry) : (entry.Read(table.Cache), entry);
    }

    public record CacheManagerKey(ContentType Type, string Locale, string Path) {
        public override int GetHashCode() => HashCode.Combine(Type, Locale, Path.ToLowerInvariant());
    }

    private record CacheManagerValue(string Name, int Index) {
        public override int GetHashCode() => HashCode.Combine(Name.ToLowerInvariant(), Index);
    }

    public record CacheManagerEntry(string Path, ContentType Type, string Locale, ReadOnlyMemory<byte> Data, TableEntry Entry);
}
