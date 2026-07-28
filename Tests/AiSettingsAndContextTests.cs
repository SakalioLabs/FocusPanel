using System;
using System.Collections.Generic;
using System.Linq;
using FocusPanel.Services;
using Xunit;

namespace FocusPanel.Tests;

public sealed class AiSettingsAndContextTests
{
    [Fact]
    public void Settings_ProtectsKeyAndAllowsModelOnlyUpdate()
    {
        var store = new MemoryConfigStore();
        var protector = new PrefixProtector();
        var service = new AiSettingsService(store, protector);

        service.Save(" sk-example ", "gpt-test");

        Assert.True(service.HasApiKey);
        Assert.Equal("protected:sk-example",
            store.Values[AiSettingsService.ApiKeyConfigKey]);
        Assert.Equal("sk-example", service.LoadApiKey());
        Assert.Equal("gpt-test", service.Model);

        service.Save(string.Empty, "gpt-next");

        Assert.Equal("sk-example", service.LoadApiKey());
        Assert.Equal("gpt-next", service.Model);
    }

    [Fact]
    public void Settings_ClearRemovesOnlyApiKey()
    {
        var store = new MemoryConfigStore();
        var service = new AiSettingsService(
            store,
            new PrefixProtector());
        service.Save("key", "gpt-test");

        service.ClearApiKey();

        Assert.False(service.HasApiKey);
        Assert.Null(service.LoadApiKey());
        Assert.Equal("gpt-test", service.Model);
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
        var objectives = Enumerable.Range(1, 12)
            .Select(
                value => new AiOkrSummary(
                    $"目标 {value}",
                    value * 10));

        string result = AiContextFormatter.Format(
            tasks,
            objectives,
            new AiFocusSummary(3, 75));

        Assert.Contains("任务 20", result);
        Assert.DoesNotContain("任务 21", result);
        Assert.Contains("目标 10", result);
        Assert.DoesNotContain("目标 11", result);
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
            Array.Empty<AiOkrSummary>(),
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
