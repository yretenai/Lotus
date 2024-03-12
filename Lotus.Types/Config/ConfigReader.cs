// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.IO;

namespace Lotus.Types.Config;

public static class ConfigReader {
    public static Dictionary<string, ConfigSection> ReadSections(string config, string rootSection, string rootType) {
        var sections = new Dictionary<string, ConfigSection>();
        if (config.Trim().Length is 0) {
            return sections;
        }

        var currentSection = new ConfigSection(rootSection, rootType);
        sections[currentSection.Name] = currentSection;

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
                if (currentSection.Values.Count is 0) {
                    sections.Remove(currentSection.Name);
                }

                currentSection = new ConfigSection(split[0], split[1]);
                sections[currentSection.Name] = currentSection;
                continue;
            }

            var eqPos = line.IndexOf('=', StringComparison.Ordinal);
            if (eqPos < 1) {
                throw new InvalidDataException();
            }

            currentSection.Values[line[..eqPos]] = ParseValue(ref lineIndex, lines, line[(eqPos + 1)..]);
        }

        return sections;
    }

    private static object ParseValue(ref int lineIndex, string[] lines, string v) {
        switch (v[0]) {
            case '{' when v.Length > 1: {
                if (v[1] is '}') {
                    return Array.Empty<object>();
                }

                throw new NotSupportedException();
            }
            case '{': {
                var nextLine = lines[lineIndex].Trim();
                if (nextLine is "{" || nextLine.EndsWith(',')) // array
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

        return ob;
    }

    private static List<object> ParseArray(ref int lineIndex, string[] lines) {
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
        return list;
    }
}
