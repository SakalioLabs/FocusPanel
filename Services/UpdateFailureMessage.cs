using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace FocusPanel.Services;

public static class UpdateFailureMessage
{
    public static string Describe(Exception exception)
    {
        Exception root = Unwrap(exception);
        string details = root.Message;

        if ((root is HttpRequestException request
                && request.StatusCode == HttpStatusCode.Forbidden)
            || Contains(details, "403")
            || Contains(details, "rate limit"))
        {
            return "GitHub 暂时限制了自动请求。请稍后重试，"
                + "或点击“打开官方下载页”手动安装最新版。";
        }

        if (root is TaskCanceledException
            || Contains(details, "timed out")
            || Contains(details, "timeout"))
        {
            return "连接更新服务器超时。请检查网络后重试，"
                + "或打开官方下载页手动安装。";
        }

        if (root is UnauthorizedAccessException)
        {
            return "更新文件没有足够的写入权限。请退出正在运行的旧版本后重试，"
                + "或使用安装程序覆盖升级。";
        }

        if (root is IOException)
        {
            return "更新文件正在被占用或磁盘空间不足。请退出旧版本并检查磁盘后重试。";
        }

        return "更新服务暂时不可用。请稍后重试，"
            + "或点击“打开官方下载页”手动安装最新版。";
    }

    private static Exception Unwrap(Exception exception)
    {
        while (exception.InnerException != null)
            exception = exception.InnerException;
        return exception;
    }

    private static bool Contains(string value, string fragment)
        => value.Contains(
            fragment,
            StringComparison.OrdinalIgnoreCase);
}
