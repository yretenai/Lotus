// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lotus.Infrastructure;

public sealed class Deferrable : IDisposable, IAsyncDisposable {
    public List<IDisposable> Disposables { get; set; } = [];

    public async ValueTask DisposeAsync() {
        foreach (var disposable in Disposables) {
            if (disposable is IAsyncDisposable disposableAsyncDisposable) {
                await disposableAsyncDisposable.DisposeAsync();
            } else {
                disposable.Dispose();
            }
        }
    }

    public void Dispose() {
        foreach (var disposable in Disposables) {
            disposable.Dispose();
        }
    }
}
