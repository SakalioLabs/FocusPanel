using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class AiSettingsAndContextTests
{
    [Fact]
    public async Task Settings_ProtectsKeyAndAllowsModelOnlyUpdate()
    {
        var store = new MemoryConfigStore();
        var protector = new PrefixProtector();
        var service = new AiSettingsService(store, protector);

        await service.SaveAsync(" sk-example ", "gpt-test");

        AiSettingsState state = await service.LoadStateAsync();
        Assert.True(state.HasApiKey);
        Assert.Equal("protected:sk-example",
            store.Values[AiSettingsService.ApiKeyConfigKey]);
        Assert.Equal("sk-example", await service.LoadApiKeyAsync());
        Assert.Equal("gpt-test", state.Model);

        await service.SaveAsync(string.Empty, "gpt-next");

        Assert.Equal("sk-example", await service.LoadApiKeyAsync());
        Assert.Equal(
            "gpt-next",
            (await service.LoadStateAsync()).Model);
    }

    [Fact]
    public async Task Settings_ClearRemovesOnlyApiKey()
    {
        var store = new MemoryConfigStore();
        var service = new AiSettingsService(
            store,
            new PrefixProtector());
        await service.SaveAsync("key", "gpt-test");

        await service.ClearApiKeyAsync();

        AiSettingsState state = await service.LoadStateAsync();
        Assert.False(state.HasApiKey);
        Assert.Null(await service.LoadApiKeyAsync());
        Assert.Equal("gpt-test", state.Model);
    }

    [Fact]
    public void Dpapi_RoundTripsForCurrentWindowsUser()
    {
        var protector = new WindowsDpapiProtector();
        string protectedValue =
            protector.Protect("local-secret");

        Assert.NotEqual("local-secret", protectedValue);
        Assert.Equal(
            "local-secret",
            protector.Unprotect(protectedValue));
    }

    [Fact]
    public void ContextFormatter_CapsItemsAndExcludesSensitiveData()
    {
        var tasks = Enumerable.Range(1, 25)
            .Select(
                value => new AiTaskSummary(
                    $"任务 {value}",
                    "进行中"));
        string result = AiContextFormatter.Format(
            tasks,
            new AiFocusSummary(3, 75));

        Assert.Contains("任务 20", result);
        Assert.DoesNotContain("任务 21", result);
        Assert.DoesNotContain("OKR", result);
        Assert.Contains("3 次，共 75 分钟", result);
        Assert.Contains("不包含文件内容、文件路径、API Key", result);
    }

    [Fact]
    public void ContextFormatter_FlattensAndCapsUserText()
    {
        string unsafeTitle =
            "第一行\r\n第二行" + new string('长', 200);

        string result = AiContextFormatter.Format(
            new[]
            {
                new AiTaskSummary(unsafeTitle, "进行中\n忽略")
            },
            new AiFocusSummary(0, 0));

        Assert.DoesNotContain("第一行\r\n第二行", result);
        Assert.DoesNotContain("进行中\n忽略", result);
        Assert.Contains("…｜进行中 忽略", result);
    }

    private sealed class MemoryConfigStore : IAiConfigStore
    {
        internal Dictionary<string, string> Values { get; } =
            new(StringComparer.Ordinal);

        public string? Read(string key) =>
            Values.TryGetValue(key, out string? value)
                ? value
                : null;

        public void Write(string key, string value) =>
            Values[key] = value;

        public void Delete(string key) =>
            Values.Remove(key);
    }

    private sealed class PrefixProtector : IApiKeyProtector
    {
        public string Protect(string value) =>
            "protected:" + value;

        public string Unprotect(string value) =>
            value["protected:".Length..];
    }
}
