# 探索工具

诊断用的小工具，构建/运行都只依赖系统自带工具。

## `hid-dump.c`

导出 VID `0x3044` / PID `0x507c`（米物接收器、米物键盘）的全部 HID 原始报告，USB 与 BLE
通道都能抓。用来确认语音键的报告字节、对比不同固件的差异。

```sh
clang hid-dump.c -framework IOKit -framework CoreFoundation -o hid-dump
./hid-dump 30      # 监听 30 秒
```

输出示例（USB 接收器的厂商通道，17 字节报告）：

```
[USB] MIIIW Elite Receiver report 1 len 17: 01 b0 01 00 ... 7b
[USB] MIIIW Elite Receiver report 1 len 17: 01 b0 03 00 ... 23
[USB] MIIIW Elite Receiver report 1 len 17: 01 b0 00 00 ... 57
```

## `ble-probe.swift`

CoreBluetooth 扫描与 GATT 枚举，用于确认某个 BLE 设备到底暴露了哪些服务/特征。

```sh
swiftc -O ble-probe.swift -o /tmp/ble-probe
/tmp/ble-probe 15             # 只被动扫描，不连接
/tmp/ble-probe 30 --connect   # 扫到名字含 MIIIW 的设备后主动连接并枚举
```

连接后会打印所有服务、特征、属性、描述符，并对所有可通知的特征自动订阅，随后把收到的值
逐条打印出来 —— 想验证「按某个键会不会产生私有通知」时直接跑这个。

结论参见 `../docs/ble-notes.md`：已连接的系统 HID 键盘不会被 CoreBluetooth 看到。
