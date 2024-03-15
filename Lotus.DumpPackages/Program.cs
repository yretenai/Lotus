// SPDX-License-Identifier: MPL-2.0

using System;
using System.IO;
using DragonLib;
using Lotus.ContentCache;
using Lotus.Types;
using Serilog;
using Serilog.Events;

namespace Lotus.DumpPackages;

public static class Program {
    public static void Main(string[] args) {
        if (args.Length < 2) {
            Console.WriteLine("Usage: Lotus.DumpPackages.exe path/to/warframe/install path/to/export");
            return;
        }

        args[1].EnsureDirectoryExists();
        Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Console(LogEventLevel.Information)
                    .WriteTo.File(Path.Combine(args[1], "DumpPackages.log"), LogEventLevel.Debug)
                    .CreateLogger();

        foreach (var toc in Directory.EnumerateFiles(Path.Combine(args[0], "Cache.Windows"), "*.toc", SearchOption.TopDirectoryOnly)) {
            CacheManager.Instance.LoadTable(toc);
        }

        TypeFactory.Instance.LoadCache();
        TypeFactory.Instance.LoadPackages();
        TypeFactory.Instance.LoadDependencies();
        TypeFactory.Instance.LoadLanguages();

        foreach (var (sourcePath, _) in TypeFactory.Instance.Packages!.EntityRegistry) {
            var config = TypeFactory.Instance.BuildTypeConfig(sourcePath).Config;
            if (string.IsNullOrEmpty(config)) {
                continue;
            }

            Log.Information("{Path}", sourcePath);

            var path = Path.Combine(args[1], "Config", sourcePath[1..] + ".cfg");
            path.EnsureDirectoryExists();

            File.WriteAllText(path, config);
        }
    }
}
