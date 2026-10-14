using System.Globalization;

namespace DUN;

// UI strings. English and Simplified Chinese, chosen from the Windows display language.
static class S
{
    static readonly bool Zh = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh";
    static string T(string en, string zh) => Zh ? zh : en;

    public const string AppName = "DUN";

    // Tray
    public static string ToggleLine => T("Show / hide clipboard", "显示 / 隐藏剪贴板");
    public static string Pause => T("Pause recording", "暂停记录");
    public static string PausedUntil => T("Paused until {0:HH:mm}", "已暂停至 {0:HH:mm}");
    public static string PausedForever => T("Paused", "已暂停");
    public static string Pause10 => T("For 10 minutes", "10 分钟");
    public static string Pause60 => T("For 1 hour", "1 小时");
    public static string PauseManual => T("Until I resume", "直到手动恢复");
    public static string Resume => T("Resume recording", "恢复记录");
    public static string SettingsMenu => T("Settings…", "设置…");
    public static string OpenFolder => T("Open data folder", "打开数据文件夹");
    public static string ClearLine => T("Clear history (keep pinned)…", "清空历史（保留固定项）…");
    public static string ClearConfirm => T("Delete {0} unpinned items?", "删除 {0} 条未固定的记录？");
    public static string Quit => T("Quit", "退出");
    public static string Welcome => T("Copy something, then rest the pointer at the top edge of the screen or press {0}.",
                                      "复制点什么，然后把鼠标停在屏幕顶边，或按 {0}。");
    public static string HotkeyFailed => T("The shortcut {0} is already used by another app.", "快捷键 {0} 已被其他程序占用。");

    // Line
    public static string Hint => T("Things you copy will hang here", "复制的内容会挂在这里");
    public static string NoMatches => T("Nothing matches", "没有匹配的内容");
    public static string Copied => T("Copied", "已复制");
    public static string Chars => T("{0} chars", "{0} 字");
    public static string Lines => T("{0} lines", "{0} 行");
    public static string MoreFiles => T("+{0} more", "另 {0} 项");
    public static string SearchPlaceholder => T("Type to search", "输入以搜索");
    public static string FilterAll => T("All", "全部");
    public static string FilterText => T("Text", "文字");
    public static string FilterImages => T("Images", "图片");
    public static string FilterLinks => T("Links", "链接");
    public static string FilterFiles => T("Files", "文件");
    public static string FilterPinned => T("Pinned", "已固定");
    public static string KeysHelpCopy => T("Enter copy · Shift+Enter paste · Ctrl+P pin · Del delete · Esc close",
                                           "Enter 复制 · Shift+Enter 粘贴 · Ctrl+P 固定 · Del 删除 · Esc 关闭");
    public static string KeysHelpPaste => T("Enter paste · Shift+Enter copy · Ctrl+P pin · Del delete · Esc close",
                                            "Enter 粘贴 · Shift+Enter 复制 · Ctrl+P 固定 · Del 删除 · Esc 关闭");

    // Item menu
    public static string Copy => T("Copy", "复制");
    public static string Paste => T("Paste", "粘贴");
    public static string PastePlain => T("Paste as plain text", "粘贴为纯文本");
    public static string Pin => T("Pin", "固定");
    public static string Unpin => T("Unpin", "取消固定");
    public static string Edit => T("Edit…", "编辑…");
    public static string EditImage => T("Edit in Paint", "用画图编辑");
    public static string Open => T("Open", "打开");
    public static string OpenLink => T("Open in browser", "在浏览器中打开");
    public static string QuickLook => T("Quick look", "快速查看");
    public static string ShowInFolder => T("Show in folder", "在文件夹中显示");
    public static string SaveAs => T("Save as…", "另存为…");
    public static string Remove => T("Delete", "删除");

    // Dialogs
    public static string ShortcutTitle => T("Shortcut", "快捷键");
    public static string ShortcutPrompt => T("Press the new shortcut. Esc cancels.", "请按下新的快捷键，Esc 取消。");
    public static string EditTitle => T("Edit text", "编辑文字");
    public static string Save => T("Save", "保存");
    public static string SaveAndCopy => T("Save and copy", "保存并复制");
    public static string Cancel => T("Cancel", "取消");
    public static string OK => T("OK", "确定");

    // Settings
    public static string SettingsTitle => T("DUN settings", "DUN 设置");
    public static string SetShortcut => T("Shortcut", "快捷键");
    public static string ReplaceWinV => T("Also open with Win+V (replaces Windows clipboard history)", "也用 Win+V 打开（替代系统剪贴板历史）");
    public static string ClickAction => T("Click / Enter", "单击 / 回车");
    public static string ClickCopy => T("Copy only (hold Shift to paste)", "仅复制（按住 Shift 则粘贴）");
    public static string ClickPaste => T("Paste into the active window (hold Shift to copy only)", "粘贴到当前窗口（按住 Shift 则仅复制）");
    public static string Reveal => T("Mouse", "鼠标");
    public static string EdgeReveal => T("Show when the pointer rests at the top edge", "鼠标停在屏幕顶边时显示");
    public static string MaxItems => T("Keep at most", "最多保留");
    public static string ItemsSuffix => T("items (pinned don't count)", "条（固定项不计）");
    public static string Retention => T("Delete after", "自动删除");
    public static string Never => T("Never", "永不");
    public static string Days => T("{0} days", "{0} 天");
    public static string Excluded => T("Ignore apps", "忽略的程序");
    public static string ExcludedNote => T("One program name per line, e.g. KeePass", "每行一个程序名，例如 KeePass");
    public static string Startup => T("Startup", "启动");
    public static string StartWithWindows => T("Start with Windows", "开机启动");
}
