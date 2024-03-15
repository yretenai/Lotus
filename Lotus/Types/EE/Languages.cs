// SPDX-License-Identifier: MPL-2.0

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Lotus.Compression;
using Lotus.Infrastructure;
using Lotus.IO;
using Lotus.Structs;
using Lotus.Structs.EE;

namespace Lotus.Types.EE;

public record Languages : CacheFile {
    public Languages(CursoredMemoryMarshal buffer, string filePath, string config) : base(buffer, filePath, config) {
        HeaderSize = buffer.Read<int>();
        Version = buffer.Read<int>();
        Flags = buffer.Read<uint>();

        if (Version < 41 && !Debugger.IsAttached) {
            return;
        }

        Debug.Assert(HeaderSize is 20, "HeaderSize is 20");
        Debug.Assert(Flags is 1, "Flags is 1");

        var count = buffer.Read<int>();
        for (var i = 0; i < count; ++i) {
            Suffixes.Add(buffer.ReadString().ToLanguageCode());
        }

        var zdict = buffer.Slice(buffer.Read<int>());
        using var decompressor = new ZStandard();
        if (!decompressor.SetParameter(ZSTDParameter.Format, (int) ZSTDFormat.Magicless)) {
            throw new InvalidOperationException();
        }

        if (!decompressor.LoadDict(zdict)) {
            throw new InvalidOperationException();
        }

        count = buffer.Read<int>();
        for (var i = 0; i < count; ++i) {
            var sectionName = buffer.ReadString();
            var stringBuffer = buffer.Part(buffer.Read<int>());

            var entryCount = buffer.Read<int>();
            var entries = new List<LanguageEntry>();
            for (var j = 0; j < entryCount; ++j) {
                var key = buffer.ReadString();
                var offset = buffer.Read<int>();
                var size = buffer.Read<ushort>();
                var flags = buffer.Read<LanguageFlags>();

                stringBuffer.Cursor = offset;
                var value = stringBuffer.Part(size);

                string text;
                if (flags.HasFlag(LanguageFlags.Compressed)) {
                    var decompressedSize = (int) value.ReadULEB(32);
                    using var textBuffer = MemoryPool<byte>.Shared.Rent(decompressedSize);
                    var textMem = textBuffer.Memory[..decompressedSize];
                    var bytes = decompressor.Decompress(value.Slice(), textMem);
                    if (bytes != decompressedSize) {
                        throw new InvalidOperationException();
                    }

                    text = Encoding.UTF8.GetString(textMem.Span);
                } else {
                    text = Encoding.UTF8.GetString(value.Slice().Span);
                }

                entries.Add(new LanguageEntry(key, text, flags));
            }

            Sections.Add(new LanguageSectionEntry(sectionName, entries));
        }
    }

    public int HeaderSize { get; }
    public int Version { get; }
    public uint Flags { get; }
    public List<LanguageCode> Suffixes { get; } = [];
    public List<LanguageSectionEntry> Sections { get; } = [];
}
