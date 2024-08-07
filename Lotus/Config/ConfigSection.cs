// SPDX-License-Identifier: MPL-2.0

using System.Collections.Generic;

namespace Lotus.Config;

public record ConfigSection(string Name) {
    public Dictionary<string, object> Values { get; set; } = [];
    public string? Type { get; set; }
}
