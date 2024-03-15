// SPDX-License-Identifier: MPL-2.0

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Lotus.Config;

public record ConfigSections(List<ConfigSection> Sections) {
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out object value) {
        value = null;

        for (var i = Sections.Count - 1; i >= 0; ++i) {
            if (Sections[i].Values.TryGetValue(key, out value)) {
                return true;
            }
        }

        return false;
    }

    public static ConfigSections Empty { get; } = new([]);
}
