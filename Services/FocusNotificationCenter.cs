using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FocusPanel.Services;

internal sealed record FocusNotificationSnapshot(
    string Key,
    string Title,
    string Message,
    string Glyph,
    FocusToastKind Kind,
    string? ActionLabel,
    DateTimeOffset CreatedAt,
    bool IsUnread,
    FocusNotificationActionKind ActionKind =
        FocusNotificationActionKind.None);

internal sealed record FocusNotificationLoadResult(
    IReadOnlyList<FocusNotificationSnapshot> Items,
    string? Warning = null);

internal interface IFocusNotificationStore
{
    FocusNotificationLoadResult Load();

    void Save(IReadOnlyList<FocusNotificationSnapshot> items);
}

internal sealed class TransientFocusNotificationStore
    : IFocusNotificationStore
{
    public FocusNotificationLoadResult Load() =>
        new(Array.Empty<FocusNotificationSnapshot>());

    public void Save(
        IReadOnlyList<FocusNotificationSnapshot> items)
    {
    }
}

internal sealed class JsonFocusNotificationStore
    : IFocusNotificationStore
{
    internal const long MaximumFileBytes = 1024 * 1024;
    private const string FileName = "panel-notifications.json";

    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

    private readonly string _filePath;

    internal JsonFocusNotificationStore()
        : this(GetDefaultFilePath())
    {
    }

    internal JsonFocusNotificationStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException(
                "Notification history path is required.",
                nameof(filePath));
        }

        _filePath = Path.GetFullPath(filePath);
    }

    public FocusNotificationLoadResult Load()
    {
        if (!File.Exists(_filePath))
        {
            return new FocusNotificationLoadResult(
                Array.Empty<FocusNotificationSnapshot>());
        }

        try
        {
            var file = new FileInfo(_filePath);
            if (file.Length > MaximumFileBytes)
            {
                throw new InvalidDataException(
                    "通知历史文件超过 1 MB 安全上限。");
            }

            string json = File.ReadAllText(
                _filePath,
                Encoding.UTF8);
            FocusNotificationSnapshot[]? items =
                JsonSerializer.Deserialize<
                    FocusNotificationSnapshot[]>(
                    json,
                    JsonOptions);
            if (items == null)
            {
                throw new InvalidDataException(
                    "通知历史文件没有有效内容。");
            }

            return new FocusNotificationLoadResult(items);
        }
        catch (Exception ex)
        {
            string? archivedPath =
                TryArchiveCorruptedFile();
            string suffix = archivedPath == null
                ? "原文件无法归档，Panel 已忽略它。"
                : $"原文件已归档为 {Path.GetFileName(archivedPath)}。";
            return new FocusNotificationLoadResult(
                Array.Empty<FocusNotificationSnapshot>(),
                $"通知历史损坏或无法读取：{ex.Message} {suffix}");
        }
    }

    public void Save(
        IReadOnlyList<FocusNotificationSnapshot> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        string? directory = Path.GetDirectoryName(_filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException(
                "通知历史目录无效。");
        }

        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(
            directory,
            $".panel-notifications-{Guid.NewGuid():N}.tmp");
        try
        {
            string json = JsonSerializer.Serialize(
                items,
                JsonOptions);
            File.WriteAllText(
                temporaryPath,
                json,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));
            File.Move(
                temporaryPath,
                _filePath,
                overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "清理通知历史临时文件失败："
                    + ex.Message);
            }
        }
    }

    internal static string GetDefaultFilePath() =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "FocusPanel",
            FileName);

    private string? TryArchiveCorruptedFile()
    {
        try
        {
            if (!File.Exists(_filePath))
                return null;

            string? directory =
                Path.GetDirectoryName(_filePath);
            if (string.IsNullOrWhiteSpace(directory))
                return null;

            string archivedPath = Path.Combine(
                directory,
                $"panel-notifications.corrupt-"
                + $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-"
                + $"{Guid.NewGuid():N}.json");
            File.Move(_filePath, archivedPath);
            return archivedPath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                "归档损坏通知历史失败："
                + ex.Message);
            return null;
        }
    }
}

public sealed partial class FocusNotificationItem : ObservableObject
{
    internal FocusNotificationItem(
        FocusToastNotification notification,
        DateTimeOffset createdAt,
        Action? resolvedAction)
        : this(
            notification.Key,
            notification.Title,
            notification.Message,
            notification.Glyph,
            notification.Kind,
            notification.ActionLabel,
            notification.Action ?? resolvedAction,
            createdAt,
            isUnread: true,
            notification.ActionKind)
    {
    }

    internal FocusNotificationItem(
        FocusNotificationSnapshot snapshot,
        Action? resolvedAction)
        : this(
            snapshot.Key,
            snapshot.Title,
            snapshot.Message,
            snapshot.Glyph,
            snapshot.Kind,
            snapshot.ActionLabel,
            resolvedAction,
            snapshot.CreatedAt,
            snapshot.IsUnread,
            snapshot.ActionKind)
    {
    }

    private FocusNotificationItem(
        string key,
        string title,
        string message,
        string glyph,
        FocusToastKind kind,
        string? actionLabel,
        Action? action,
        DateTimeOffset createdAt,
        bool isUnread,
        FocusNotificationActionKind actionKind)
    {
        Key = key;
        Title = title;
        Message = message;
        Glyph = glyph;
        Kind = kind;
        ActionLabel = actionLabel;
        Action = action;
        CreatedAt = createdAt;
        ActionKind = actionKind;
        this.isUnread = isUnread;
    }

    public string Key { get; }

    public string Title { get; }

    public string Message { get; }

    public string Glyph { get; }

    public FocusToastKind Kind { get; }

    public string? ActionLabel { get; }

    public DateTimeOffset CreatedAt { get; }

    public string TimeText =>
        CreatedAt.LocalDateTime.ToString("MM-dd HH:mm");

    public bool HasAction =>
        Action != null
        && !string.IsNullOrWhiteSpace(ActionLabel);

    public bool IsExpiredAction =>
        Action == null
        && !string.IsNullOrWhiteSpace(ActionLabel);

    public FocusNotificationActionKind ActionKind { get; }

    internal Action? Action { get; }

    [ObservableProperty]
    private bool isUnread = true;

    internal FocusNotificationSnapshot ToSnapshot() =>
        new(
            Key,
            Title,
            Message,
            Glyph,
            Kind,
            ActionLabel,
            CreatedAt,
            IsUnread,
            ActionKind);
}

public sealed class FocusNotificationCenter
{
    public const int MaximumItems = 50;
    private const int MaximumTextLength = 4096;

    private readonly object _persistenceSync = new();
    private readonly ObservableCollection<FocusNotificationItem>
        _items = new();
    private readonly ReadOnlyObservableCollection<FocusNotificationItem>
        _readOnlyItems;
    private readonly IFocusNotificationStore _store;
    private readonly Func<
        FocusNotificationActionKind,
        Action?> _actionResolver;
    private IReadOnlyList<FocusNotificationSnapshot>?
        _pendingSave;
    private Task _saveProcessor = Task.CompletedTask;
    private bool _isSaving;
    private bool _isAcceptingSaves = true;

    public FocusNotificationCenter()
        : this(
            new JsonFocusNotificationStore(),
            actionResolver: null)
    {
    }

    public FocusNotificationCenter(
        Func<FocusNotificationActionKind, Action?>
            actionResolver)
        : this(
            new JsonFocusNotificationStore(),
            actionResolver)
    {
    }

    internal FocusNotificationCenter(
        IFocusNotificationStore store,
        Func<FocusNotificationActionKind, Action?>?
            actionResolver = null)
    {
        _store = store
            ?? throw new ArgumentNullException(nameof(store));
        _actionResolver =
            actionResolver
            ?? (_ => null);
        _readOnlyItems =
            new ReadOnlyObservableCollection<FocusNotificationItem>(
                _items);

        FocusNotificationLoadResult loaded;
        try
        {
            loaded = _store.Load();
        }
        catch (Exception ex)
        {
            loaded = new FocusNotificationLoadResult(
                Array.Empty<FocusNotificationSnapshot>(),
                $"通知历史无法读取：{ex.Message}");
        }

        LastPersistenceError = loaded.Warning;
        Restore(loaded.Items);
    }

    public ReadOnlyObservableCollection<FocusNotificationItem> Items =>
        _readOnlyItems;

    public int UnreadCount { get; private set; }

    public string? LastPersistenceError { get; private set; }

    public event EventHandler? Changed;

    public event EventHandler? PersistenceStatusChanged;

    public void Add(FocusToastNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        for (int index = 0; index < _items.Count; index++)
        {
            if (!string.Equals(
                    _items[index].Key,
                    notification.Key,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (_items[index].IsUnread)
                UnreadCount--;
            _items.RemoveAt(index);
            break;
        }

        _items.Insert(
            0,
            new FocusNotificationItem(
                notification,
                DateTimeOffset.Now,
                ResolveAction(notification.ActionKind)));
        UnreadCount++;
        TrimToCapacity();
        OnChangedAndQueueSave();
    }

    public void MarkAllRead()
    {
        if (UnreadCount == 0)
            return;

        foreach (FocusNotificationItem item in _items)
            item.IsUnread = false;

        UnreadCount = 0;
        OnChangedAndQueueSave();
    }

    public void Invoke(FocusNotificationItem? item)
    {
        if (item == null || !_items.Contains(item))
            return;

        if (item.IsUnread)
        {
            item.IsUnread = false;
            UnreadCount--;
            OnChangedAndQueueSave();
        }

        item.Action?.Invoke();
    }

    public void Clear()
    {
        if (_items.Count == 0)
            return;

        _items.Clear();
        UnreadCount = 0;
        OnChangedAndQueueSave();
    }

    public Task CompleteAsync()
    {
        lock (_persistenceSync)
        {
            _isAcceptingSaves = false;
            return _saveProcessor;
        }
    }

    public Task FlushAsync()
    {
        lock (_persistenceSync)
            return _saveProcessor;
    }

    private void Restore(
        IReadOnlyList<FocusNotificationSnapshot> snapshots)
    {
        var identities = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (FocusNotificationSnapshot snapshot in snapshots)
        {
            FocusNotificationSnapshot? normalized =
                Normalize(snapshot);
            if (normalized == null
                || !identities.Add(normalized.Key))
            {
                continue;
            }

            var item = new FocusNotificationItem(
                normalized,
                ResolveAction(normalized.ActionKind));
            _items.Add(item);
            if (item.IsUnread)
                UnreadCount++;
            if (_items.Count == MaximumItems)
                break;
        }
    }

    private void TrimToCapacity()
    {
        while (_items.Count > MaximumItems)
        {
            FocusNotificationItem removed = _items[^1];
            if (removed.IsUnread)
                UnreadCount--;
            _items.RemoveAt(_items.Count - 1);
        }
    }

    private void OnChangedAndQueueSave()
    {
        Changed?.Invoke(this, EventArgs.Empty);
        QueueSave();
    }

    private void QueueSave()
    {
        FocusNotificationSnapshot[] snapshot =
            _items.Select(item => item.ToSnapshot()).ToArray();
        lock (_persistenceSync)
        {
            if (!_isAcceptingSaves)
                return;

            _pendingSave = snapshot;
            if (_isSaving)
                return;

            _isSaving = true;
            _saveProcessor = ProcessSavesAsync();
        }
    }

    private async Task ProcessSavesAsync()
    {
        while (true)
        {
            IReadOnlyList<FocusNotificationSnapshot>? snapshot;
            lock (_persistenceSync)
            {
                snapshot = _pendingSave;
                _pendingSave = null;
                if (snapshot == null)
                {
                    _isSaving = false;
                    return;
                }
            }

            try
            {
                await Task.Run(() => _store.Save(snapshot))
                    .ConfigureAwait(false);
                if (LastPersistenceError != null)
                {
                    LastPersistenceError = null;
                    NotifyPersistenceStatusChanged();
                }
            }
            catch (Exception ex)
            {
                LastPersistenceError =
                    $"通知历史保存失败：{ex.Message}";
                NotifyPersistenceStatusChanged();
            }
        }
    }

    private void NotifyPersistenceStatusChanged()
    {
        EventHandler? handlers = PersistenceStatusChanged;
        if (handlers == null)
            return;

        foreach (Delegate handler in handlers.GetInvocationList())
        {
            try
            {
                ((EventHandler)handler)(this, EventArgs.Empty);
            }
            catch
            {
                // Persistence must continue after a detached observer.
            }
        }
    }

    private static FocusNotificationSnapshot? Normalize(
        FocusNotificationSnapshot snapshot)
    {
        string key = Truncate(snapshot.Key);
        string title = Truncate(snapshot.Title);
        if (string.IsNullOrWhiteSpace(key)
            || string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        FocusToastKind kind =
            Enum.IsDefined(typeof(FocusToastKind), snapshot.Kind)
                ? snapshot.Kind
                : FocusToastKind.Information;
        FocusNotificationActionKind actionKind =
            Enum.IsDefined(
                typeof(FocusNotificationActionKind),
                snapshot.ActionKind)
                ? snapshot.ActionKind
                : FocusNotificationActionKind.None;
        DateTimeOffset createdAt = snapshot.CreatedAt == default
            ? DateTimeOffset.Now
            : snapshot.CreatedAt;
        return snapshot with
        {
            Key = key,
            Title = title,
            Message = Truncate(snapshot.Message),
            Glyph = Truncate(snapshot.Glyph),
            Kind = kind,
            ActionKind = actionKind,
            ActionLabel = string.IsNullOrWhiteSpace(snapshot.ActionLabel)
                ? null
                : Truncate(snapshot.ActionLabel),
            CreatedAt = createdAt
        };
    }

    private Action? ResolveAction(
        FocusNotificationActionKind actionKind)
    {
        if (actionKind == FocusNotificationActionKind.None)
            return null;

        try
        {
            return _actionResolver(actionKind);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                "恢复通知动作失败："
                + ex.Message);
            return null;
        }
    }

    private static string Truncate(string? value)
    {
        value ??= string.Empty;
        return value.Length <= MaximumTextLength
            ? value
            : value[..MaximumTextLength];
    }
}
