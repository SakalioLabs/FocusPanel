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
    string Model,
    string Provider = AiProvider.DeepSeek,
    bool SmartOrganizerEnabled = false);

public static class AiProvider
{
    public const string DeepSeek = "DeepSeek";
    public const string OpenAi = "OpenAI";
}

public interface IAiSettingsService
{
    Task<AiSettingsState> LoadStateAsync();
    Task<string?> LoadApiKeyAsync();
    Task<AiSettingsState> SaveAsync(
        string apiKey,
        string model,
        string provider,
        bool smartOrganizerEnabled);
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
    internal const string DeepSeekApiKeyConfigKey =
        "AI.DeepSeek.ApiKeyProtected";
    internal const string DeepSeekModelConfigKey =
        "AI.DeepSeek.Model";
    internal const string ProviderConfigKey = "AI.Provider";
    internal const string SmartOrganizerConfigKey =
        "AI.SmartOrganizer.Enabled";
    public const string DefaultModel = "deepseek-v4-flash";
    public const string DefaultOpenAiModel = "gpt-5.6-sol";

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
                string provider = ResolveProvider();
                bool hasApiKey = !string.IsNullOrWhiteSpace(
                    _store.Read(GetApiKeyConfigKey(provider)));
                string? value = _store.Read(
                    GetModelConfigKey(provider));
                string model = string.IsNullOrWhiteSpace(value)
                    ? GetDefaultModel(provider)
                    : value;
                return new AiSettingsState(
                    hasApiKey,
                    model,
                    provider,
                    string.Equals(
                        _store.Read(SmartOrganizerConfigKey),
                        bool.TrueString,
                        StringComparison.OrdinalIgnoreCase));
            });

    public Task<string?> LoadApiKeyAsync() =>
        RunSerializedAsync(
            () =>
            {
                string provider = ResolveProvider();
                string? encrypted = _store.Read(
                    GetApiKeyConfigKey(provider));
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
        string model,
        string provider,
        bool smartOrganizerEnabled) =>
        RunSerializedAsync(
            () =>
            {
                string savedProvider = NormalizeProvider(provider);
                string apiKeyConfigKey =
                    GetApiKeyConfigKey(savedProvider);
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    _store.Write(
                        apiKeyConfigKey,
                        _protector.Protect(apiKey.Trim()));
                }

                string savedModel =
                    string.IsNullOrWhiteSpace(model)
                        ? GetDefaultModel(savedProvider)
                        : model.Trim();
                _store.Write(ProviderConfigKey, savedProvider);
                _store.Write(
                    GetModelConfigKey(savedProvider),
                    savedModel);
                _store.Write(
                    SmartOrganizerConfigKey,
                    smartOrganizerEnabled.ToString());
                bool hasApiKey = !string.IsNullOrWhiteSpace(
                    _store.Read(apiKeyConfigKey));
                return new AiSettingsState(
                    hasApiKey,
                    savedModel,
                    savedProvider,
                    smartOrganizerEnabled);
            });

    public Task ClearApiKeyAsync() =>
        RunSerializedAsync(
            () =>
            {
                _store.Delete(
                    GetApiKeyConfigKey(ResolveProvider()));
                return true;
            });

    private string ResolveProvider()
    {
        string? configured = _store.Read(ProviderConfigKey);
        if (!string.IsNullOrWhiteSpace(configured))
            return NormalizeProvider(configured);

        // Existing installations used OpenAI only. Keep their credential
        // on OpenAI instead of ever presenting it to the new default provider.
        return !string.IsNullOrWhiteSpace(_store.Read(ApiKeyConfigKey))
            || !string.IsNullOrWhiteSpace(_store.Read(ModelConfigKey))
            ? AiProvider.OpenAi
            : AiProvider.DeepSeek;
    }

    internal static string NormalizeProvider(string? provider) =>
        string.Equals(
            provider,
            AiProvider.OpenAi,
            StringComparison.OrdinalIgnoreCase)
            ? AiProvider.OpenAi
            : AiProvider.DeepSeek;

    internal static string GetDefaultModel(string provider) =>
        NormalizeProvider(provider) == AiProvider.OpenAi
            ? DefaultOpenAiModel
            : DefaultModel;

    private static string GetApiKeyConfigKey(string provider) =>
        NormalizeProvider(provider) == AiProvider.OpenAi
            ? ApiKeyConfigKey
            : DeepSeekApiKeyConfigKey;

    private static string GetModelConfigKey(string provider) =>
        NormalizeProvider(provider) == AiProvider.OpenAi
            ? ModelConfigKey
            : DeepSeekModelConfigKey;

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
