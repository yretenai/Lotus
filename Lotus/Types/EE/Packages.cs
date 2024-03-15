// SPDX-License-Identifier: MPL-2.0

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Lotus.Compression;
using Lotus.Infrastructure;
using Lotus.IO;
using Lotus.Structs.EE;

namespace Lotus.Types.EE;

// This is how you determine file type.
public record Packages : CacheFile {
    public Packages(CursoredMemoryMarshal buffer, string filePath, string config) : base(buffer, filePath, config) {
        HeaderSize = buffer.Read<int>();
        Version = buffer.Read<int>();
        Flags = buffer.Read<uint>();

        if (Version < 31 && !Debugger.IsAttached) {
            return;
        }

        Debug.Assert(HeaderSize is 20, "HeaderSize is 20");
        Debug.Assert(Flags is 1, "Flags is 1");

        if (Version >= 40) {
            Hash = buffer.Read<uint>();
        }

        int count;
        if (Version >= 36) {
            count = buffer.Read<int>();
            Types.EnsureCapacity(count);
            for (var i = 0; i < count; ++i) {
                var str = buffer.ReadString();
                var unknown1 = buffer.Read<short>();
                Types.Add(new PackageRef(str, unknown1));
            }
        }

        count = buffer.Read<int>();
        PackageRegistry.EnsureCapacity(count);
        for (var i = 0; i < count; ++i) {
            var str = buffer.ReadString();
            var unknown1 = buffer.Read<byte>();
            PackageRegistry.Add(new PackageRef(str, unknown1));
        }

        NextConfig nextConfig;
        using var defer = new Deferrable();
        var comFlagsBuffer = new CursoredMemoryMarshal();
        if (Version >= 34) {
            comFlagsBuffer = buffer.Part(buffer.Read<int>());
            var comSizeBuffer = buffer.Part(buffer.Read<int>());
            var comZBuffer = buffer.Slice(buffer.Read<int>());
            var dictsize = comSizeBuffer.Read<int>();
            var zdict = comZBuffer[..dictsize];
            var zbuffer = new CursoredMemoryMarshal(comZBuffer[dictsize..]);

            var decompressor = new ZStandard();
            if (!decompressor.SetParameter(ZSTDParameter.Format, (int) ZSTDFormat.Magicless)) {
                throw new InvalidOperationException();
            }

            if (!decompressor.LoadDict(zdict)) {
                throw new InvalidOperationException();
            }

            defer.Disposables.Add(decompressor);

            nextConfig = () => {
                if (comFlagsBuffer.ReadBits(1) == 1) { // hasText
                    var size = (int) comSizeBuffer.ReadULEB(32);
                    var frameData = zbuffer.Slice(size);
                    if (comFlagsBuffer.ReadBits(1) == 1) { // isCompressed
                        var frame = new CursoredMemoryMarshal(frameData);
                        var dsize = (int) frame.ReadULEB(32);
                        using var buf = MemoryPool<byte>.Shared.Rent(dsize);
                        var bufMem = buf.Memory.Slice(0, dsize);
                        var n = decompressor.Decompress(frame.Slice(), bufMem);
                        if (n != dsize) {
                            throw new InvalidOperationException();
                        }

                        var str = Encoding.ASCII.GetString(bufMem.Span[..(int) n]);
                        return str;
                    }

                    return Encoding.ASCII.GetString(frameData.Span.TrimEnd((byte) 0));
                }

                return string.Empty;
            };
        } else {
            var stringBuffer = buffer.Slice(buffer.Read<int>());
            var stringIndex = 0;
            nextConfig = () => {
                if (stringIndex < stringBuffer.Length) {
                    comFlagsBuffer.ReadBits(1); // isCompressed
                    var textBuffer = stringBuffer[stringIndex++..].Span;
                    var textLength = textBuffer.IndexOf((byte) 0);
                    stringIndex += textLength;
                    return Encoding.ASCII.GetString(textBuffer[..textLength]);
                }

                return string.Empty;
            };
        }

        count = buffer.Read<int>();
        EntityRegistry.EnsureCapacity(count);

        for (var i = 0; i < count; ++i) {
            var packageName = buffer.ReadString();
            var fileName = buffer.ReadString();
            var unknown1 = Version >= 36 ? buffer.Read<ushort>() : buffer.Read<int>();
            var unknown2 = buffer.Read<byte>();
            var parentType = buffer.ReadString();
            var unknown3 = Version < 40 ? buffer.Read<int>() : 0; // cached as string index.

            var text = nextConfig();

            var package = new PackageEntry(packageName, fileName, parentType, text) {
                Unknown1 = unknown1,
                Unknown2 = unknown2,
                Unknown3 = unknown3,
            };
            EntityRegistry[packageName + fileName] = package;

            //   store in type lookup?  store globally?
            if ((unknown1 & 1) == 0 && (unknown1 & 0x400) == 0x400) {
                EntityRegistry[fileName] = package;
            }
        }
    }

    public int HeaderSize { get; }
    public int Version { get; }
    public uint Flags { get; }
    public uint Hash { get; }
    public List<PackageRef> Types { get; } = [];
    public List<PackageRef> PackageRegistry { get; } = [];
    public Dictionary<string, PackageEntry> EntityRegistry { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? this[string path] => TryGetEntity(path, out var entity) ? entity.Content : null;

    public bool TryGetEntity(string path, [MaybeNullWhen(false)] out PackageEntry entry) => EntityRegistry.TryGetValue(path, out entry);

    public bool TryFindParentType(PackageEntry package, [MaybeNullWhen(false)] out PackageEntry entry) {
        entry = null;
        if (package.ParentType.Length == 0) {
            return false;
        }

        if (TryGetEntity(package.ParentType, out entry)) {
            return true;
        }

        var parentType = package.ParentType;
        if (parentType.Length > 0 && !parentType.Contains('/', StringComparison.Ordinal)) {
            parentType = package.PackageName[..(package.PackageName.LastIndexOf('/') + 1)] + parentType;
        }

        while (parentType.Length > 0) {
            if (TryGetEntity(parentType, out entry)) {
                return true;
            }

            var lastSlash = parentType.LastIndexOf('/');
            if (lastSlash <= 0) {
                break;
            }

            var penultimateSlash = parentType.LastIndexOf('/', lastSlash - 1);
            if (penultimateSlash <= 0) {
                break;
            }

            var name = parentType[lastSlash..];
            var parentPath = parentType[..penultimateSlash];
            parentType = parentPath + name;
        }

        return false;
    }

    private delegate string NextConfig();
}
