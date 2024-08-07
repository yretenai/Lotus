// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Lotus.Config;

public static class ConfigReader {
    public static ConfigSections ReadSections(string config, string rootSection = "Lotus", string rootType = "/EE/Types/Applet/") {
        var sections = new List<ConfigSection>();
        if (config.Trim().Length is 0) {
            return ConfigSections.Empty;
        }

        var currentSection = new ConfigSection(rootSection) {
            Type = rootType + rootSection,
        };
        var lines = config.Split('\n');
        var lineIndex = 0;
        while (lineIndex < lines.Length) {
            var line = lines[lineIndex++].Trim();

            if (line.Length is 0) {
                continue;
            }

            if (line.StartsWith('+')) {
                continue; // settings value
            }

            if (line[0] is '[') {
                //sections
                var split = line[1..^1].Split(',', 2, StringSplitOptions.TrimEntries);
                if (currentSection.Values.Count > 0) {
                    sections.Add(currentSection);
                }

                currentSection = new ConfigSection(split[0]) {
                    Type = split[1] + split[0],
                };
                continue;
            }

            var eqPos = line.IndexOf('=', StringComparison.Ordinal);
            if (eqPos < 1) {
                throw new InvalidDataException();
            }

            currentSection.Values[line[..eqPos]] = ParseValue(ref lineIndex, lines, line[(eqPos + 1)..]);
        }

        if (currentSection.Values.Count > 0) {
            sections.Add(currentSection);
        }

        return new ConfigSections(sections);
    }

    private static object ParseValue(ref int lineIndex, string[] lines, string v) {
        switch (v[0]) {
            case '{' when v.Length > 1: {
                return v[1] is '}' ? [] : v[1..^1].Split(',');
            }
            case '{': {
                var nextLine = lines[lineIndex].Trim();
                if (nextLine is "{" || nextLine.EndsWith(',') || !nextLine.Contains('=', StringComparison.Ordinal)) // array
                {
                    return ParseArray(ref lineIndex, lines);
                }

                return ParseObject(ref lineIndex, lines);
            }
            case '\"':
                return ReadString(ref lineIndex, lines, v[1..]);
            default:
                return v;
        }
    }

    private static string ReadString(ref int lineIndex, IReadOnlyList<string> lines, string currentLine) {
        var str = string.Empty;
        var cursor = 0;
        var skipNext = false;
        while (true) {
            if (cursor > currentLine.Length) {
                currentLine = lines[lineIndex++];
                cursor = 0;
            }

            var ch = currentLine[cursor++];
            if (!skipNext) {
                if (ch is '"') {
                    break;
                }

                if (ch is '\\') {
                    skipNext = true;
                    continue;
                }
            }

            str += ch;
        }

        return str;
    }

    private static Dictionary<string, object> ParseObject(ref int lineIndex, string[] lines) {
        var ob = new Dictionary<string, object>();
        while (true) {
            var line = lines[lineIndex++].Trim();
            if (line[0] is '}') {
                break;
            }

            var eqPos = line.IndexOf('=', StringComparison.Ordinal);
            if (eqPos < 1) {
                throw new InvalidDataException();
            }

            ob[line[..eqPos]] = ParseValue(ref lineIndex, lines, line[(eqPos + 1)..]);
        }

        if (ob.Count == 0) {
            return ob;
        }

        // polymorphics are `Tag:Key`, usually with an accomanying `Tag:Type`.
        var newOb = new Dictionary<string, object>();
        foreach (var (key, value) in ob) {
            if (!key.Contains(':', StringComparison.Ordinal)) {
                newOb[key] = value;
            }

            var tagParts = key.Split(':', 2);
            if (!newOb.TryGetValue(tagParts[0], out var tagBox)) {
                tagBox = newOb[tagParts[0]] = new ConfigSection(tagParts[0]);
            }

            if (tagBox is not ConfigSection tag) {
                return ob;
            }

            if (tagParts[1] == "Type") {
                if (value is not string str) {
                    return ob;
                }

                tag.Type = str;
                continue;
            }

            tag.Values[tagParts[1]] = value;
        }

        return newOb;
    }

    private static object ParseArray(ref int lineIndex, string[] lines) {
        var list = new List<object>();
        while (true) {
            var line = lines[lineIndex++].Trim();
            if (line.EndsWith(',')) {
                line = line[..^1];
            }

            list.Add(ParseValue(ref lineIndex, lines, line));
            line = lines[lineIndex - 1].Trim();
            if (line[^1] != ',') {
                break;
            }
        }

        lineIndex++;

        // Arrays are sometimes Dictionaries, e.g.
        // Behaviors={
        // fire:Type=/Types/WeaponFireBehavior
        // fire:WeaponFireBehavior={}
        // }
        if (list is [Dictionary<string, object> dict] && dict.Values.All(x => x is ConfigSection)) {
            return dict;
        }

        return list;
    }
}
