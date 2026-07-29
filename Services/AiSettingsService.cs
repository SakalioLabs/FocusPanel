using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FocusPanel.Data;
using FocusPanel.Models;

namespace FocusPanel.Services;

public readonly record struct AiSettingsState(
    bool HasApiKey,
    string Model);

public interface IAiSettingsService
{
    Task<AiSettingsState> LoadStateAsync();
    Task<string?> LoadApiKeyAsync();
    Task<AiSettingsState> SaveAsync(
        string apiKey,
        string model);
    Task ClearApiKeyAsync();
}

internal interface IAiConfigStore
{
    string? Read(string key);
    void Write(string key, string value);
    void Delete(string key);
}

internal interface IApiKeyProtector
{
    string Protect(string value);
    string Unprotect(string value);
}

public sealed class AiSettingsService : IAiSettingsService
{
    internal const string ApiKeyConfigKey =
        "AI.OpenAI.ApiKeyProtected";
    internal const string ModelConfigKey =
        "AI.OpenAI.Model";
    public const string DefaultModel = "gpt-5.6-sol";

    private readonly IAiConfigStore _store;
    private readonly IApiKeyProtector _protector;
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    public AiSettingsService()
        : this(
            new AppConfigAiStore(),
            new WindowsDpapiProtector())
    {
    }

    internal AiSettingsService(
        IAiConfigStore store,
        IApiKeyProtector protector)
    {
        _store = store;
        _protector = protector;
    }

    public Task<AiSettingsState> LoadStateAsync() =>
        RunSerializedAsync(
            () =>
            {
                bool hasApiKey = !string.IsNullOrWhiteSpace(
                    _store.Read(ApiKeyConfigKey));
                string? value = _store.Read(ModelConfigKey);
                string model = string.IsNullOrWhiteSpace(value)
                    ? DefaultModel
                    : value;
                return new AiSettingsState(
                    hasApiKey,
                    model);
            });

    public Task<string?> LoadApiKeyAsync() =>
        RunSerializedAsync(
            () =>
            {
                string? encrypted =
                    _store.Read(ApiKeyConfigKey);
                if (string.IsNullOrWhiteSpace(encrypted))
                    return null;
                try
                {
                    return _protector.Unprotect(encrypted);
                }
                catch
                {
                    return null;
                }
            });

    public Task<AiSettingsState> SaveAsync(
        string apiKey,
        string model) =>
        RunSerializedAsync(
            () =>
            {
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    _store.Write(
                        ApiKeyConfigKey,
                        _protector.Protect(apiKey.Trim()));
                }

                string savedModel =
                    string.IsNullOrWhiteSpace(model)
                        ? DefaultModel
                        : model.Trim();
                _store.Write(ModelConfigKey, savedModel);
                bool hasApiKey = !string.IsNullOrWhiteSpace(
                    _store.Read(ApiKeyConfigKey));
                return new AiSettingsState(
                    hasApiKey,
                    savedModel);
            });

    public Task ClearApiKeyAsync() =>
        RunSerializedAsync(
            () =>
            {
                _store.Delete(ApiKeyConfigKey);
                return true;
            });

    private async Task<T> RunSerializedAsync<T>(
        Func<T> operation)
    {
        await _operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(operation).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }
}

internal sealed class AppConfigAiStore : IAiConfigStore
{
    public string? Read(string key)
    {
        using var context = new AppDbContext();
        context.EnsureSchema();
        return context.AppConfigs.Find(key)?.Value;
    }

    public void Write(string key, string value)
    {
        using var context = new AppDbContext();
        context.EnsureSchema();
        AppConfig? config = context.AppConfigs.Find(key);
        if (config == null)
        {
            context.AppConfigs.Add(
                new AppConfig { Key = key, Value = value });
        }
        else
        {
            config.Value = value;
        }
        context.SaveChanges();
    }

    public void Delete(string key)
    {
        using var context = new AppDbContext();
        context.EnsureSchema();
        AppConfig? config = context.AppConfigs.Find(key);
        if (config == null)
            return;
        context.AppConfigs.Remove(config);
        context.SaveChanges();
    }
}

internal sealed class WindowsDpapiProtector : IApiKeyProtector
{
    private const int CryptprotectUiForbidden = 0x1;

    public string Protect(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        return Convert.ToBase64String(
            Transform(bytes, protect: true));
    }

    public string Unprotect(string value)
    {
        byte[] bytes = Convert.FromBase64String(value);
        return Encoding.UTF8.GetString(
            Transform(bytes, protect: false));
    }

    private static byte[] Transform(
        byte[] input,
        bool protect)
    {
        var inputBlob = new DataBlob();
        var outputBlob = new DataBlob();
        try
        {
            inputBlob.Size = input.Length;
            inputBlob.Data =
                Marshal.AllocHGlobal(input.Length);
            Marshal.Copy(
                input,
                0,
                inputBlob.Data,
                input.Length);

            bool succeeded = protect
                ? CryptProtectData(
                    ref inputBlob,
                    null,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptprotectUiForbidden,
                    out outputBlob)
                : CryptUnprotectData(
                    ref inputBlob,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptprotectUiForbidden,
                    out outputBlob);
            if (!succeeded)
                throw new InvalidOperationException(
                    "Windows 无法保护 AI 凭据。");

            var result = new byte[outputBlob.Size];
            Marshal.Copy(
                outputBlob.Data,
                result,
                0,
                outputBlob.Size);
            return result;
        }
        finally
        {
            if (inputBlob.Data != IntPtr.Zero)
                Marshal.FreeHGlobal(inputBlob.Data);
            if (outputBlob.Data != IntPtr.Zero)
                LocalFree(outputBlob.Data);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Size;
        public IntPtr Data;
    }

    [DllImport(
        "crypt32.dll",
        SetLastError = true,
        CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn,
        string? description,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        out DataBlob dataOut);

    [DllImport(
        "crypt32.dll",
        SetLastError = true,
        CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        IntPtr description,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        out DataBlob dataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
