using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

internal static class CustomInstallerLauncher
{
    private const string SetupResourceName = "FocusPanelSetup";

    [STAThread]
    private static int Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using (var dialog = new InstallLocationDialog())
        {
            if (dialog.ShowDialog() != DialogResult.OK)
                return 0;

            return RunSetup(dialog.InstallDirectory);
        }
    }

    private static int RunSetup(string installDirectory)
    {
        string tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "FocusPanelInstaller",
            Guid.NewGuid().ToString("N"));
        string setupPath = Path.Combine(
            tempDirectory,
            "FocusPanel-win-Setup.exe");

        try
        {
            Directory.CreateDirectory(tempDirectory);
            using (Stream input = Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream(SetupResourceName))
            using (var output = File.Create(setupPath))
            {
                if (input == null)
                    throw new InvalidOperationException("安装程序资源缺失。");
                input.CopyTo(output);
            }

            using (Process process = Process.Start(
                new ProcessStartInfo
                {
                    FileName = setupPath,
                    Arguments = "--installto " + Quote(installDirectory),
                    UseShellExecute = true,
                    WorkingDirectory = tempDirectory
                }))
            {
                if (process == null)
                    throw new InvalidOperationException("无法启动 FocusPanel 安装程序。");
                process.WaitForExit();
                return process.ExitCode;
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
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private sealed class InstallLocationDialog : Form
    {
        private readonly TextBox _pathBox;

        internal InstallLocationDialog()
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
                Text = "后续一键更新会继续使用此目录，不会跳回默认位置。",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Location = new Point(26, 58)
            };
            _pathBox = new TextBox
            {
                Location = new Point(28, 88),
                Size = new Size(424, 25),
                Text = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "FocusPanel")
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
