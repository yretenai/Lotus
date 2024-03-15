// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using Serilog;

namespace Lotus.Compression;

public static class Oodle {
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool Oodle_CheckVersion(uint oodleHeaderVersion, ref uint pOodleLibVersion);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void Oodle_LogHeader();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public delegate bool OodleCore_Plugin_DisplayAssertion([MarshalAs(UnmanagedType.LPStr)] string file, int line, [MarshalAs(UnmanagedType.LPStr)] string function, [MarshalAs(UnmanagedType.LPStr)] string message);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public delegate bool OodleCore_Plugin_Printf(int verboseLevel, [MarshalAs(UnmanagedType.LPStr)] string file, int line, [MarshalAs(UnmanagedType.LPStr)] string format);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.FunctionPtr)]
    public delegate OodleCore_Plugin_DisplayAssertion OodleCore_Plugins_SetAssertion([MarshalAs(UnmanagedType.FunctionPtr)] OodleCore_Plugin_DisplayAssertion rrDisplayAssertion);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.FunctionPtr)]
    public delegate OodleCore_Plugin_Printf OodleCore_Plugins_SetPrintf([MarshalAs(UnmanagedType.FunctionPtr)] OodleCore_Plugin_Printf rrRawPrintf);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.LPStr)]
    public delegate string OodleLZ_Compressor_GetName(OodleLZ_Compressor compressor);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate int OodleLZ_Decompress(byte* srcBuf, int srcSize, byte* rawBuf, int rawSize, int fuzzSafe = 1, int checkCRC = 0, OodleLZ_Verbosity verbosity = OodleLZ_Verbosity.Minimal, byte* decBufBase = null, int decBufSize = 0, byte* fpCallback = null, void* callbackUserData = null, byte* decoderMemory = null, int decoderMemorySize = 0, OodleLZ_Decode_ThreadPhase threadPhase = OodleLZ_Decode_ThreadPhase.Unthreaded);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate OodleLZ_Compressor OodleLZ_GetFirstChunkCompressor(byte* srcBuf, int srcSize, ref bool pIndependent);

    public enum OodleLZ_Compressor {
        Invalid = -1,
        LZH = 0,
        LZHLW = 1,
        LZNIB = 2,
        None = 3,
        LZB16 = 4,
        LZBLW = 5,
        LZA = 6,
        LZNA = 7,
        Kraken = 8,
        Mermaid = 9,
        BitKnit = 10,
        Selkie = 11,
        Hydra = 12,
        Leviathan = 13,
    }

    public enum OodleLZ_Decode_ThreadPhase {
        ThreadPhase1 = 1,
        ThreadPhase2 = 2,
        ThreadPhaseAll = 3,
        Unthreaded = ThreadPhaseAll,
    }

    public enum OodleLZ_Verbosity {
        None = 0,
        Minimal = 1,
        Some = 2,
        Lots = 3,
    }

    // this will return a platform-appropriate library name, wildcarded to suppress prefixes, suffixes and version masks
    // - oo2core_9_win32.dll
    // - oo2core_9_win64.dll
    // - oo2core_9_winuwparm64.dll
    // - liboo2coremac64.2.9.10.dylib
    // - liboo2corelinux64.so.9
    // - liboo2corelinuxarm64.so.9
    // - liboo2corelinuxarm32.so.9
    public static IEnumerable<string> OodleLibName {
        get {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
                switch (RuntimeInformation.ProcessArchitecture) {
                    case Architecture.Arm64:
                        yield return "*oo2core*winuwparm64*.dll";

                        break;
                    case Architecture.X86:
                        yield return "*oo2core*win32*.dll";

                        break;
                }

                yield return "*oo2core*win64*.dll";

                yield break;
            }

            // you can find these in the unreal source post-installation
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
                switch (RuntimeInformation.ProcessArchitecture) {
                    case Architecture.Arm64:
                        yield return "*oo2core*linuxarm64*.so*";

                        break;
                    case Architecture.Arm:
                    case Architecture.Armv6:
                        yield return "*oo2core*linuxarm32*.so*";

                        break;
                }

                yield return "*oo2core*linux64*.so*";

                yield break;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64) {
                    yield return "*oo2core*macarm64*.dylib"; // fixme: this doesn't exist, when they add Apple Silicon builds add me.
                }

                yield return "*oo2core*mac64*.dylib";

                yield break;
            }

            throw new PlatformNotSupportedException();
        }
    }

    [MemberNotNullWhen(true, nameof(DecompressDelegate))]
    public static bool IsReady => DecompressDelegate != null;

    public static OodleLZ_Decompress? DecompressDelegate { get; set; }
    public static OodleLZ_Compressor_GetName? GetNameDelegate { get; set; }
    public static OodleLZ_GetFirstChunkCompressor? GetCompressorDelegate { get; set; }

    public static bool TryFindOodleDll(string? path, [MaybeNullWhen(false)] out string result) {
        path ??= Environment.CurrentDirectory;
        foreach (var oodleLibName in OodleLibName) {
            var files = Directory.GetFiles(path, oodleLibName, SearchOption.TopDirectoryOnly);
            if (files.Length == 0) {
                continue;
            }

            result = files[0];
            return true;
        }

        result = null;
        return false;
    }

    public static string ParseOodleVersion(uint value) {
        var release = value >> 28;
        var check = (value >> 24) & 0xF;
        var major = (value >> 16) & 0xFF;
        var minor = (value >> 8) & 0xFF;
        var table = value & 0xFF;
        return $"{release}.{major}.{minor} (check: {check:X1}, seek: {table})";
    }

    public static uint CreateOodleVersion(int major, int minor, int seekTableSize = 48) => (46u << 24) | (uint) (major << 16) | (uint) (minor << 8) | (uint) seekTableSize;

    public static bool LoadOodleDll(string? path = null, uint oodleVer = 0) {
        if (IsReady) {
            return true;
        }

        path ??= Environment.CurrentDirectory;

        if (Directory.Exists(path) && new FileInfo(path).Attributes.HasFlag(FileAttributes.Directory)) {
            if (!TryFindOodleDll(path, out var oodlePath)) {
                return false;
            }

            path = oodlePath;
        }

        if (string.IsNullOrEmpty(path) || !File.Exists(path)) {
            return false;
        }

        if (!NativeLibrary.TryLoad(path, out var handle)) {
            return false;
        }

        if (!NativeLibrary.TryGetExport(handle, nameof(OodleLZ_Decompress), out var address)) {
            return false;
        }

        DecompressDelegate = Marshal.GetDelegateForFunctionPointer<OodleLZ_Decompress>(address);

        if (NativeLibrary.TryGetExport(handle, nameof(OodleLZ_GetFirstChunkCompressor), out address)) {
            GetCompressorDelegate = Marshal.GetDelegateForFunctionPointer<OodleLZ_GetFirstChunkCompressor>(address);
        }

        if (NativeLibrary.TryGetExport(handle, nameof(OodleLZ_Compressor_GetName), out address)) {
            GetNameDelegate = Marshal.GetDelegateForFunctionPointer<OodleLZ_Compressor_GetName>(address);
        }

        if (NativeLibrary.TryGetExport(handle, "OodleCore_Plugin_Printf_Verbose", out var callbackAddress) ||
            NativeLibrary.TryGetExport(handle, "OodleCore_Plugin_Printf_Default", out callbackAddress)) {
            var callback = Marshal.GetDelegateForFunctionPointer<OodleCore_Plugin_Printf>(callbackAddress);
            if (NativeLibrary.TryGetExport(handle, nameof(OodleCore_Plugins_SetPrintf), out address)) {
                Marshal.GetDelegateForFunctionPointer<OodleCore_Plugins_SetPrintf>(address)(callback);
            }
        }

        if (NativeLibrary.TryGetExport(handle, "OodleCore_Plugin_DisplayAssertion_Default", out callbackAddress)) {
            var callback = Marshal.GetDelegateForFunctionPointer<OodleCore_Plugin_DisplayAssertion>(callbackAddress);
            if (NativeLibrary.TryGetExport(handle, nameof(OodleCore_Plugins_SetAssertion), out address)) {
                Marshal.GetDelegateForFunctionPointer<OodleCore_Plugins_SetAssertion>(address)(callback);
            }
        }

        if (NativeLibrary.TryGetExport(handle, nameof(Oodle_LogHeader), out address)) {
            Marshal.GetDelegateForFunctionPointer<Oodle_LogHeader>(address)();
        }

        if (NativeLibrary.TryGetExport(handle, nameof(Oodle_CheckVersion), out address)) {
            var version = 0u;
            var expected = oodleVer > 0 ? oodleVer : CreateOodleVersion(9, 0);
            var result = Marshal.GetDelegateForFunctionPointer<Oodle_CheckVersion>(address)(expected, ref version);
            Log.Information("Loaded Oodle Version {Version}", ParseOodleVersion(version));
            if (!result) {
                Log.Error("Invalid Oodle version! Expected a version compatible with {Expected} ({Version:X8}), got {Parsed} ({Result:X8})", ParseOodleVersion(expected), expected, ParseOodleVersion(version), version);
            }
        }

        return true;
    }

    public static unsafe int Decompress(Memory<byte> input, Memory<byte> output) {
        if (!IsReady) {
            if (!LoadOodleDll()) {
                throw new DllNotFoundException("Can't find Oodle library");
            }
        }

        if (!IsReady) {
            return 0;
        }

        using var inPin = input.Pin();
        using var outPin = output.Pin();
        return DecompressDelegate((byte*) inPin.Pointer, input.Length, (byte*) outPin.Pointer, output.Length);
    }

    public static unsafe OodleLZ_Compressor GetCompressor(Memory<byte> input) {
        if (!IsReady) {
            if (!LoadOodleDll()) {
                throw new DllNotFoundException("Can't find Oodle library");
            }
        }

        if (!IsReady || GetCompressorDelegate == null) {
            return OodleLZ_Compressor.Invalid;
        }

        using var inPin = input.Pin();
        var independent = false;
        return GetCompressorDelegate((byte*) inPin.Pointer, input.Length, ref independent);
    }

    public static string GetCompressorName(Memory<byte> input) {
        var compressor = GetCompressor(input);
        return GetCompressorName(compressor);
    }

    public static string GetCompressorName(OodleLZ_Compressor compressor) {
        if (!IsReady) {
            if (!LoadOodleDll()) {
                throw new DllNotFoundException("Can't find Oodle library");
            }
        }

        if (!IsReady || GetNameDelegate == null) {
            return "invalid";
        }

        return GetNameDelegate(compressor);
    }
}
