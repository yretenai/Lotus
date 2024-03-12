// lotus project
// Copyright (c) 2023 <https://github.com/yretenai/lotus>
// SPDX-License-Identifier: MPL-2.0

using System.IO;
using Lotus.Struct;

namespace Lotus.Types.EE;

public class Languages : CacheFile {
    public Languages(CursoredMemoryMarshal buffer, string filePath, string config) : base(buffer, filePath, config) {
        File.WriteAllBytes("Languages.bin", buffer.Buffer.ToArray());
        // todo
    }
}
