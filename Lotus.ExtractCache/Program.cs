// SPDX-License-Identifier: MPL-2.0

using System;
using System.IO;
using DragonLib;
using Lotus.ContentCache;
using Serilog;
using Serilog.Events;

namespace Lotus.ExtractCache;

public static class Program {
    public static void Main(string[] args) {
        if (args.Length < 2) {
            Console.WriteLine("Usage: Lotus.ExtractCache.exe path/to/warframe/install path/to/export");
            return;
        }

        args[1].EnsureDirectoryExists();
        Log.Logger = new LoggerConfiguration()
                    .WriteTo.Console(LogEventLevel.Information)
                    .WriteTo.File(Path.Combine(args[1], "ExtractCache.log"), LogEventLevel.Debug)
                    .CreateLogger();

        foreach (var toc in Directory.EnumerateFiles(Path.Combine(args[0], "Cache.Windows"), "*.toc", SearchOption.TopDirectoryOnly)) {
            CacheManager.Instance.LoadTable(toc);
        }

        foreach (var (sourcePath, type, language, data, entry) in CacheManager.Instance) {
            if (entry.IsDirectory) {
                continue;
            }

            Log.Information("{Path}", sourcePath);
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
