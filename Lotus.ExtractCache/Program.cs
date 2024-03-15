// SPDX-License-Identifier: MPL-2.0

using System;
using System.Diagnostics;
using System.IO;
using DragonLib;
using Lotus.ContentCache;
using Lotus.Types;
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
                    .MinimumLevel.Debug()
                    .WriteTo.Console(LogEventLevel.Information)
                    .WriteTo.File(Path.Combine(args[1], "ExtractCache.log"), LogEventLevel.Debug)
                    .CreateLogger();

        foreach (var toc in Directory.EnumerateFiles(Path.Combine(args[0], "Cache.Windows"), "*.toc", SearchOption.TopDirectoryOnly)) {
            CacheManager.Instance.LoadTable(toc);
        }

        TypeFactory.Instance.LoadPackages();

        foreach (var instance in CacheManager.Instance) {
            try {
                var (sourcePath, type, language, data, entry) = instance;
                if (entry.IsDirectory) {
                    continue;
                }

                var currentPath = sourcePath;
                if (TypeFactory.Instance.Packages!.TryGetEntity(currentPath, out var entity)) {
                    while (TypeFactory.Instance.Packages!.TryFindParentType(entity, out var nextEntity)) {
                        entity = nextEntity;
                    }
                }

                if (!Path.HasExtension(sourcePath)) {
                    Debug.Assert(!string.IsNullOrEmpty(entity?.FileName), "!string.IsNullOrEmpty(entity?.FileName)");
                    Debug.Assert(entity.ParentType.Length == 0, "entity.ParentType.Length == 0");
                    sourcePath += "." + entity.FileName.ToLowerInvariant();
                }

                Log.Information("{Path}", sourcePath);
                var path = Path.Combine(args[1], type.ToString("G"), language.ToString("G"), sourcePath[1..]);
                if (data == null) {
                    continue;
                }

                path.EnsureDirectoryExists();

                var fi = new FileInfo(path);
                using (var output = fi.Create()) {
                    output.Write(data.Memory.Span[..entry.Size]);
                    output.Flush();
                }

                fi.CreationTimeUtc = fi.LastWriteTimeUtc = DateTime.FromFileTimeUtc(entry.Time);
            } finally {
                instance.Dispose();
            }
        }

        CacheManager.Instance.Dispose();
    }
}
