# 米物语音键 for Windows

米物精英键盘语音键 → Windows 自带「语音输入」（`Win+H`）的极简替代品。

替代搜狗智能助手那一整套：不装驱动、不装输入法、不联网上传，只有一个几百 KB 的单文件小程序。

## 前提

1. 键盘切到 **2.4G 接收器模式**（USB 接收器插在电脑上）。
   - 语音键的私有 HID 报告（`B0 01/03/00`）只存在于接收器的厂商通道（Usage Page `0xFF00` / Usage `1`）；
     蓝牙 BLE 模式下键盘只有标准 HOGP，没有这个通道，也没有音频端点。
2. Windows 10 1809+ / Windows 11。
   - Windows 11：`Win+H` = 语音输入。
   - Windows 10：`Win+H` = 听写，需要先在「设置 → 隐私 → 语音」里打开**在线语音识别**。

## 构建

把 `MIIWVoiceWin` 文件夹拷到 Windows 上，**双击 `build.bat`**，会在同目录生成 `MIIW Voice.exe`。

用的是 Windows 自带的 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`，不需要 Visual Studio、
不需要 .NET SDK、不需要联网。

## 使用

1. 双击 `MIIW Voice.exe`，托盘出现麦克风图标（没有主窗口）。
2. 把光标放进任意可输入文字的地方。
3. **按一下语音键开始说话，再按一下结束**。程序做的是：按下 → 发一次 `Win+H`；松开 → 再发一次 `Win+H`。
4. 右键托盘图标：
   - 「登录时自动运行」写入 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
   - 「打开日志」看 HID 报告和注入记录
   - 「退出」

日志位置：`%LOCALAPPDATA%\MIIWVoice\voice.log`

## 麦克风

默认用**系统默认输入设备**（Windows 语音输入读的就是这个）。两种用法：

- **用电脑自己的麦克风**：什么都不用改，直接按键。代价是语音键只当快捷键用。
- **用键盘自带的麦克风**：把 `MIIIW Elite Receiver` 设为默认输入设备，然后启动时加 `--keyboard-mic`
  （右键 exe → 新建快捷方式 → 目标后面加参数）。按下时程序会给接收器发 `B2 01` 打开麦克风，
  松手发 `B2 00` 关闭，和 macOS 版的行为一致。

## 命令行参数

| 参数 | 作用 |
| --- | --- |
| `--keyboard-mic` | 按键时开关键盘自带麦克风（`B2 01` / `B2 00`） |
| `--no-toggle` | 松手发 `Esc` 而不是第二次 `Win+H`（个別系统上语音条不被第二次 Win+H 关掉时用） |
| `--slow` | 把 Win 组合键拆成几步发送，间隔 25 ms（`Win+H` 不生效时先试这个） |
| `--dump` | 把接收器所有原始 HID 报告写进日志，便于排查固件差异 |
| `--no-input` | 只记录日志、不注入按键，用来确认语音键能被读到 |
| `--autostart` | 首次运行时直接写入开机启动项 |

## 已知限制

- **前台是管理员权限窗口时按键注入会被 UIPI 拦掉**（Windows 的规则）。在普通窗口里用没问题。
- `Win+H` 需要系统开启语音输入；如果第一次按毫无反应，先手动按一次 `Win+H` 看系统面板是否出现，
  再决定是系统设置问题还是注入问题（日志里能看到「注入 Win+H」）。
- 松手停止依赖系统的语音面板行为；如果 `Win+H` 两次不能关闭，用 `--no-toggle`。
- 只匹配 VID `0x3044` / PID `0x507c` 的厂商通道，别的键盘不适用。

## 和 macOS 版的对应关系

| macOS（`../VoiceKey.c`） | Windows（本目录） |
| --- | --- |
| `IOHIDManager` 匹配 VID/PID/`0xFF00`/`1` | `SetupAPI` + `HidD_GetAttributes` + `HidP_GetCaps` |
| `B0 01` → `CGEvent` 发 F18 | `B0 01` → `SendInput` 发 `Win+H` |
| `B0 00` → 再发一次 F18 | `B0 00` → 再发一次 `Win+H`（或 `Esc`） |
| `--keyboard-mic` 发 `B2 01/00` | `--keyboard-mic` 发 `B2 01/00` |
| 菜单栏 `NSStatusItem` | 托盘 `NotifyIcon` |
| 需要「辅助功能」授权 | 无需任何授权 |

## 卸载搜狗

本程序与搜狗无关。确认能用之后，可以放心卸载搜狗输入法的智能助手组件；键盘本身是标准 HID
键盘 + USB Audio，不依赖任何厂商驱动。
