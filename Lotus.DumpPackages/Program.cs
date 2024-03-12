// lotus project
// Copyright (c) 2023 <https://github.com/yretenai/lotus>
// SPDX-License-Identifier: MPL-2.0

using System;
using System.IO;
using System.Text;
using Lotus.Cache;
using Lotus.Types;

namespace Lotus.DumpPackages;

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

        TypeFactory.Instance.LoadPackages();

        var logPath = Path.Combine(args[1], "Config", "Packages.log");
        var logDir = Path.GetDirectoryName(logPath) ?? args[1];
        if (!Directory.Exists(logDir)) {
            Directory.CreateDirectory(logDir);
        }

        using var log = new StreamWriter(File.OpenWrite(logPath), Encoding.UTF8, -1, false);

        foreach (var (sourcePath, info) in TypeFactory.Instance.Packages!.EntityRegistry) {
            log.WriteLine($"PackageEntry {{ PackageName = {info.PackageName}, Parent = {info.ParentFile}, Name = {info.FileName} }}");
            Console.WriteLine(sourcePath);
            var path = Path.Combine(args[1], "Config", sourcePath[1..] + ".cfg");
            var dir = Path.GetDirectoryName(path) ?? args[1];
            if (!Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(path, TypeFactory.Instance.BuildTypeConfig(sourcePath).Config);
        }
    }
}
