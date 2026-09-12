using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace CommandPaletteLLM;

internal interface IDataProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedText);
}

internal sealed class DpapiDataProtector : IDataProtector
{
    private const int CryptprotectUiForbidden = 0x1;
    private static readonly byte[] OptionalEntropy =
        Encoding.UTF8.GetBytes("CommandPaletteLLM.ProviderApiKey.v1");

    public string Protect(string plaintext) => Convert.ToBase64String(
        Transform(Encoding.UTF8.GetBytes(plaintext), protect: true));

    public string Unprotect(string protectedText) => Encoding.UTF8.GetString(
        Transform(Convert.FromBase64String(protectedText), protect: false));

    private static byte[] Transform(byte[] input, bool protect)
    {
        var inputBlob = CreateBlob(input);
        var entropyBlob = CreateBlob(OptionalEntropy);
        var outputBlob = new DataBlob();
        try
        {
            var succeeded = protect
                ? CryptProtectData(
                    ref inputBlob,
                    null,
                    ref entropyBlob,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptprotectUiForbidden,
                    out outputBlob)
                : CryptUnprotectData(
                    ref inputBlob,
                    IntPtr.Zero,
                    ref entropyBlob,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptprotectUiForbidden,
                    out outputBlob);
            if (!succeeded)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var output = new byte[outputBlob.Length];
            Marshal.Copy(outputBlob.Data, output, 0, output.Length);
            return output;
        }
        finally
        {
            if (inputBlob.Data != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(inputBlob.Data);
            }

            if (entropyBlob.Data != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(entropyBlob.Data);
            }

            if (outputBlob.Data != IntPtr.Zero)
            {
                _ = LocalFree(outputBlob.Data);
            }
        }
    }

    private static DataBlob CreateBlob(byte[] bytes)
    {
        var data = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, data, bytes.Length);
        return new DataBlob { Length = bytes.Length, Data = data };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Length;
        public IntPtr Data;
    }

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn,
        string? description,
        ref DataBlob optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        out DataBlob dataOut);

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        IntPtr description,
        ref DataBlob optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        out DataBlob dataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}

internal sealed class StoredLlmProviderSettings
{
    public string Id { get; set; } = "default";

    public string Name { get; set; } = "Local LLM";

    public string BaseUrl { get; set; } = "http://127.0.0.1:8080/v1";

    public string Model { get; set; } = "local-model";

    public string? ApiKey { get; set; }

    public string? ProtectedApiKey { get; set; }

    public string? ConsentedRemoteOrigin { get; set; }
}

internal static class ProviderDataConsent
{
    public static bool RequiresConsent(string baseUrl) =>
        TryGetRemoteOrigin(baseUrl, out _);

    public static bool HasConsent(LlmProviderSettings provider) =>
        !TryGetRemoteOrigin(provider.BaseUrl, out var origin) ||
        string.Equals(
            origin,
            provider.ConsentedRemoteOrigin,
            StringComparison.OrdinalIgnoreCase);

    public static string GetConsentOrigin(string baseUrl) =>
        TryGetRemoteOrigin(baseUrl, out var origin) ? origin : string.Empty;

    private static bool TryGetRemoteOrigin(string baseUrl, out string origin)
    {
        origin = string.Empty;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.IsLoopback ||
            string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        origin = uri.GetComponents(
            UriComponents.SchemeAndServer,
            UriFormat.UriEscaped).TrimEnd('/');
        return true;
    }
}
