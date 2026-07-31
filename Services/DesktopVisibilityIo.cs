using System;
using System.IO;
using System.Threading.Tasks;

namespace FocusPanel.Services;

internal interface IDesktopVisibilityIo
{
    Task<FileAttributes> ReadAttributesAsync(
        string path);

    Task ApplyAttributesAsync(
        string path,
        FileAttributes attributes,
        bool requiresElevation);

    Task<IDisposable> BeginElevatedBatchAsync();
}

internal interface IDesktopVisibilityElevatedBatch
    : IDisposable
{
    void SetAttributes(
        string path,
        FileAttributes attributes);
}

internal sealed class DesktopVisibilityIo
    : IDesktopVisibilityIo
{
    private readonly IDesktopItemVisibilityService
        _visibility;
    private readonly Action<
        string,
        FileAttributes> _setElevatedAttributes;
    private readonly Func<
        IDesktopVisibilityElevatedBatch>
        _startElevatedBatch;
    private readonly object _elevatedBatchGate = new();
    private IDesktopVisibilityElevatedBatch?
        _elevatedBatch;

    internal DesktopVisibilityIo(
        IDesktopItemVisibilityService visibility)
        : this(
            visibility,
            DesktopVisibilityElevatedHelper
                .SetAttributes,
            DesktopVisibilityElevatedHelper
                .StartBatch)
    {
    }

    internal DesktopVisibilityIo(
        IDesktopItemVisibilityService visibility,
        Action<
            string,
            FileAttributes> setElevatedAttributes,
        Func<IDesktopVisibilityElevatedBatch>?
            startElevatedBatch = null)
    {
        _visibility =
            visibility
            ?? throw new ArgumentNullException(
                nameof(visibility));
        _setElevatedAttributes =
            setElevatedAttributes
            ?? throw new ArgumentNullException(
                nameof(setElevatedAttributes));
        _startElevatedBatch =
            startElevatedBatch
            ?? DesktopVisibilityElevatedHelper
                .StartBatch;
    }

    public Task<FileAttributes>
        ReadAttributesAsync(
            string path)
    {
        string requiredPath =
            RequirePath(path);
        return Task.Run(
            () =>
            {
                if (!_visibility.Exists(
                        requiredPath))
                {
                    throw new FileNotFoundException(
                        "找不到要收纳的桌面项目。",
                        requiredPath);
                }

                return _visibility.GetAttributes(
                    requiredPath);
            });
    }

    public Task ApplyAttributesAsync(
        string path,
        FileAttributes attributes,
        bool requiresElevation)
    {
        string requiredPath =
            RequirePath(path);
        return Task.Run(
            () =>
            {
                if (requiresElevation)
                {
                    IDesktopVisibilityElevatedBatch?
                        batch;
                    lock (_elevatedBatchGate)
                        batch = _elevatedBatch;
                    if (batch != null)
                    {
                        batch.SetAttributes(
                            requiredPath,
                            attributes);
                    }
                    else
                    {
                        _setElevatedAttributes(
                            requiredPath,
                            attributes);
                    }
                    return;
                }

                _visibility.SetAttributes(
                    requiredPath,
                    attributes);
                _visibility.NotifyAttributesChanged(
                    requiredPath);
            });
    }

    public Task<IDisposable>
        BeginElevatedBatchAsync() =>
        Task.Run<IDisposable>(
            () =>
            {
                IDesktopVisibilityElevatedBatch batch =
                    _startElevatedBatch();
                lock (_elevatedBatchGate)
                {
                    if (_elevatedBatch != null)
                    {
                        batch.Dispose();
                        throw new InvalidOperationException(
                            "管理员收纳会话已在运行。");
                    }

                    _elevatedBatch = batch;
                }

                return new ElevatedBatchLease(
                    this,
                    batch);
            });

    private void EndElevatedBatch(
        IDesktopVisibilityElevatedBatch batch)
    {
        lock (_elevatedBatchGate)
        {
            if (ReferenceEquals(
                    _elevatedBatch,
                    batch))
            {
                _elevatedBatch = null;
            }
        }

        batch.Dispose();
    }

    private sealed class ElevatedBatchLease
        : IDisposable
    {
        private DesktopVisibilityIo? _owner;
        private IDesktopVisibilityElevatedBatch?
            _batch;

        internal ElevatedBatchLease(
            DesktopVisibilityIo owner,
            IDesktopVisibilityElevatedBatch batch)
        {
            _owner = owner;
            _batch = batch;
        }

        public void Dispose()
        {
            DesktopVisibilityIo? owner =
                System.Threading.Interlocked
                    .Exchange(ref _owner, null);
            IDesktopVisibilityElevatedBatch? batch =
                System.Threading.Interlocked
                    .Exchange(ref _batch, null);
            if (owner != null && batch != null)
                owner.EndElevatedBatch(batch);
        }
    }

    private static string RequirePath(
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "Desktop item path is required.",
                nameof(path));
        }

        return path;
    }
}
