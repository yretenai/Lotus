// lotus project
// Copyright (c) 2023 <https://github.com/yretenai/lotus>
// SPDX-License-Identifier: MPL-2.0

using System;
using System.IO;
using Lotus.Cache;

namespace Lotus.ExtractCache;

public static class Program {
    public static void Main(string[] args) {
        if (args.Length < 2) {
            Console.WriteLine("Usage: Lotus.ExtractCache.exe path/to/warframe/install path/to/export");
            return;
        }

        foreach (var toc in Directory.EnumerateFiles(Path.Combine(args[0], "Cache.Windows"), "*.toc", SearchOption.TopDirectoryOnly)) {
            Console.WriteLine($"Loading {Path.GetFileNameWithoutExtension(toc)}");
            CacheManager.Instance.LoadTable(toc);
        }

        foreach (var (sourcePath, type, language, data, entry) in CacheManager.Instance) {
            if (entry.IsDirectory) {
                continue;
            }

            Console.WriteLine(sourcePath);
            var path = Path.Combine(args[1], type.ToString("G"), language, sourcePath[1..]);
            if (data.Length == 0) {
                continue;
            }

            var dir = Path.GetDirectoryName(path) ?? args[1];
            if (!Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            if (!Path.HasExtension(path)) {
                path += ".bin";
            }

            var fi = new FileInfo(path);
            using (var @out = fi.Create()) {
                @out.Write(data.Span);
                @out.Flush();
            }

            fi.CreationTimeUtc = fi.LastWriteTimeUtc = DateTime.FromFileTimeUtc(entry.Time);
        }

        CacheManager.Instance.Dispose();
    }
}