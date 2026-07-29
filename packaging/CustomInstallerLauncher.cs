using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using FocusPanel.Services;
using Microsoft.Win32;

internal static class CustomInstallerLauncher
{
    private const string MsiResourceName = "FocusPanelMsi";

    [STAThread]
    private static int Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        string existingDirectory =
            FindExistingInstallDirectory();
        using (var dialog = new InstallLocationDialog(
            GetDefaultInstallDirectory(),
            existingDirectory))
        {
            if (dialog.ShowDialog() != DialogResult.OK)
                return 0;

            if (!string.IsNullOrEmpty(
                    existingDirectory)
                && !SamePath(
                    existingDirectory,
                    dialog.InstallDirectory))
            {
                DialogResult relocate =
                    MessageBox.Show(
                        "检测到 FocusPanel 当前安装在：\r\n"
                        + existingDirectory
                        + "\r\n\r\n要改到：\r\n"
                        + dialog.InstallDirectory
                        + "\r\n\r\nWindows 将先卸载旧程序文件，再安装到新目录。"
                        + "任务、收纳记录和设置位于用户 AppData，不会被删除。是否继续？",
                        "迁移 FocusPanel 安装位置",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                if (relocate != DialogResult.Yes)
                    return 0;

                int uninstallResult =
                    UninstallExisting(
                        existingDirectory);
                if (uninstallResult != 0)
                    return uninstallResult;

                if (!WaitForUninstallCompletion(
                        existingDirectory,
                        TimeSpan.FromSeconds(90)))
                {
                    MessageBox.Show(
                        "旧版卸载程序已经返回，但 Windows 仍未释放原安装目录。"
                        + "\r\n\r\n为避免新版本又写回 C 盘，本次迁移已停止。"
                        + "请在“已安装的应用”确认旧版已移除后，再重新运行此安装包。",
                        "迁移尚未完成",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return 1;
                }
            }

            return RunInstaller(
                dialog.InstallDirectory);
        }
    }

    private static int RunInstaller(
        string installDirectory)
    {
        string tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "FocusPanelInstaller",
            Guid.NewGuid().ToString("N"));
        string msiPath = Path.Combine(
            tempDirectory,
            "FocusPanel-win.msi");
        string logPath = Path.Combine(
            Path.GetTempPath(),
            "FocusPanel-install.log");

        try
        {
            Directory.CreateDirectory(tempDirectory);
            using (Stream input = Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream(MsiResourceName))
            using (var output = File.Create(msiPath))
            {
                if (input == null)
                    throw new InvalidOperationException("安装程序资源缺失。");
                input.CopyTo(output);
            }

            using (Process process = Process.Start(
                new ProcessStartInfo
                {
                    FileName = "msiexec.exe",
                    Arguments =
                        "/i " + Quote(msiPath)
                        + " VELOPACK_INSTALLDIR="
                        + Quote(installDirectory)
                        + " INSTALLFOLDER="
                        + Quote(installDirectory)
                        + " /L*V " + Quote(logPath),
                    UseShellExecute = true,
                    WorkingDirectory = tempDirectory
                }))
            {
                if (process == null)
                    throw new InvalidOperationException("无法启动 FocusPanel 安装程序。");
                process.WaitForExit();
                if (process.ExitCode == 1602)
                    return 0;
                if (process.ExitCode == 0
                    || process.ExitCode == 3010)
                {
                    string actualDirectory =
                        WaitForInstalledDirectory(
                            TimeSpan.FromSeconds(30));
                    if (!SamePath(
                            actualDirectory,
                            installDirectory))
                    {
                        throw new InvalidOperationException(
                            "Windows Installer 没有使用所选目录。"
                            + "\r\n所选目录："
                            + installDirectory
                            + "\r\n实际目录："
                            + (string.IsNullOrWhiteSpace(
                                    actualDirectory)
                                ? "未检测到安装记录"
                                : actualDirectory)
                            + "\r\n\r\n安装日志："
                            + logPath);
                    }
                    return 0;
                }

                throw new InvalidOperationException(
                    "Windows Installer 返回错误 "
                    + process.ExitCode
                    + "。详细日志："
                    + logPath);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "无法安装 FocusPanel：\r\n" + ex.Message,
                "FocusPanel 安装",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                    Directory.Delete(tempDirectory, true);
            }
            catch
            {
                // The installer may still be releasing an antivirus-scanned file.
            }
        }
    }

    private static string Quote(string value)
    {
        if (value.IndexOf('"') >= 0)
            throw new ArgumentException("安装路径不能包含双引号。");
        return "\"" + value + "\"";
    }

    private static string GetDefaultInstallDirectory()
    {
        var candidates =
            new System.Collections.Generic
                .List<InstallerDriveCandidate>();
        foreach (DriveInfo drive
                 in DriveInfo.GetDrives())
        {
            try
            {
                candidates.Add(
                    new InstallerDriveCandidate(
                        drive.RootDirectory.FullName,
                        drive.DriveType,
                        drive.IsReady,
                        drive.IsReady
                            ? drive.AvailableFreeSpace
                            : 0));
            }
            catch
            {
                // Ignore a drive that disappeared during enumeration.
            }
        }

        return InstallerLocationPolicy
            .SelectDefaultDirectory(
                candidates,
                Path.GetPathRoot(
                    Environment.SystemDirectory),
                Environment.GetFolderPath(
                    Environment.SpecialFolder
                        .LocalApplicationData));
    }

    private static string FindExistingInstallDirectory()
    {
        string[] subKeys =
        {
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\FocusPanel",
            @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\FocusPanel"
        };
        RegistryKey[] roots =
        {
            Registry.CurrentUser,
            Registry.LocalMachine
        };

        foreach (RegistryKey root in roots)
        {
            foreach (string subKey in subKeys)
            {
                try
                {
                    using (RegistryKey key =
                           root.OpenSubKey(subKey))
                    {
                        string location =
                            key == null
                                ? string.Empty
                                : key.GetValue(
                                    "InstallLocation",
                                    string.Empty)
                                    as string;
                        if (!string.IsNullOrWhiteSpace(
                                location))
                        {
                            return Path.GetFullPath(
                                location);
                        }
                    }
                }
                catch
                {
                    // A damaged or inaccessible uninstall record is ignored.
                }
            }
        }

        return string.Empty;
    }

    private static bool WaitForUninstallCompletion(
        string previousDirectory,
        TimeSpan timeout)
    {
        DateTime deadline =
            DateTime.UtcNow.Add(timeout);
        do
        {
            string registeredDirectory =
                FindExistingInstallDirectory();
            bool registrationReleased =
                string.IsNullOrWhiteSpace(
                    registeredDirectory)
                || !SamePath(
                    registeredDirectory,
                    previousDirectory);
            bool updaterReleased =
                !File.Exists(
                    Path.Combine(
                        previousDirectory,
                        "Update.exe"));
            if (registrationReleased
                && updaterReleased)
            {
                return true;
            }

            Thread.Sleep(250);
        }
        while (DateTime.UtcNow < deadline);

        return false;
    }

    private static string WaitForInstalledDirectory(
        TimeSpan timeout)
    {
        DateTime deadline =
            DateTime.UtcNow.Add(timeout);
        string directory;
        do
        {
            directory =
                FindExistingInstallDirectory();
            if (!string.IsNullOrWhiteSpace(
                    directory))
            {
                return directory;
            }

            Thread.Sleep(250);
        }
        while (DateTime.UtcNow < deadline);

        return string.Empty;
    }

    private static int UninstallExisting(
        string existingDirectory)
    {
        string updater = Path.Combine(
            existingDirectory,
            "Update.exe");
        if (!File.Exists(updater))
        {
            MessageBox.Show(
                "找不到旧安装的卸载程序：\r\n"
                + updater
                + "\r\n请先在 Windows“已安装的应用”中卸载旧版，再重新运行安装包。",
                "无法迁移安装位置",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return 1;
        }

        try
        {
            using (Process process = Process.Start(
                new ProcessStartInfo
                {
                    FileName = updater,
                    Arguments = "--uninstall",
                    UseShellExecute = true,
                    WorkingDirectory =
                        existingDirectory
                }))
            {
                if (process == null)
                    throw new InvalidOperationException("无法启动旧版卸载程序。");
                process.WaitForExit();
                if (process.ExitCode == 0)
                    return 0;

                MessageBox.Show(
                    "旧版卸载未完成（错误 "
                    + process.ExitCode
                    + "）。新版本尚未写入，现有数据保持不变。",
                    "迁移已停止",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return process.ExitCode;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "无法卸载旧版 FocusPanel：\r\n"
                + ex.Message,
                "迁移已停止",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return 1;
        }
    }

    private static bool SamePath(
        string first,
        string second)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(first)
                    .TrimEnd('\\', '/'),
                Path.GetFullPath(second)
                    .TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private sealed class InstallLocationDialog : Form
    {
        private readonly TextBox _pathBox;

        internal InstallLocationDialog(
            string defaultDirectory,
            string existingDirectory)
        {
            Text = "安装 FocusPanel";
            Font = new Font("Microsoft YaHei UI", 9F);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 188);

            var title = new Label
            {
                Text = "选择 FocusPanel 安装位置",
                Font = new Font(Font.FontFamily, 14F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(24, 22)
            };
            var hint = new Label
            {
                Text = string.IsNullOrWhiteSpace(
                        existingDirectory)
                    ? "可直接输入或浏览到 D/E 盘；安装结束后会校验真实落盘目录。"
                    : "当前版本可迁移到新目录；业务数据和设置不会被删除。",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Location = new Point(26, 58)
            };
            _pathBox = new TextBox
            {
                Location = new Point(28, 88),
                Size = new Size(424, 25),
                Text = defaultDirectory
            };
            var browse = new Button
            {
                Text = "浏览…",
                Location = new Point(462, 86),
                Size = new Size(72, 29)
            };
            browse.Click += Browse_Click;

            var cancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Location = new Point(368, 139),
                Size = new Size(78, 32)
            };
            var install = new Button
            {
                Text = "安装",
                Location = new Point(456, 139),
                Size = new Size(78, 32)
            };
            install.Click += Install_Click;

            Controls.Add(title);
            Controls.Add(hint);
            Controls.Add(_pathBox);
            Controls.Add(browse);
            Controls.Add(cancel);
            Controls.Add(install);
            AcceptButton = install;
            CancelButton = cancel;
        }

        internal string InstallDirectory { get; private set; }

        private void Browse_Click(object sender, EventArgs e)
        {
            using (var picker = new FolderBrowserDialog())
            {
                picker.Description = "选择 FocusPanel 安装目录";
                picker.SelectedPath = _pathBox.Text;
                picker.ShowNewFolderButton = true;
                if (picker.ShowDialog(this) == DialogResult.OK)
                    _pathBox.Text = picker.SelectedPath;
            }
        }

        private void Install_Click(object sender, EventArgs e)
        {
            try
            {
                string path = Path.GetFullPath(_pathBox.Text.Trim());
                if (string.IsNullOrEmpty(Path.GetPathRoot(path)))
                    throw new ArgumentException("请选择有效的绝对路径。");

                InstallDirectory = path;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "安装目录无效",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }
}
