using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

internal static class Program
{
    private const string DllName = "slskdown_core.dll";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr compress_data_fast(IntPtr data, UIntPtr len, out UIntPtr out_len);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr decompress_data_fast(IntPtr data, UIntPtr len, out UIntPtr out_len);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void free_compressed_data(IntPtr ptr, UIntPtr len);

    private static void Main()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "..", "SlskDown", "bin", "Release", "net8.0-windows", "slskdown_core.dll"));

        if (!File.Exists(candidate))
        {
            Console.Error.WriteLine($"ERROR: DLL not found at: {candidate}");
            Environment.Exit(2);
            return;
        }

        // Ensure P/Invoke resolves to the exact DLL we want.
        if (!NativeLibrary.TryLoad(candidate, out var handle))
        {
            Console.Error.WriteLine($"ERROR: NativeLibrary.TryLoad failed for: {candidate}");
            Environment.Exit(3);
            return;
        }

        NativeLibrary.SetDllImportResolver(typeof(Program).Assembly, (_, _, _) => handle);

        var rng = new Random(12345);
        var input = new byte[256 * 1024];
        rng.NextBytes(input);

        IntPtr inputPtr = IntPtr.Zero;
        IntPtr compressedPtr = IntPtr.Zero;
        IntPtr decompressedPtr = IntPtr.Zero;
        UIntPtr compressedLen = UIntPtr.Zero;
        UIntPtr decompressedLen = UIntPtr.Zero;

        try
        {
            inputPtr = Marshal.AllocHGlobal(input.Length);
            Marshal.Copy(input, 0, inputPtr, input.Length);

            compressedPtr = compress_data_fast(inputPtr, (UIntPtr)input.Length, out compressedLen);
            if (compressedPtr == IntPtr.Zero || compressedLen == UIntPtr.Zero)
            {
                Console.Error.WriteLine("ERROR: compress_data_fast returned null/empty");
                Environment.Exit(4);
                return;
            }

            decompressedPtr = decompress_data_fast(compressedPtr, compressedLen, out decompressedLen);
            if (decompressedPtr == IntPtr.Zero || decompressedLen == UIntPtr.Zero)
            {
                Console.Error.WriteLine("ERROR: decompress_data_fast returned null/empty");
                Environment.Exit(5);
                return;
            }

            var output = new byte[(int)decompressedLen];
            Marshal.Copy(decompressedPtr, output, 0, output.Length);

            var ok = output.Length == input.Length && output.SequenceEqual(input);
            if (!ok)
            {
                Console.Error.WriteLine($"ERROR: roundtrip mismatch inputLen={input.Length} outputLen={output.Length}");
                Environment.Exit(6);
                return;
            }

            Console.WriteLine("OK: compress/decompress roundtrip passed");
            Console.WriteLine($"Input bytes: {input.Length}");
            Console.WriteLine($"Compressed bytes: {(ulong)compressedLen}");
            Console.WriteLine($"Decompressed bytes: {(ulong)decompressedLen}");
        }
        finally
        {
            if (decompressedPtr != IntPtr.Zero && decompressedLen != UIntPtr.Zero)
            {
                free_compressed_data(decompressedPtr, decompressedLen);
            }

            if (compressedPtr != IntPtr.Zero && compressedLen != UIntPtr.Zero)
            {
                free_compressed_data(compressedPtr, compressedLen);
            }

            if (inputPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(inputPtr);
            }
        }
    }
}
