# 米物键盘听写助手

为米物精英键盘 USB 接收器（VID:PID `3044:507c`）制作的非官方 macOS 菜单栏工具。需要 macOS 13 或以上，构建同时支持 Apple 芯片和 Intel Mac。

## 在另一台 Mac 上安装

本仓库提供源码。打开「终端」，如果还没装命令行开发工具，先运行 `xcode-select --install` 并完成安装。然后运行：

```sh
git clone https://github.com/cjwn/miiw-dictation.git
cd miiw-dictation
./build-menu-app.sh
open dist
```

1. 解压 `MIIW Voice.zip`，将 `MIIW Voice.app` 移到「应用程序」再打开。插入原装 USB 接收器，把键盘切到 USB 接收器模式。
2. 点击菜单栏麦克风图标，打开「首次设置」。在「系统设置 → 隐私与安全性 → 辅助功能」允许该应用发送快捷键。如果刚授权仍无响应，从菜单退出并重新打开应用。
3. 在「系统设置 → 键盘 → 听写」打开听写。快捷键选「自定义」，按一下米物语音键录入 F18。若选用米物麦克风模式，按住约一秒再松开。
4. 菜单选「Mac 麦克风」时，在系统听写中选 Mac 内置麦克风，短按米物语音键启动听写。菜单选「米物麦克风」时，在系统听写中选 `MIIIW Elite Receiver`，在文本框中按住语音键，等提示后说话，松手结束。
5. 可在菜单里选「登录时启动」。已安装早期命令行测试版的用户，应先停止旧程序并把其 plist 移出 LaunchAgents，避免重复触发。

本应用处理键盘事件、发送快捷键并控制米物麦克风启停。音频采集与语音转文字由 macOS 听写负责，是否在本机处理取决于 Apple 的系统设置及语言支持。应用自身不保存或上传录音。

## Windows 版（已在真机实测可用）

`MIIWVoiceWin/` 是同一套思路的 Windows 实现：**按住语音键说话，松开约 0.5 秒后文字自动打出**。
单文件 C#，双击 `build.bat` 用系统自带的 `csc.exe` 编译，不装 VS、不装 SDK、不联网编译、无需驱动和任何权限授权。

识别默认走小米 MiMo 的 `mimo-v2.5-asr` 接口（需自备 `MIMO_BASE_URL` / `MIMO_API_KEY`）。
这里**没有**沿用 `Win+H`，因为 Windows 10 的 `Win+H`（听写）对中文基本不可用：面板能弹出但
一个字都不出，把清晰语音外放给系统 OneCore 引擎直接识别也返回空结果，多台机器复现。
想用系统听写可以 `--win-h` 切回。详见 [`MIIWVoiceWin/README.md`](MIIWVoiceWin/README.md)。

> 注意：应用内识别会把录音上传到所配置的接口；`--win-h` 模式则不录音。

蓝牙 BLE 模式为什么不能替代接收器，见 [`docs/ble-notes.md`](docs/ble-notes.md)。诊断工具在 `tools/`。

项目与米物、搜狗及 Apple 无隶属或背书关系。名称仅用于说明兼容设备和系统功能。

## 签名与更新

默认构建使用临时签名，适合本机验证。重编译后 macOS 可能要求重新授权。也可以将本机的 Apple Development 签名身份传给 `./build-menu-app.sh "<签名身份>"`。更新时应沿用同一 Bundle ID 和签名身份。

当前没有公开的公证安装包。分发预编译版本时，应使用 Developer ID Application 签名并完成 Apple 公证。

## 许可证

MIT，见 [LICENSE](LICENSE)。可以自由使用、修改、再分发（含商用），保留版权声明即可。

项目与米物、搜狗及 Apple 无隶属或背书关系；文中出现的产品名称仅用于说明兼容设备，
相关商标归各自权利人所有。
