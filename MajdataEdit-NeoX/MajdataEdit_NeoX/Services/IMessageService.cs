using System.Threading.Tasks;

namespace MajdataEdit_NeoX.Services;

/// <summary>
/// 给用户看一条"必须知道"的提示（例如选中的目录不是谱面目录）。
///
/// 与 <see cref="IFileDialogService"/> 一样经组合根注入，让 VM 不直接依赖窗口/平台。
/// </summary>
public interface IMessageService
{
    /// <summary>显示提示，等用户确认后返回。</summary>
    /// <param name="title">提示标题。</param>
    /// <param name="message">提示正文。</param>
    Task ShowAsync(string title, string message);
}
