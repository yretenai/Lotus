// lotus project
// Copyright (c) 2023 <https://github.com/yretenai/lotus>
// SPDX-License-Identifier: MPL-2.0

using System.Collections.Generic;

namespace Lotus.Types.Config;

public record ConfigSection(string Name, string Type) {
    public Dictionary<string, object> Values { get; } = new();
}
