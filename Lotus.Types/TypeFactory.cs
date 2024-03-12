// lotus project
// Copyright (c) 2023 <https://github.com/yretenai/lotus>
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Lotus.Cache;
using Lotus.Struct;
using Lotus.Types.EE;

namespace Lotus.Types;

public sealed class TypeFactory {
    private TypeFactory() {
        var asm = Assembly.GetExecutingAssembly();
        foreach (var type in asm.GetTypes()) {
            if (!type.IsClass || type.Namespace?.StartsWith("Lotus.Types.") == false ||
                !type.IsAssignableTo(typeof(CacheFile))) {
                continue;
            }

            var parts = type.FullName!.Split('.')[2..];
            if (parts.Length < 2) {
                continue;
            }

            TypeCache[$"/{parts[0]}/Types/{string.Join('/', parts[1..])}"] = type;

            foreach (var aliasAttribute in type.GetCustomAttributes<TypeAliasAttribute>()) {
                if (!TypeCache.ContainsKey(aliasAttribute.Alias)) {
                    var alias = aliasAttribute.Alias;
                    if (!alias.Contains('.', StringComparison.Ordinal)) {
                        alias = $"/{parts[0]}/Types/{string.Join('/', parts[1..^1])}/{alias}";
                    }

                    TypeCache[alias] = type;
                }
            }
        }
    }

    public static TypeFactory Instance { get; } = new();
    public Dictionary<string, Type> TypeCache { get; } = new();
    public Packages? Packages { get; internal set; }
    public Languages? Languages { get; internal set; }

    public CacheFile? CreateTypeInstance(string path, string? typeHint = null, string locale = "global") {
        if (Packages == null && string.IsNullOrEmpty(typeHint)) {
            throw new InvalidOperationException($"Call TypeFactory.Instance.{nameof(LoadPackages)}");
        }

        var (typeList, config) = BuildTypeConfig(path);
        if (typeList.Count == 0) {
            if (string.IsNullOrEmpty(typeHint)) {
                return default;
            }

            typeList.Add(typeHint);
        }

        var data = CacheManager.Instance.ReadHeader(path, locale).Data;
        if (data.IsEmpty) {
            Debug.WriteLine($"{path} does not exist", "Lotus/TypeFactory");
            return default;
        }

        var cursor = new CursoredMemoryMarshal(data);
        return CreateTypeInstance(cursor, path, typeList, config);
    }

    public (HashSet<string> TypeList, string Config) BuildTypeConfig(string path) {
        var set = new HashSet<string>();
        var config = string.Empty;
        if (Packages == null) {
            return (set, config);
        }

        while (true) {
            if (!Packages.TryGetEntity(path, out var entity)) {
                break;
            }

            var newConfig = $"[{entity.PackageName}{entity.FileName}]\n{entity.Content}\n{config}";
            config = newConfig;
            set.Add(entity.ParentFile);

            if (entity.ParentFile.Length > 0) {
                path = entity.ParentFile;
                continue;
            }

            break;
        }

        return (set, config);
    }

    public CacheFile? CreateTypeInstance(CursoredMemoryMarshal data, string name, HashSet<string> typeNames, string config) {
        foreach (var typeName in typeNames) {
            if (!TypeCache.TryGetValue(typeName, out var type)) {
                continue;
            }

            try {
                data.Cursor = 0;
                var instance = Activator.CreateInstance(type, data, name, config) as CacheFile;
            #if DEBUG
                if (data.Left > 0) {
                    Debug.WriteLine($"Unconsumed data on type {typeName} with file {name}!");
                }
            #endif
                return instance;
            } catch (Exception e) {
                Debug.WriteLine($"Error while trying to initialize {typeName}\n{e}", "Lotus/TypeFactory");
                return default;
            }
        }

        return default;
    }

    public T? CreateTypeInstance<T>(string path, string? typeHint = null, string locale = "global") where T : CacheFile {
        var instance = CreateTypeInstance(path, typeHint, locale);
        if (instance == null) {
            return default;
        }

        if (instance is not T t) {
            Debug.WriteLine($"Tried to convert {instance.GetType().FullName} to incompatible type {typeof(T).FullName}", "Lotus/TypeFactory");
            return default;
        }

        return t;
    }

    public T? CreateTypeInstance<T>(CursoredMemoryMarshal data, string name, string type, string config) where T : CacheFile {
        var instance = CreateTypeInstance(data, name, [type], config);

        if (instance == null) {
            return default;
        }

        if (instance is not T t) {
            Debug.WriteLine($"Tried to convert {instance.GetType().FullName} to incompatible type {typeof(T).FullName}", "Lotus/TypeFactory");
            return default;
        }

        return t;
    }

    public void LoadPackages() {
        Packages = CreateTypeInstance<Packages>("/Packages.bin", "/EE/Types/Packages");
    }

    public void LoadLanguages(string locale = "en") {
        Languages = CreateTypeInstance<Languages>("/Languages.bin", "/EE/Types/Languages", locale);
    }
}
