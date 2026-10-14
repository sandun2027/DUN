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
    public static string EditTitle => T("Edit text", "编辑文字");
    public static string Save => T("Save", "保存");
    public static string SaveAndCopy => T("Save and copy", "保存并复制");
    public static string Cancel => T("Cancel", "取消");

    // Settings
    public static string SettingsTitle => T("Settings", "设置");
    public static string SettingsHeading => T("Settings", "设置");
    public static string SettingsSubtitle => T("Make DUN work your way. Everything stays on this computer.", "让 DUN 按你的习惯工作。所有内容都只保存在这台电脑上。");
    public static string SectionShortcut => T("Shortcut", "快捷键");
    public static string OpenClipboard => T("Open DUN", "打开 DUN");
    public static string OpenClipboardNote => T("Search and pick with the keyboard", "用键盘搜索、挑选");
    public static string PressShortcut => T("Press keys…", "请按下快捷键…");
    public static string WinVTitle => T("Use Win+V", "用 Win+V 打开");
    public static string WinVNote => T("Replaces Windows clipboard history", "替代系统自带的剪贴板历史");
    public static string SectionHabits => T("Habits", "使用习惯");
    public static string ClickTitle => T("Click a card", "单击卡片");
    public static string ClickNote => T("Hold Shift for the other one", "按住 Shift 执行另一种");
    public static string ClickCopyShort => T("Copy", "仅复制");
    public static string ClickPasteShort => T("Paste", "直接粘贴");
    public static string EdgeTitle => T("Top-edge reveal", "顶边呼出");
    public static string EdgeNote => T("Show when the pointer rests at the top", "鼠标停在屏幕顶边时显示");
    public static string SectionHistory => T("History", "历史记录");
    public static string MaxItems => T("Keep at most", "最多保留");
    public static string MaxItemsNote => T("Pinned cards don't count", "固定的卡片不计入");
    public static string Retention => T("Delete after", "自动删除");
    public static string Never => T("Never", "永不");
    public static string Days => T("{0} d", "{0} 天");
    public static string SectionPrivacy => T("Privacy", "隐私");
    public static string Excluded => T("Ignore these apps", "忽略这些程序");
    public static string ExcludedNote => T("Copies from them are never recorded. One program name per line.", "从这些程序复制的内容不会被记录，每行一个程序名。");
    public static string StartWithWindows => T("Start with Windows", "开机启动");
    public static string StartupNote => T("Run after you sign in", "登录 Windows 后自动运行");
}
