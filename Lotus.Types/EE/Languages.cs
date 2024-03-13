// SPDX-License-Identifier: MPL-2.0

using System.IO;
using Lotus.ContentCache;

namespace Lotus.Types.EE;

public class Languages : CacheFile {
    public Languages(CursoredMemoryMarshal buffer, string filePath, string config) : base(buffer, filePath, config) {
        File.WriteAllBytes("Languages.bin", buffer.Buffer.ToArray());
        // todo
    }
}
