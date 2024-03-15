// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using Lotus.Structs;

namespace Lotus.Infrastructure;

public static class Extensions {
    public static T? SafelyGet<T>(this List<T> container, int index) {
        if (index < 0 || index > container.Count) {
            return default;
        }

        return container[index];
    }

    public static LanguageCode ToLanguageCode(this string? str) {
        if (string.IsNullOrEmpty(str)) {
            return LanguageCode.Global;
        }

        return str.ToLower().TrimStart('_') switch {
                   "global" => LanguageCode.Global,
                   "en"     => LanguageCode.English,
                   "de"     => LanguageCode.German,
                   "es"     => LanguageCode.Spanish,
                   "fr"     => LanguageCode.French,
                   "it"     => LanguageCode.Italian,
                   "ja"     => LanguageCode.Japanese,
                   "ko"     => LanguageCode.Korean,
                   "pl"     => LanguageCode.Polish,
                   "pt"     => LanguageCode.Portuguese,
                   "ru"     => LanguageCode.Russian,
                   "tc"     => LanguageCode.TraditionalChinese,
                   "th"     => LanguageCode.Thai,
                   "tr"     => LanguageCode.Turkish,
                   "uk"     => LanguageCode.Ukrainian,
                   "zh"     => LanguageCode.SimplifiedChinese,
                   "xx"     => LanguageCode.Unspecified,
                   _        => throw new NotSupportedException(),
               };
    }

    public static string ToLanguageCode(this LanguageCode code) {
        return code switch {
                   LanguageCode.Global             => "global",
                   LanguageCode.English            => "en",
                   LanguageCode.German             => "de",
                   LanguageCode.Spanish            => "es",
                   LanguageCode.French             => "fr",
                   LanguageCode.Italian            => "it",
                   LanguageCode.Japanese           => "ja",
                   LanguageCode.Korean             => "ko",
                   LanguageCode.Polish             => "pl",
                   LanguageCode.Portuguese         => "pt",
                   LanguageCode.Russian            => "ru",
                   LanguageCode.TraditionalChinese => "tc",
                   LanguageCode.Thai               => "th",
                   LanguageCode.Turkish            => "tr",
                   LanguageCode.Ukrainian          => "uk",
                   LanguageCode.SimplifiedChinese  => "zh",
                   LanguageCode.Unspecified        => "xx",
                   _                               => throw new ArgumentOutOfRangeException(nameof(code), code, null),
               };
    }
}
