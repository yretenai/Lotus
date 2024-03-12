// SPDX-License-Identifier: MPL-2.0

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Lotus.Struct;
using Lotus.Struct.Types.EE;
using ZstdNet;

namespace Lotus.Types.EE;

// This is how you determine file type.
public class Packages : CacheFile {
    public Packages(CursoredMemoryMarshal buffer, string filePath, string config) : base(buffer, filePath, config) {
        HeaderSize = buffer.Read<int>();
        Debug.Assert(HeaderSize is 20, "HeaderSize is 20");
        Version = buffer.Read<int>();
        Debug.Assert(Version is >= 31 and <= 40, "Version is >= 31 and <= 40");
        Flags = buffer.Read<int>();
        Debug.Assert(Flags is 1, "Flags is 1");
        if (Version >= 40) {
            Hash = buffer.Read<uint>();
        }

        Types = new List<PackageRef>();
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
        PackageRegistry = new List<PackageRef>();
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

            var decompressorOptions = new DecompressionOptions(zdict.ToArray(), new Dictionary<ZSTD_dParameter, int> {
                { (ZSTD_dParameter) 1000, 1 },
            });
            var decompressor = new Decompressor(decompressorOptions);
            defer.Disposables.Add(decompressorOptions);
            defer.Disposables.Add(decompressor);

            nextConfig = () => {
                if (comFlagsBuffer.ReadBits(1) == 1) { // hasText
                    var size = (int) comSizeBuffer.ReadULEB(32);
                    var frameData = zbuffer.Slice(size);
                    if (comFlagsBuffer.ReadBits(1) == 1) { // isCompressed
                        var frame = new CursoredMemoryMarshal(frameData);
                        var dsize = (int) frame.ReadULEB(32);
                        var buf = ArrayPool<byte>.Shared.Rent(dsize);
                        decompressor.Unwrap(frame.Slice().ToArray(), buf.AsSpan(0, dsize), false);
                        var str = Encoding.ASCII.GetString(buf, 0, dsize);
                        ArrayPool<byte>.Shared.Return(buf);
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
        EntityRegistry = new Dictionary<string, PackageEntity>();
        EntityRegistry.EnsureCapacity(count);

        for (var i = 0; i < count; ++i) {
            var packageName = buffer.ReadString();
            var fileName = buffer.ReadString();
            var unknown1 = Version >= 36 ? buffer.Read<ushort>() : buffer.Read<int>();
            var unknown2 = buffer.Read<byte>();
            var parentType = buffer.ReadString();
            var unknown3 = Version < 40 ? buffer.Read<int>() : 0; // cached as string index.

            var text = nextConfig();

            if (parentType.Length > 0 && !parentType.Contains('/', StringComparison.Ordinal)) {
                Debug.Assert(packageName[0] is '/');
                parentType = packageName[..(packageName.LastIndexOf('/') + 1)] + parentType;
            }

            EntityRegistry[$"{packageName}{fileName}"] = new PackageEntity(packageName, fileName, parentType, text) {
                Unknown1 = unknown1,
                Unknown2 = unknown2,
                Unknown3 = unknown3,
            };
        }
    }

    public int HeaderSize { get; }
    public int Version { get; }
    public int Flags { get; }
    public uint Hash { get; }
    public List<PackageRef> Types { get; }
    public List<PackageRef> PackageRegistry { get; }
    public Dictionary<string, PackageEntity> EntityRegistry { get; }

    public string? this[string path] => TryGetEntity(path, out var entity) ? entity.Content : null;

    public bool TryGetEntity(string path, [MaybeNullWhen(false)] out PackageEntity entity) => EntityRegistry.TryGetValue(path, out entity);

    private delegate string NextConfig();
}
