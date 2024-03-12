// lotus project
// Copyright (c) 2023 <https://github.com/yretenai/lotus>
// SPDX-License-Identifier: MPL-2.0

using System;

namespace Lotus.Types;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class TypeAliasAttribute(string alias) : Attribute {
    public string Alias { get; } = alias;
}
