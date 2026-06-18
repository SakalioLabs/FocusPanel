using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusPanel.Data;
using FocusPanel.Models;
using FocusPanel.Services;
using Microsoft.VisualBasic.FileIO;

namespace FocusPanel.ViewModels;

public partial class DesktopOverlayViewModel : ObservableObject
{
    private const double StartX = 16;
    private const double StartY = 16;
    private const string DesktopIconScaleKey = "DesktopOverlay_IconScale";
    private readonly FileOrganizerService _fileService;

    public ObservableCollection<DesktopFile> Files => _fileService.Files;

    [ObservableProperty]
    private bool areDesktopIconsVisible = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DesktopIconWidth))]
    [NotifyPropertyChangedFor(nameof(DesktopIconHeight))]
    [NotifyPropertyChangedFor(nameof(DesktopIconImageSize))]
    [NotifyPropertyChangedFor(nameof(DesktopIconTextFontSize))]
    [NotifyPropertyChangedFor(nameof(DesktopIconTopRowHeight))]
    [NotifyPropertyChangedFor(nameof(DesktopIconCellWidth))]
    [NotifyPropertyChangedFor(nameof(DesktopIconCellHeight))]
    private double desktopIconScale = 1.0;

    public double DesktopIconWidth => 98 * DesktopIconScale;
    public double DesktopIconHeight => 108 * DesktopIconScale;
    public double DesktopIconImageSize => 64 * DesktopIconScale;
    public double DesktopIconTextFontSize => Math.Max(11, 12 * DesktopIconScale);
    public double DesktopIconTopRowHeight => 68 * DesktopIconScale;
    public double DesktopIconCellWidth => 104 * DesktopIconScale;
    public double DesktopIconCellHeight => 112 * DesktopIconScale;

    public DesktopOverlayViewModel()
    {
        _fileService = new FileOrganizerService();
        _fileService.FilesChanged += () => OnPropertyChanged(nameof(Files));
        LoadDesktopIconScale();
        _ = _fileService.RefreshFiles();
    }

    [RelayCommand]
    private void ToggleDesktopIconVisibility()
    {
        AreDesktopIconsVisible = !AreDesktopIconsVisible;
    }

    [RelayCommand]
    private void SetDesktopIconScale(string scaleStr)
    {
        if (double.TryParse(scaleStr, out double scale))
        {
            DesktopIconScale = Math.Clamp(scale, 0.75, 1.45);
        }
    }

    partial void OnDesktopIconScaleChanged(double value)
    {
        SaveDesktopIconScale();
    }

    [RelayCommand]
    private void OpenFile(DesktopFile? file)
    {
        if (file == null || string.IsNullOrEmpty(file.FullPath)) return;
        StartShell(file.FullPath);
    }

    [RelayCommand]
    private void OpenWith(DesktopFile? file)
    {
        if (file == null || string.IsNullOrEmpty(file.FullPath)) return;

        try
        {
            var info = new System.Diagnostics.ProcessStartInfo(file.FullPath)
            {
                UseShellExecute = true,
                Verb = "openas"
            };
            System.Diagnostics.Process.Start(info);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OpenWith failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ShowInExplorer(DesktopFile? file)
    {
        if (file == null || string.IsNullOrEmpty(file.FullPath)) return;
        StartShell("explorer.exe", $"/select,\"{file.FullPath}\"");
    }

    [RelayCommand]
    private void ShowProperties(DesktopFile? file)
    {
        if (file == null || string.IsNullOrEmpty(file.FullPath)) return;

        try
        {
            var info = new System.Diagnostics.ProcessStartInfo(file.FullPath)
            {
                UseShellExecute = true,
                Verb = "properties"
            };
            System.Diagnostics.Process.Start(info);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ShowProperties failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenDesktopFolder()
    {
        StartShell(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
    }

    [RelayCommand]
    private void CopyFile(DesktopFile? file)
    {
        SetClipboardFiles(file, DragDropEffects.Copy);
    }

    [RelayCommand]
    private void CutFile(DesktopFile? file)
    {
        SetClipboardFiles(file, DragDropEffects.Move);
    }

    [RelayCommand]
    private async Task Paste()
    {
        if (!Clipboard.ContainsFileDropList()) return;

        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var files = Clipboard.GetFileDropList().Cast<string>().Where(File.Exists).ToList();
        var directories = Clipboard.GetFileDropList().Cast<string>().Where(Directory.Exists).ToList();
        bool move = Clipboard.GetData("Preferred DropEffect") is MemoryStream stream
            && stream.Length > 0
            && stream.ToArray()[0] == 2;

        await Task.Run(() =>
        {
            foreach (var path in files)
            {
                var target = UniquePath(Path.Combine(desktopPath, Path.GetFileName(path)));
                if (move) File.Move(path, target);
                else File.Copy(path, target);
            }

            foreach (var path in directories)
            {
                var target = UniquePath(Path.Combine(desktopPath, Path.GetFileName(path)));
                if (move) Directory.Move(path, target);
                else CopyDirectory(path, target);
            }
        });

        if (move) Clipboard.Clear();
        Refresh();
    }

    [RelayCommand]
    private async Task RenameFile(DesktopFile? file)
    {
        if (file == null || string.IsNullOrEmpty(file.FullPath)) return;

        string newName = Microsoft.VisualBasic.Interaction.InputBox("输入新名称", "重命名", file.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName == file.Name) return;

        string target = Path.Combine(Path.GetDirectoryName(file.FullPath) ?? "", newName);
        if (File.Exists(target) || Directory.Exists(target))
        {
            MessageBox.Show("同名文件已经存在。", "重命名", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await Task.Run(() =>
        {
            if (File.Exists(file.FullPath)) File.Move(file.FullPath, target);
            else if (Directory.Exists(file.FullPath)) Directory.Move(file.FullPath, target);

            using var context = new AppDbContext();
            context.EnsureSchema();
            var pref = context.DesktopFilePreferences.FirstOrDefault(p => p.FilePath == file.Name);
            if (pref != null)
            {
                pref.FilePath = newName;
                context.SaveChanges();
            }
        });

        Refresh();
    }

    [RelayCommand]
    private async Task DeleteFile(DesktopFile? file)
    {
        if (file == null || string.IsNullOrEmpty(file.FullPath)) return;

        var result = MessageBox.Show($"确定要将 \"{file.Name}\" 移到回收站吗？", "删除",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        await Task.Run(() =>
        {
            if (File.Exists(file.FullPath))
            {
                FileSystem.DeleteFile(file.FullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }
            else if (Directory.Exists(file.FullPath))
            {
                FileSystem.DeleteDirectory(file.FullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }
        });

        Refresh();
    }

    [RelayCommand]
    private async Task RestoreOrMoveToDesktop(DesktopDropRequest? request)
    {
        if (request?.File == null) return;

        double x = Math.Max(0, request.X - DesktopIconWidth / 2);
        double y = Math.Max(0, request.Y - DesktopIconHeight / 2);

        request.File.DesktopX = x;
        request.File.DesktopY = y;

        if (request.File.IsHidden)
        {
            await _fileService.RestoreFileToDesktop(request.File.Name, x, y);
        }
        else
        {
            await _fileService.SaveDesktopPosition(request.File.Name, x, y);
        }

        Refresh();
    }

    [RelayCommand]
    private async Task SortByName()
    {
        await ArrangeFiles(Files.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase));
    }

    [RelayCommand]
    private async Task SortByType()
    {
        await ArrangeFiles(Files.OrderBy(f => f.FileType).ThenBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase));
    }

    [RelayCommand]
    private async Task SortByDate()
    {
        await ArrangeFiles(Files.OrderByDescending(f => f.CreatedAt).ThenBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase));
    }

    [RelayCommand]
    private async Task SortBySize()
    {
        await ArrangeFiles(Files.OrderByDescending(f => f.Size).ThenBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase));
    }

    [RelayCommand]
    public void Refresh()
    {
        _ = _fileService.RefreshFiles();
    }

    private async Task ArrangeFiles(IEnumerable<DesktopFile> orderedFiles)
    {
        int index = 0;
        foreach (var file in orderedFiles.ToList())
        {
            double x = StartX + (index / 7) * DesktopIconCellWidth;
            double y = StartY + (index % 7) * DesktopIconCellHeight;
            file.DesktopX = x;
            file.DesktopY = y;
            await _fileService.SaveDesktopPosition(file.Name, x, y);
            index++;
        }
    }

    private void LoadDesktopIconScale()
    {
        try
        {
            using var context = new AppDbContext();
            context.EnsureSchema();
            var config = context.AppConfigs.Find(DesktopIconScaleKey);
            if (config != null && double.TryParse(config.Value, out double scale))
            {
                DesktopIconScale = Math.Clamp(scale, 0.75, 1.45);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Load desktop icon scale failed: {ex.Message}");
        }
    }

    private void SaveDesktopIconScale()
    {
        try
        {
            using var context = new AppDbContext();
            context.EnsureSchema();
            var value = DesktopIconScale.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var config = context.AppConfigs.Find(DesktopIconScaleKey);
            if (config == null)
            {
                context.AppConfigs.Add(new AppConfig { Key = DesktopIconScaleKey, Value = value });
            }
            else
            {
                config.Value = value;
            }

            context.SaveChanges();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Save desktop icon scale failed: {ex.Message}");
        }
    }

    private static void SetClipboardFiles(DesktopFile? file, DragDropEffects effect)
    {
        if (file == null || string.IsNullOrEmpty(file.FullPath)) return;

        var files = new StringCollection { file.FullPath };
        var data = new DataObject();
        data.SetFileDropList(files);
        data.SetData("Preferred DropEffect", new MemoryStream(new[] { (byte)effect, (byte)0, (byte)0, (byte)0 }));
        Clipboard.SetDataObject(data, true);
    }

    private static void StartShell(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Shell start failed: {ex.Message}");
        }
    }

    private static void StartShell(string fileName, string arguments)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Shell start failed: {ex.Message}");
        }
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return path;

        string directory = Path.GetDirectoryName(path) ?? "";
        string name = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        int count = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(directory, $"{name} ({count++}){extension}");
        } while (File.Exists(candidate) || Directory.Exists(candidate));

        return candidate;
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(destinationDir, Path.GetFileName(file)));
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            CopyDirectory(dir, Path.Combine(destinationDir, Path.GetFileName(dir)));
        }
    }
}

public sealed class DesktopDropRequest
{
    public DesktopFile? File { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
}
