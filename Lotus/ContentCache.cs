// SPDX-License-Identifier: MPL-2.0

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Lotus.Infrastructure;
using Lotus.Structs;
using Serilog;

namespace Lotus;

public sealed record CacheKey(ContentType Type, LanguageCode Locale, string Path) {
    public override int GetHashCode() => HashCode.Combine(Type, Locale, Path.ToLowerInvariant());
}

public sealed record CacheEntry(string Path, ContentType Type, LanguageCode Locale, IMemoryOwner<byte>? Data, TableEntry Entry) : IDisposable {
    public static CacheEntry Default { get; } = new(string.Empty, ContentType.Header, LanguageCode.Global, null, default);

    public void Dispose() {
        Data?.Dispose();
    }
}

public sealed class ContentCache : IDisposable, IEnumerable<CacheEntry> {
    public static ContentCache Instance { get; } = new();

    public IEnumerable<string> Names {
        get {
            foreach (var ((_, _, path), _) in Entries) {
                yield return path;
            }
        }
    }

    public IEnumerable<CacheKey> Keys => Entries.Keys;

    private Dictionary<string, ContentTable> Tables { get; } = new();
    private Dictionary<CacheKey, CacheValue> Entries { get; } = new();

    public void Dispose() {
        foreach (var (_, table) in Tables) {
            table.Dispose();
        }

        Tables.Clear();
        Entries.Clear();
    }

    public IEnumerator<CacheEntry> GetEnumerator() {
        foreach (var ((contentType, locale, path), value) in Entries) {
            yield return ReadFile(value, contentType, locale, path);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void LoadTable(string path) {
        var fullName = Path.GetFileNameWithoutExtension(path);
        var namePart = fullName.Split('_');
        var type = (ContentType) namePart[0][0];
        var name = namePart[0][2..];
        var language = (namePart.Length > 1 ? namePart[1] : "global").ToLanguageCode();
        Log.Information("[{Category}] Loading Cache TOC {Name} (Type: {Type:G}, Locale: {Locale})", "Lotus/Cache", name, type, language);
        var table = Tables[fullName] = new ContentTable(File.OpenRead(path), File.OpenRead(Path.ChangeExtension(path, ".cache"))) {
            Locale = language,
            Name = name,
            Type = type,
        };

        foreach (var (filePath, index) in table.Files) {
            Entries[new CacheKey(type, language, filePath)] = new CacheValue(fullName, index);
        }
    }

    public CacheEntry ReadHeader(string path, LanguageCode locale = LanguageCode.Global) {
        while (true) {
            var record = ReadFile(new CacheKey(ContentType.Header, locale, path));

            if (record.Data == null && locale == LanguageCode.Global) {
                locale = LanguageCode.English;
                continue;
            }

            return record;
        }
    }

    public CacheEntry ReadData(string path, LanguageCode locale = LanguageCode.Global) {
        while (true) {
            var record = ReadFile(new CacheKey(ContentType.Full, locale, path));
            if (record.Data == null) {
                record = ReadFile(new CacheKey(ContentType.Base, locale, path));
            }

            if (record.Data == null && locale == LanguageCode.Global) {
                locale = LanguageCode.English;
                continue;
            }

            return record;
        }
    }

    private CacheEntry ReadFile(CacheKey key) => !Entries.TryGetValue(key, out var value) ? CacheEntry.Default : ReadFile(value, key.Type, key.Locale, key.Path);

    private CacheEntry ReadFile(CacheValue value, ContentType type, LanguageCode locale, string path) {
        var (name, index) = value;
        if (!Tables.TryGetValue(name, out var table) || index >= table.Entries.Length) {
            return CacheEntry.Default;
        }

        var entry = table.Entries[index];

        return entry.IsDirectory ? CacheEntry.Default : new CacheEntry(path, type, locale, entry.Read(table.Cache), entry);
    }

    private sealed record CacheValue(string Name, int Index) {
        public override int GetHashCode() => HashCode.Combine(Name.ToLowerInvariant(), Index);
    }
}
