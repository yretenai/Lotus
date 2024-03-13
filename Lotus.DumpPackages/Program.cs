// SPDX-License-Identifier: MPL-2.0

using System;
using System.IO;
using System.Text;
using DragonLib;
using Lotus.ContentCache;
using Lotus.Types;
using Serilog;
using Serilog.Events;

namespace Lotus.DumpPackages;

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

        TypeFactory.Instance.LoadCache();
        TypeFactory.Instance.LoadPackages();
        TypeFactory.Instance.LoadDependencies();
        TypeFactory.Instance.LoadLanguages();

        var logPath = Path.Combine(args[1], "Config", "Packages.log");
        var logDir = Path.GetDirectoryName(logPath) ?? args[1];
        if (!Directory.Exists(logDir)) {
            Directory.CreateDirectory(logDir);
        }

        using var log = new StreamWriter(File.OpenWrite(logPath), Encoding.UTF8, -1, false);

        foreach (var (sourcePath, info) in TypeFactory.Instance.Packages!.EntityRegistry) {
            log.WriteLine(info);
            Log.Information("{Path}", sourcePath);
            var path = Path.Combine(args[1], "Config", sourcePath[1..] + ".cfg");
            var dir = Path.GetDirectoryName(path) ?? args[1];
            if (!Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(path, TypeFactory.Instance.BuildTypeConfig(sourcePath).Config);
        }
    }
}
