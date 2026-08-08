using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FocusPanel.Services;

public sealed partial class FocusNotificationItem : ObservableObject
{
    internal FocusNotificationItem(
        FocusToastNotification notification,
        DateTimeOffset createdAt)
    {
        Key = notification.Key;
        Title = notification.Title;
        Message = notification.Message;
        Glyph = notification.Glyph;
        Kind = notification.Kind;
        ActionLabel = notification.ActionLabel;
        Action = notification.Action;
        CreatedAt = createdAt;
    }

    public string Key { get; }

    public string Title { get; }

    public string Message { get; }

    public string Glyph { get; }

    public FocusToastKind Kind { get; }

    public string? ActionLabel { get; }

    public DateTimeOffset CreatedAt { get; }

    public string TimeText =>
        CreatedAt.LocalDateTime.ToString("HH:mm");

    public bool HasAction =>
        Action != null
        && !string.IsNullOrWhiteSpace(ActionLabel);

    internal Action? Action { get; }

    [ObservableProperty]
    private bool isUnread = true;
}

public sealed class FocusNotificationCenter
{
    public const int MaximumItems = 50;

    private readonly ObservableCollection<FocusNotificationItem>
        _items = new();
    private readonly ReadOnlyObservableCollection<FocusNotificationItem>
        _readOnlyItems;

    public FocusNotificationCenter()
    {
        _readOnlyItems =
            new ReadOnlyObservableCollection<FocusNotificationItem>(
                _items);
    }

    public ReadOnlyObservableCollection<FocusNotificationItem> Items =>
        _readOnlyItems;

    public int UnreadCount { get; private set; }

    public event EventHandler? Changed;

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
                DateTimeOffset.Now));
        UnreadCount++;

        while (_items.Count > MaximumItems)
        {
            FocusNotificationItem removed =
                _items[^1];
            if (removed.IsUnread)
                UnreadCount--;
            _items.RemoveAt(_items.Count - 1);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MarkAllRead()
    {
        if (UnreadCount == 0)
            return;

        foreach (FocusNotificationItem item in _items)
            item.IsUnread = false;

        UnreadCount = 0;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Invoke(FocusNotificationItem? item)
    {
        if (item == null || !_items.Contains(item))
            return;

        if (item.IsUnread)
        {
            item.IsUnread = false;
            UnreadCount--;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        item.Action?.Invoke();
    }

    public void Clear()
    {
        if (_items.Count == 0)
            return;

        _items.Clear();
        UnreadCount = 0;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
