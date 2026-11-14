# 探索笔记：蓝牙 BLE 模式能不能替代接收器

结论：**不能**。语音键和键盘麦克风只在 USB 接收器模式下存在。下面是判断依据，供以后复查。

## 1. 结论摘要

| 问题 | 答案 |
| --- | --- |
| BLE 模式下有音频通道吗？ | 没有。设备只声明 `Services: 0x400000 < BLE >`，无 HFP / A2DP / LE Audio |
| BLE 模式有厂商自定义 HID 语音键通道吗？ | 没有。HID 描述符里只有标准键盘和 Consumer Control |
| 已连接状态下能枚举 GATT 私有服务吗？ | 不能。`bluetoothd` 独占 ATT 连接，CoreBluetooth 看不到它 |
| 那用 BLE 能实现语音键吗？ | 只能在键盘把语音键映射成某个标准 Consumer usage 时才有机会，需实测确认 |

## 2. 证据

### 2.1 蓝牙侧只有 BLE，没有音频 profile

`system_profiler SPBluetoothDataType`（macOS 15.7.7，键盘走蓝牙通道）：

```
Connected:
    MIIIW KB Elite:
        Address: XX:XX:XX:XX:XX:XX   # 随机蓝牙地址，已隐去
        Vendor ID: 0x3044
        Product ID: 0x507C
        Minor Type: Keyboard
        Services: 0x400000 < BLE >
```

`system_profiler SPAudioDataType` 里唯一的米物输入设备仍是 USB 的 `MIIIW Elite Receiver`
（16 kHz 单声道）。蓝牙链路上没有任何音频端点。

### 2.2 BLE 链路上实际生效的 HID 报告描述符

从 `ioreg`（`IOHIDInterface` 的 `ReportDescriptor`）取到，只有两段：

- Report 1：标准 boot 键盘（修饰键 + 预留 + 6 键位）
- Report 2：Consumer Control（16 位 usage 数组）

没有 `Usage Page 0xFF00 / Usage 0x01` 的厂商 collection，也没有 `B0` 通道。也就是说接收器上
那套 `B0 01 / B0 03 / B0 00`（按下 / 按住 / 松开）在蓝牙模式下不存在。

### 2.3 为什么枚举不到 GATT

已连接的 HID 设备由 `bluetoothd` 接管，CoreBluetooth 扫描结果里不会出现它（实测扫到 18 个
其它 BLE 设备，没有键盘）。要枚举只能让键盘切到**未配对的蓝牙通道**进入广播状态再连。
`IOBluetooth` 框架也没有任何公开 GATT API（头文件里只有 SDP），所以已连接状态下无解。

一次实测：`ble-probe` 被动扫描 15 秒，键盘未出现。

## 3. 结论

- 语音键与键盘麦克风只存在于 **2.4G 接收器**模式。
- Mac 端应继续走「`IOHIDManager` 读接收器厂商通道 + 发送系统听写快捷键」。
- Windows 端同理（见 `../MIIWVoiceWin/`），只是把快捷键换成 `Win+H`。

## 4. 复现工具

`tools/` 下两个备用工具：

- `ble-probe.swift` —— CoreBluetooth 扫描与 GATT 枚举。`swiftc -O ble-probe.swift -o /tmp/ble-probe`，
  默认只被动扫描，加 `--connect` 才会主动连接。
- `hid-dump.c` —— 导出 VID `0x3044` / PID `0x507c` 所有 HID 原始报告，USB 与 BLE 通道都能抓。
  `clang hid-dump.c -framework IOKit -framework CoreFoundation -o hid-dump`
