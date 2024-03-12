// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;

namespace Lotus.Struct;

public static class Oodle {
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate int OodleLZ_Decompress(void* srcBuf, int srcSize, void* rawBuf, int rawSize,
                                                  int fuzzSafe = 1, int checkCRC = 0, OodleLZ_Verbosity verbosity = OodleLZ_Verbosity.None,
                                                  void* decBufBase = null, int decBufSize = 0, void* fpCallback = null, void* callbackUserData = null,
                                                  void* decoderMemory = null, int decoderMemorySize = 0,
                                                  OodleLZ_Decode_ThreadPhase threadPhase = OodleLZ_Decode_ThreadPhase.Unthreaded);

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
                    yield return "*oo2core*macarm64*.dylib"; // todo: this doesn't exist.
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

    public static void LoadOodleDll(string? path = null) {
        if (IsReady) {
            return;
        }

        path ??= Environment.CurrentDirectory;

        if (Directory.Exists(path) && new FileInfo(path).Attributes.HasFlag(FileAttributes.Directory)) {
            if (!TryFindOodleDll(path, out var oodlePath)) {
                return;
            }

            path = oodlePath;
        }

        if (string.IsNullOrEmpty(path) || !File.Exists(path)) {
            return;
        }

        if (!NativeLibrary.TryLoad(path, out var handle)) {
            return;
        }

        if (!NativeLibrary.TryGetExport(handle, nameof(OodleLZ_Decompress), out var address)) {
            return;
        }

        DecompressDelegate = Marshal.GetDelegateForFunctionPointer<OodleLZ_Decompress>(address);
    }

    public static unsafe void Decompress(Memory<byte> input, Memory<byte> output) {
        if (!IsReady) {
            LoadOodleDll();
        }

        if (!IsReady) {
            return;
        }

        using var inPin = input.Pin();
        using var outPin = output.Pin();
        DecompressDelegate(inPin.Pointer, input.Length, outPin.Pointer, output.Length);
    }
}
