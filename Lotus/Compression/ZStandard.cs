// SPDX-License-Identifier: BSD-3-Clause

using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Lotus.Compression;

public enum ZSTDParameter {
    WindowLogMax = 100,
    Format = 1000,
    StableOutBuffer = 1001,
    ForceIgnoreChecksum = 1002,
    RefMultipleDDicts = 1003,
}

public enum ZSTDFormat {
    Normal = 0,
    Magicless = 1,
}

public enum ZSTDDictLoadMethod {
    ByCopy = 0,
    ByRef = 1,
}

public enum ZSTDDictContentType {
    Auto = 0,
    RawContent = 1,
    Full = 2,
}

public enum ZSTDForceIgnoreChecksum {
    ZSTD_d_validateChecksum = 0,
    ZSTD_d_ignoreChecksum = 1,
}

public enum ZSTDRefMultipleDDicts {
    ZSTD_rmd_refSingleDDict = 0,
    ZSTD_rmd_refMultipleDDicts = 1,
}

public sealed partial class ZStandard : IDisposable {
    private static partial class NativeMethods {
        [LibraryImport("libzstd"), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        public static partial nint ZSTD_DCtx_setParameter(nint dctx, ZSTDParameter param, int value);

        [LibraryImport("libzstd"), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        public static unsafe partial nint ZSTD_DCtx_loadDictionary_advanced(nint dctx, byte* dict, nuint dictSize, ZSTDDictLoadMethod loadMethod, ZSTDDictContentType contentType);

        [LibraryImport("libzstd"), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        public static partial nint ZSTD_createDCtx();

        [LibraryImport("libzstd"), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        public static partial nint ZSTD_freeDCtx(nint dctx);

        [LibraryImport("libzstd"), DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        public static unsafe partial nint ZSTD_decompressDCtx(nint dctx, byte* dst, int dstCapacity, byte* src, int srcSize);
    }

    private Memory<byte> Dict { get; set; } = Memory<byte>.Empty;
    private nint DContext { get; set; }
    private MemoryHandle DictPin { get; set; }

    public ZStandard() {
        DContext = NativeMethods.ZSTD_createDCtx();
        if (DContext < 0) {
            throw new UnreachableException();
        }
    }

    public bool SetParameter(ZSTDParameter parameter, int value) {
        var result = NativeMethods.ZSTD_DCtx_setParameter(DContext, parameter, value);
        return result == 0;
    }

    public unsafe bool LoadDict(Memory<byte> dict, ZSTDDictLoadMethod loadMethod = ZSTDDictLoadMethod.ByRef, ZSTDDictContentType contentType = ZSTDDictContentType.Auto) {
        if (dict.IsEmpty) {
            return UnloadDict();
        }

        Dict = dict;
        DictPin = Dict.Pin();
        var result = NativeMethods.ZSTD_DCtx_loadDictionary_advanced(DContext, (byte*) DictPin.Pointer, (nuint) Dict.Length, loadMethod, contentType);
        if (result < 0) {
            FreeDict();
            return false;
        }

        if (loadMethod != ZSTDDictLoadMethod.ByRef) {
            FreeDict();
        }

        return true;
    }

    public unsafe long Decompress(Memory<byte> input, Memory<byte> output) {
        using var inPin = input.Pin();
        using var outPin = output.Pin();
        return NativeMethods.ZSTD_decompressDCtx(DContext, (byte*) outPin.Pointer, output.Length, (byte*) inPin.Pointer, input.Length);
    }

    public unsafe bool UnloadDict() {
        var result = NativeMethods.ZSTD_DCtx_loadDictionary_advanced(DContext, (byte*) nint.Zero, nuint.Zero, 0, 0);
        if ((nint) DictPin.Pointer != IntPtr.Zero) {
            FreeDict();
        }

        return result == 0;
    }

    private unsafe void FreeDict() {
        if ((nint) DictPin.Pointer != IntPtr.Zero) {
            DictPin.Dispose();
            DictPin = default;
            Dict = Memory<byte>.Empty;
        }
    }

    private void FreeContext() {
        if (DContext > 0) {
            DContext = NativeMethods.ZSTD_freeDCtx(DContext);
        }
    }

    private void ReleaseUnmanagedResources() {
        FreeContext();
        FreeDict();
    }

    public void Dispose() {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~ZStandard() {
        ReleaseUnmanagedResources();
    }
}
