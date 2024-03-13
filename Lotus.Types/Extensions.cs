// SPDX-License-Identifier: MPL-2.0

using System.Collections.Generic;

namespace Lotus.Types;

public static class Extensions {
    public static T? SafelyGet<T>(this List<T> container, int index) {
        if (index < 0 || index > container.Count) {
            return default;
        }

        return container[index];
    }
}
