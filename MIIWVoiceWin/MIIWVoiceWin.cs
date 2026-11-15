// MIIW Voice for Windows
//
// 把米物精英键盘（VID 0x3044 / PID 0x507c 的 USB 接收器）上的语音键翻译成 Windows 自带的
// 语音输入快捷键 Win+H。不依赖搜狗、不需要驱动、不需要管理员权限。
//
// 构建：双击 build.bat（用 Windows 自带的 csc.exe，不必安装 Visual Studio）。
// 目标框架：.NET Framework 4.x（Windows 10/11 自带），因此本文件只用 C# 5 语法。

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MiiwVoice
{
    internal static class Log
    {
        private static readonly object Gate = new object();
        private static string path;

        public static string Path
        {
            get
            {
                if (path == null)
                {
                    string dir = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "MIIWVoice");
                    try { Directory.CreateDirectory(dir); } catch { }
                    path = System.IO.Path.Combine(dir, "voice.log");
                }
                return path;
            }
        }

        public static void Write(string message)
        {
            lock (Gate)
            {
                try
                {
                    FileInfo fi = new FileInfo(Path);
                    if (fi.Exists && fi.Length > 256 * 1024) fi.Delete();
                    File.AppendAllText(Path,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + message + Environment.NewLine);
                }
                catch { }
            }
        }
    }

    internal static class Native
    {
        public const int VkLWin = 0x5B;
        public const int VkEscape = 0x1B;
        public const uint KeyEventKeyUp = 0x0002;
        public const uint InputKeyboard = 1;

        [StructLayout(LayoutKind.Sequential)]
        public struct MouseInput
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KeyboardInput
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HardwareInput
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)] public MouseInput mi;
            [FieldOffset(0)] public KeyboardInput ki;
            [FieldOffset(0)] public HardwareInput hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Input
        {
            public uint type;
            public InputUnion u;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint count, Input[] inputs, int size);

        // ---- HID ----

        [StructLayout(LayoutKind.Sequential)]
        public struct HidAttributes
        {
            public uint Size;
            public ushort VendorID;
            public ushort ProductID;
            public ushort VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HidpCaps
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SpDeviceInterfaceData
        {
            public uint cbSize;
            public Guid InterfaceClassGuid;
            public uint Flags;
            public IntPtr Reserved;
        }

        [DllImport("hid.dll")]
        public static extern void HidD_GetHidGuid(out Guid guid);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetAttributes(IntPtr device, out HidAttributes attributes);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetPreparsedData(IntPtr device, out IntPtr data);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_FreePreparsedData(IntPtr data);

        [DllImport("hid.dll")]
        public static extern int HidP_GetCaps(IntPtr data, ref HidpCaps caps);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_SetNumInputBuffers(IntPtr device, int buffers);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr parent, uint flags);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref Guid classGuid,
            uint index, ref SpDeviceInterfaceData data);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SpDeviceInterfaceData data,
            IntPtr detail, uint detailSize, out uint required, IntPtr info);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security,
            uint disposition, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadFile(IntPtr file, byte[] buffer, int count, out int read, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteFile(IntPtr file, byte[] buffer, int count, out int written, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr handle);

        public const uint DigcfPresent = 0x00000002;
        public const uint DigcfDeviceInterface = 0x00000010;
        public const uint GenericRead = 0x80000000;
        public const uint GenericWrite = 0x40000000;
        public const uint FileShareRead = 0x00000001;
        public const uint FileShareWrite = 0x00000002;
        public const uint OpenExisting = 3;
        public static readonly IntPtr InvalidHandle = new IntPtr(-1);

        // ---- winmm waveIn（录音）----

        public const ushort VkReturn = 0x0D;

        [StructLayout(LayoutKind.Sequential)]
        public struct WaveFormatEx
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WaveInCaps
        {
            public ushort wMid;
            public ushort wPid;
            public uint vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szPname;
            public uint dwFormats;
            public ushort wChannels;
            public ushort wReserved1;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WaveHdr
        {
            public IntPtr lpData;
            public uint dwBufferLength;
            public uint dwBytesRecorded;
            public IntPtr dwUser;
            public uint dwFlags;
            public uint dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }

        [DllImport("winmm.dll")]
        public static extern int waveInGetNumDevs();

        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        public static extern int waveInGetDevCaps(IntPtr deviceId, ref WaveInCaps caps, int size);

        [DllImport("winmm.dll")]
        public static extern int waveInOpen(out IntPtr handle, int deviceId, ref WaveFormatEx format,
            IntPtr callback, IntPtr instance, int flags);

        [DllImport("winmm.dll")]
        public static extern int waveInPrepareHeader(IntPtr handle, IntPtr header, int size);

        [DllImport("winmm.dll")]
        public static extern int waveInUnprepareHeader(IntPtr handle, IntPtr header, int size);

        [DllImport("winmm.dll")]
        public static extern int waveInAddBuffer(IntPtr handle, IntPtr header, int size);

        [DllImport("winmm.dll")]
        public static extern int waveInStart(IntPtr handle);

        [DllImport("winmm.dll")]
        public static extern int waveInStop(IntPtr handle);

        [DllImport("winmm.dll")]
        public static extern int waveInReset(IntPtr handle);

        [DllImport("winmm.dll")]
        public static extern int waveInClose(IntPtr handle);
    }

    /// <summary>发送系统快捷键（Win+H 等）。</summary>
    internal static class KeySender
    {
        public static bool Enabled = true;
        public static bool SlowCombo = false;
        /// <summary>松手时：true = 再按一次 Win+H（切换关闭），false = 发送 Esc。</summary>
        public static bool StopByToggle = true;

        private static Native.Input Key(int vk, bool up)
        {
            Native.Input input = new Native.Input();
            input.type = Native.InputKeyboard;
            input.u.ki.wVk = (ushort)vk;
            input.u.ki.dwFlags = up ? Native.KeyEventKeyUp : 0;
            return input;
        }

        private static bool Send(Native.Input[] inputs)
        {
            uint sent = Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Native.Input)));
            if (sent == 0)
            {
                Log.Write("SendInput 失败，错误码 " + Marshal.GetLastWin32Error());
                return false;
            }
            return true;
        }

        /// <summary>Win + 指定虚拟键。</summary>
        public static void SendWinCombo(int vk)
        {
            if (!Enabled)
            {
                Log.Write("（已禁用按键注入）Win+" + ((char)vk));
                return;
            }
            if (SlowCombo)
            {
                Send(new[] { Key(Native.VkLWin, false) });
                Thread.Sleep(25);
                Send(new[] { Key(vk, false), Key(vk, true) });
                Thread.Sleep(25);
                Send(new[] { Key(Native.VkLWin, true) });
            }
            else
            {
                Send(new[] {
                    Key(Native.VkLWin, false),
                    Key(vk, false),
                    Key(vk, true),
                    Key(Native.VkLWin, true),
                });
            }
            Log.Write("注入 Win+" + ((char)vk));
        }

        public static void StartVoiceTyping()
        {
            SendWinCombo('H');
        }

        public static void StopVoiceTyping()
        {
            if (StopByToggle)
            {
                SendWinCombo('H');
            }
            else if (Enabled)
            {
                Send(new[] { Key(Native.VkEscape, false), Key(Native.VkEscape, true) });
                Log.Write("注入 Esc");
            }
        }
    }

    /// <summary>
    /// 后台线程查找接收器的厂商 HID 通道（Usage Page 0xFF00 / Usage 0x0001）并读取 17 字节报告。
    /// 状态通过队列交给 UI 线程处理，避免跨线程操作控件。
    /// </summary>
    internal sealed class HidWatcher : IDisposable
    {
        private const ushort VendorId = 0x3044;
        private const ushort ProductId = 0x507c;
        private const ushort VendorUsagePage = 0xFF00;
        private const ushort VendorUsage = 0x0001;

        /// <summary>
        /// 发送 B2 01 后等多久才触发听写。接收器每次开麦都会产生一个直流冲击：
        /// 采样值从 0 指数型冲到 int16 下限并持续修约 300~400ms，这段音频完全不可用。
        /// 太早触发听写会让识别器一开始就吃到爆音。
        /// </summary>
        private const int MicWarmupMs = 450;

        private readonly Thread thread;
        private readonly object queueGate = new object();
        private readonly Queue<bool> pending = new Queue<bool>();
        private readonly bool keyboardMic;
        private readonly bool dump;

        private readonly object writeGate = new object();

        private volatile bool stopping;
        private volatile bool connected;
        private IntPtr device = Native.InvalidHandle;
        private System.Threading.Timer keepalive;
        private int inputReportLength = 17;

        public HidWatcher(bool keyboardMic, bool dump)
        {
            this.keyboardMic = keyboardMic;
            this.dump = dump;
            thread = new Thread(Run);
            thread.IsBackground = true;
            thread.Name = "MIIW HID";
        }

        public bool Connected
        {
            get { return connected; }
        }

        public void Start()
        {
            thread.Start();
        }

        /// <summary>取出一个语音键事件：true = 按下，false = 松开。</summary>
        public bool TryDequeue(out bool down)
        {
            lock (queueGate)
            {
                if (pending.Count == 0)
                {
                    down = false;
                    return false;
                }
                down = pending.Dequeue();
                return true;
            }
        }

        public void Dispose()
        {
            stopping = true;
            StopKeepalive();
            IntPtr handle = device;
            device = Native.InvalidHandle;
            if (handle != Native.InvalidHandle) Native.CloseHandle(handle);
        }

        private void Enqueue(bool down)
        {
            lock (queueGate)
            {
                if (pending.Count > 64) pending.Clear();
                pending.Enqueue(down);
            }
        }

        private void Run()
        {
            while (!stopping)
            {
                IntPtr handle = OpenVendorCollection();
                if (handle == Native.InvalidHandle)
                {
                    Thread.Sleep(1500);
                    continue;
                }

                device = handle;
                connected = true;
                Log.Write("已打开接收器语音键通道");
                InitializeVoiceChannel(handle);
                StartKeepalive(handle);
                ReadLoop(handle);
                StopKeepalive();
                connected = false;

                if (device == handle)
                {
                    device = Native.InvalidHandle;
                    Native.CloseHandle(handle);
                }
                if (!stopping)
                {
                    Log.Write("接收器断开，1.5s 后重试");
                    Thread.Sleep(1500);
                }
            }
        }

        private void ReadLoop(IntPtr handle)
        {
            // ReadFile 一次可能返回多条报告拼在一起（实测三条合法），所以缓冲区正好取一条报告
            // 的长度，保证每次读到的都是完整的一条，不会把夹在中间的语音键报告漏掉。
            byte[] buffer = new byte[inputReportLength];
            int read;
            bool held = false;
            while (!stopping)
            {
                if (!Native.ReadFile(handle, buffer, buffer.Length, out read, IntPtr.Zero)) break;
                if (read < inputReportLength) continue;
                if (dump) Log.Write("report " + Hex(buffer, read));

                // 报告形如 01 B0 <state> ...：01 是 Report ID，B0 是语音键通道。
                if (buffer[0] != 0x01 || buffer[1] != 0xB0) continue;

                byte state = buffer[2];
                if (state != 0)
                {
                    if (held) continue;            // B0 03 是按住时的重复帧
                    held = true;
                    Log.Write("语音键按下 (state=" + state + ")");
                    if (keyboardMic)
                    {
                        SetKeyboardMic(true);
                        // 等开麦爆音过去再触发听写，否则识别器开头就是一段撞满量程的噪声。
                        Thread.Sleep(MicWarmupMs);
                    }
                    Enqueue(true);
                }
                else if (held)
                {
                    held = false;
                    Log.Write("语音键松开");
                    Enqueue(false);
                    if (keyboardMic)
                    {
                        Thread.Sleep(MicWarmupMs);
                        SetKeyboardMic(false);
                    }
                }
            }
        }

        /// <summary>
        /// 向 MI_05 厂商通道写一条命令。报文体固定 128 字节：Report ID 02 + 14 字节数据 + 校验，
        /// 其余零填充。校验值是抓包得到的固定常量，只用于写入。
        /// </summary>
        private void WriteCommand(IntPtr handle, byte command, byte value, byte checksum, string label)
        {
            if (handle == Native.InvalidHandle) return;

            byte[] buffer = new byte[128];
            buffer[0] = 0x02;
            buffer[1] = command;
            buffer[2] = value;
            buffer[127] = checksum;

            // 必须用 WriteFile（IOCTL_HID_WRITE_REPORT）。HidD_SetOutputReport 对这个接收器
            // 会返回 TRUE 但实际一条报告也不发，导致初始化握手和 B2 开关全部静默失效。
            bool ok;
            int written = 0;
            lock (writeGate)
            {
                ok = Native.WriteFile(handle, buffer, buffer.Length, out written, IntPtr.Zero)
                     && written == buffer.Length;
            }
            if (!ok) Log.Write(label + " 失败，错误码 " + Marshal.GetLastWin32Error() + "，已写入 " + written);
            else if (dump) Log.Write(label + " 已发送");
        }

        /// <summary>
        /// 初始化语音通道：E1/D1/E2/B1 各两轮，时序取自搜狗助手抓包。
        /// 缺了这一步接收器不会给无线麦克风通电，只发 B2 01 会是完全静音的。
        /// </summary>
        private void InitializeVoiceChannel(IntPtr handle)
        {
            byte[][] sequence =
            {
                new byte[] { 0xE1, 0x00, 0xF3 }, // 查固件
                new byte[] { 0xD1, 0x00, 0x93 }, // 查设备身份
                new byte[] { 0xE2, 0x00, 0xF5 }, // 保活
                new byte[] { 0xB1, 0x00, 0x53 }, // 初始化语音通道
                new byte[] { 0xE1, 0x00, 0xF3 },
                new byte[] { 0xD1, 0x00, 0x93 },
                new byte[] { 0xE2, 0x00, 0xF5 },
                new byte[] { 0xB1, 0x00, 0x53 },
            };
            int[] delays = { 500, 480, 20, 20, 480, 480, 20, 20 };

            for (int i = 0; i < sequence.Length && !stopping; i++)
            {
                WriteCommand(handle, sequence[i][0], sequence[i][1], sequence[i][2], "初始化");
                Thread.Sleep(delays[i]);
            }
            Log.Write("语音通道初始化完成");
        }

        /// <summary>助手在初始化后每 6 秒发一次 E2，作为无线链路的保活。</summary>
        private void StartKeepalive(IntPtr handle)
        {
            StopKeepalive();
            keepalive = new System.Threading.Timer(
                delegate { WriteCommand(device, 0xE2, 0x00, 0xF5, "保活"); },
                null, 6000, 6000);
        }

        private void StopKeepalive()
        {
            System.Threading.Timer timer = keepalive;
            keepalive = null;
            if (timer != null) timer.Dispose();
        }

        /// <summary>米物麦克风模式：按下时让接收器打开键盘麦克风，松手关闭。</summary>
        private void SetKeyboardMic(bool open)
        {
            WriteCommand(device, 0xB2, (byte)(open ? 1 : 0), (byte)(open ? 0x3D : 0x55),
                "麦克风 " + (open ? "开" : "关"));
        }

        private IntPtr OpenVendorCollection()
        {
            Guid hidGuid;
            Native.HidD_GetHidGuid(out hidGuid);

            IntPtr set = Native.SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero,
                Native.DigcfPresent | Native.DigcfDeviceInterface);
            if (set == Native.InvalidHandle) return Native.InvalidHandle;

            try
            {
                Native.SpDeviceInterfaceData data = new Native.SpDeviceInterfaceData();
                data.cbSize = (uint)Marshal.SizeOf(typeof(Native.SpDeviceInterfaceData));
                for (uint index = 0; ; index++)
                {
                    if (!Native.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, index, ref data)) break;
                    string name = GetPath(set, ref data);
                    if (name == null) continue;

                    IntPtr handle = Native.CreateFile(name,
                        Native.GenericRead | Native.GenericWrite,
                        Native.FileShareRead | Native.FileShareWrite,
                        IntPtr.Zero, Native.OpenExisting, 0, IntPtr.Zero);
                    if (handle == Native.InvalidHandle) continue;

                    if (IsVendorCollection(handle))
                    {
                        Native.HidD_SetNumInputBuffers(handle, 64);
                        int length = GetInputReportLength(handle);
                        if (length > 0 && length <= 512) inputReportLength = length;
                        return handle;
                    }
                    Native.CloseHandle(handle);
                }
            }
            finally
            {
                Native.SetupDiDestroyDeviceInfoList(set);
            }
            return Native.InvalidHandle;
        }

        private static string GetPath(IntPtr set, ref Native.SpDeviceInterfaceData data)
        {
            uint needed;
            Native.SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out needed, IntPtr.Zero);
            if (needed == 0) return null;

            IntPtr buffer = Marshal.AllocHGlobal((int)needed);
            try
            {
                // SP_DEVICE_INTERFACE_DETAIL_DATA_W.cbSize：64 位为 8，32 位为 6。
                Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 6);
                uint written;
                if (!Native.SetupDiGetDeviceInterfaceDetail(set, ref data, buffer, needed, out written, IntPtr.Zero))
                    return null;
                return Marshal.PtrToStringUni(new IntPtr(buffer.ToInt64() + 4));
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static bool IsVendorCollection(IntPtr handle)
        {
            Native.HidAttributes attributes = new Native.HidAttributes();
            attributes.Size = (uint)Marshal.SizeOf(typeof(Native.HidAttributes));
            if (!Native.HidD_GetAttributes(handle, out attributes)) return false;
            if (attributes.VendorID != VendorId || attributes.ProductID != ProductId) return false;

            IntPtr preparsed;
            if (!Native.HidD_GetPreparsedData(handle, out preparsed)) return false;
            try
            {
                Native.HidpCaps caps = new Native.HidpCaps();
                if (Native.HidP_GetCaps(preparsed, ref caps) < 0) return false;
                return caps.UsagePage == VendorUsagePage && caps.Usage == VendorUsage;
            }
            finally
            {
                Native.HidD_FreePreparsedData(preparsed);
            }
        }

        private static int GetInputReportLength(IntPtr handle)
        {
            IntPtr preparsed;
            if (!Native.HidD_GetPreparsedData(handle, out preparsed)) return 0;
            try
            {
                Native.HidpCaps caps = new Native.HidpCaps();
                if (Native.HidP_GetCaps(preparsed, ref caps) < 0) return 0;
                return caps.InputReportByteLength;
            }
            finally
            {
                Native.HidD_FreePreparsedData(preparsed);
            }
        }

        private static string Hex(byte[] data, int length)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(length * 3);
            for (int i = 0; i < length; i++) sb.Append(data[i].ToString("x2")).Append(' ');
            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// 用 winmm 的 waveIn 录音：16 kHz / 16-bit / 单声道，不依赖任何第三方库。
    /// 一次性分配整块缓冲区（默认最长 60 秒），松开时一次性取出。
    /// </summary>
    internal sealed class MicRecorder : IDisposable
    {
        private const int Rate = 16000;
        private const int Channels = 1;
        private const int Bits = 16;

        private IntPtr handle = IntPtr.Zero;
        private IntPtr headerPtr = IntPtr.Zero;
        private IntPtr dataPtr = IntPtr.Zero;
        private int capacity;
        private bool open;

        /// <summary>按名字片段查找录音设备序号，找不到返回 -1。</summary>
        public static int FindDevice(string namePart)
        {
            int count = Native.waveInGetNumDevs();
            int size = Marshal.SizeOf(typeof(Native.WaveInCaps));
            for (int i = 0; i < count; i++)
            {
                Native.WaveInCaps caps = new Native.WaveInCaps();
                if (Native.waveInGetDevCaps(new IntPtr(i), ref caps, size) != 0) continue;
                if (caps.szPname != null &&
                    caps.szPname.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
            }
            return -1;
        }

        public static string DescribeDevices()
        {
            StringBuilder sb = new StringBuilder();
            int count = Native.waveInGetNumDevs();
            int size = Marshal.SizeOf(typeof(Native.WaveInCaps));
            for (int i = 0; i < count; i++)
            {
                Native.WaveInCaps caps = new Native.WaveInCaps();
                if (Native.waveInGetDevCaps(new IntPtr(i), ref caps, size) != 0) continue;
                sb.Append(i).Append('=').Append(caps.szPname).Append("; ");
            }
            return sb.ToString();
        }

        public void Start(int maxSeconds, bool preferKeyboardMic)
        {
            Stop();   // 清掉上一次残留

            capacity = Rate * (Bits / 8) * Channels * maxSeconds;
            dataPtr = Marshal.AllocHGlobal(capacity);
            int headerSize = Marshal.SizeOf(typeof(Native.WaveHdr));
            headerPtr = Marshal.AllocHGlobal(headerSize);

            Native.WaveFormatEx format = new Native.WaveFormatEx();
            format.wFormatTag = 1;                 // WAVE_FORMAT_PCM
            format.nChannels = Channels;
            format.nSamplesPerSec = Rate;
            format.wBitsPerSample = Bits;
            format.nBlockAlign = (ushort)(Channels * Bits / 8);
            format.nAvgBytesPerSec = (uint)(Rate * format.nBlockAlign);
            format.cbSize = 0;

            int deviceId = preferKeyboardMic ? FindDevice("MIIIW") : -1;
            if (deviceId < 0) deviceId = 0;        // WAVE_MAPPER：系统默认输入

            int rc = Native.waveInOpen(out handle, deviceId, ref format, IntPtr.Zero, IntPtr.Zero, 0);
            if (rc != 0)
            {
                Release();
                throw new InvalidOperationException("waveInOpen 失败，错误码 " + rc);
            }

            Native.WaveHdr header = new Native.WaveHdr();
            header.lpData = dataPtr;
            header.dwBufferLength = (uint)capacity;
            Marshal.StructureToPtr(header, headerPtr, false);

            Native.waveInPrepareHeader(handle, headerPtr, headerSize);
            Native.waveInAddBuffer(handle, headerPtr, headerSize);
            rc = Native.waveInStart(handle);
            if (rc != 0)
            {
                Release();
                throw new InvalidOperationException("waveInStart 失败，错误码 " + rc);
            }
            open = true;
        }

        /// <summary>停止录音并返回完整 WAV 字节（含 44 字节头）；没录到东西返回 null。</summary>
        public byte[] Stop()
        {
            if (!open)
            {
                Release();
                return null;
            }
            open = false;

            int headerSize = Marshal.SizeOf(typeof(Native.WaveHdr));
            Native.waveInStop(handle);
            Native.WaveHdr header = (Native.WaveHdr)Marshal.PtrToStructure(headerPtr, typeof(Native.WaveHdr));
            int recorded = (int)header.dwBytesRecorded;
            Native.waveInReset(handle);
            Native.waveInUnprepareHeader(handle, headerPtr, headerSize);
            Native.waveInClose(handle);
            handle = IntPtr.Zero;

            if (recorded < 0 || recorded > capacity) recorded = 0;
            byte[] pcm = new byte[recorded];
            if (recorded > 0) Marshal.Copy(dataPtr, pcm, 0, recorded);
            Release();

            return recorded == 0 ? null : BuildWav(pcm);
        }

        private void Release()
        {
            if (handle != IntPtr.Zero) { Native.waveInClose(handle); handle = IntPtr.Zero; }
            if (headerPtr != IntPtr.Zero) { Marshal.FreeHGlobal(headerPtr); headerPtr = IntPtr.Zero; }
            if (dataPtr != IntPtr.Zero) { Marshal.FreeHGlobal(dataPtr); dataPtr = IntPtr.Zero; }
        }

        public void Dispose()
        {
            Stop();
        }

        private static byte[] BuildWav(byte[] pcm)
        {
            byte[] wav = new byte[44 + pcm.Length];
            using (MemoryStream stream = new MemoryStream(wav))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                int blockAlign = Channels * Bits / 8;
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + pcm.Length);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)Channels);
                writer.Write(Rate);
                writer.Write(Rate * blockAlign);
                writer.Write((short)blockAlign);
                writer.Write((short)Bits);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(pcm.Length);
                writer.Write(pcm);
            }
            return wav;
        }
    }

    /// <summary>
    /// 小米 MiMo 的 ASR（mimo-v2.5-asr）。走 OpenAI 风格的 chat/completions，
    /// 消息里只能有一个 input_audio part，不能带任何 text part（网关自己注入提示词）。
    /// </summary>
    internal static class MimoAsr
    {
        public static string BaseUrl = "";
        public static string ApiKey = "";

        public static bool Ready
        {
            get { return BaseUrl.Length > 0 && ApiKey.Length > 0; }
        }

        /// <summary>先读进程环境变量，读不到就回落到注册表里的用户环境变量（setx 写的就是那里）。</summary>
        public static string ReadSetting(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(value)) return value;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Environment"))
                {
                    if (key != null)
                    {
                        object stored = key.GetValue(name);
                        if (stored != null) return stored.ToString();
                    }
                }
            }
            catch { }
            return "";
        }

        /// <summary>成功返回文字（可能为空字符串），失败返回 null。</summary>
        public static string Transcribe(byte[] wav)
        {
            string json = "{\"model\":\"mimo-v2.5-asr\",\"messages\":[{\"role\":\"user\",\"content\":[" +
                          "{\"type\":\"input_audio\",\"input_audio\":{\"data\":\"" +
                          Convert.ToBase64String(wav) + "\",\"format\":\"wav\"}}]}]}";
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(BaseUrl + "/chat/completions");
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Headers["Authorization"] = "Bearer " + ApiKey;
                request.Timeout = 60000;
                request.ReadWriteTimeout = 60000;
                request.KeepAlive = false;

                byte[] payload = Encoding.UTF8.GetBytes(json);
                using (Stream body = request.GetRequestStream())
                    body.Write(payload, 0, payload.Length);

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    return ParseContent(reader.ReadToEnd());
            }
            catch (WebException ex)
            {
                string detail = "";
                try
                {
                    if (ex.Response != null)
                        using (StreamReader reader = new StreamReader(ex.Response.GetResponseStream(), Encoding.UTF8))
                            detail = reader.ReadToEnd();
                }
                catch { }
                Log.Write("ASR 请求失败: " + ex.Message + " " + TextUtil.Trim(detail, 300));
                return null;
            }
            catch (Exception ex)
            {
                Log.Write("ASR 异常: " + ex.GetType().Name + " " + ex.Message);
                return null;
            }
        }

        private static string ParseContent(string body)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            Dictionary<string, object> root = serializer.DeserializeObject(body) as Dictionary<string, object>;
            if (root == null || !root.ContainsKey("choices")) return null;
            object[] choices = root["choices"] as object[];
            if (choices == null || choices.Length == 0) return null;
            Dictionary<string, object> first = choices[0] as Dictionary<string, object>;
            if (first == null) return null;
            Dictionary<string, object> message = first["message"] as Dictionary<string, object>;
            if (message == null) return null;
            object content = message.ContainsKey("content") ? message["content"] : null;
            return content == null ? "" : content.ToString().Trim();
        }
    }

    /// <summary>把文字用 SendInput 打出去。中文依靠 KEYEVENTF_UNICODE，不受键盘布局影响。</summary>
    internal static class Typer
    {
        private const uint KeyEventUnicode = 0x0004;
        private const uint KeyEventKeyUp = 0x0002;

        public static void TypeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (char c in text)
            {
                if (c == '\r') continue;
                if (c == '\n')
                {
                    Send(Native.VkReturn, (char)0, 0);
                    Send(Native.VkReturn, (char)0, KeyEventKeyUp);
                    continue;
                }
                Send(0, c, KeyEventUnicode);
                Send(0, c, KeyEventUnicode | KeyEventKeyUp);
            }
        }

        private static void Send(ushort virtualKey, char scanCode, uint flags)
        {
            Native.Input input = new Native.Input();
            input.type = Native.InputKeyboard;
            input.u.ki.wVk = virtualKey;
            input.u.ki.wScan = scanCode;
            input.u.ki.dwFlags = flags;
            Native.SendInput(1, new[] { input }, Marshal.SizeOf(typeof(Native.Input)));
        }
    }

    internal static class TextUtil
    {
        public static string Trim(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace('\r', ' ').Replace('\n', ' ');
            return text.Length <= max ? text : text.Substring(0, max) + "…";
        }
    }

    internal sealed class TrayApp : ApplicationContext
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValue = "MIIWVoice";

        private readonly NotifyIcon icon;
        private readonly ToolStripMenuItem statusItem;
        private readonly ToolStripMenuItem startupItem;
        private readonly HidWatcher watcher;
        private readonly System.Windows.Forms.Timer timer;
        private readonly MicRecorder recorder = new MicRecorder();
        private readonly bool asrMode;
        private readonly bool preferKeyboardMic;
        private bool dictationOn;
        private bool recording;
        private volatile string lastText;

        public TrayApp(bool keyboardMic, bool dump, bool startWithWindows, bool asrMode)
        {
            this.asrMode = asrMode;
            preferKeyboardMic = keyboardMic;
            watcher = new HidWatcher(keyboardMic, dump);

            statusItem = new ToolStripMenuItem("正在查找接收器…");
            statusItem.Enabled = false;

            startupItem = new ToolStripMenuItem("登录时自动运行", null, OnToggleStartup);
            startupItem.Checked = IsStartupEnabled();

            if (startWithWindows && !IsStartupEnabled()) SetStartup(true);
            startupItem.Checked = IsStartupEnabled();

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add(statusItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(startupItem);
            menu.Items.Add(new ToolStripMenuItem("打开日志", null, OnOpenLog));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("退出", null, OnExit));

            icon = new NotifyIcon();
            icon.Icon = CreateIcon();
            icon.Text = "米物语音键";
            icon.ContextMenuStrip = menu;
            icon.Visible = true;
            icon.DoubleClick += OnOpenLog;

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 80;
            timer.Tick += OnTick;
            timer.Start();

            watcher.Start();
            Log.Write("启动（模式=" + (asrMode ? "应用内识别 " + MimoAsr.BaseUrl : "Win+H 快捷键") +
                      "，麦克风=" + (keyboardMic ? "键盘自带" : "系统默认") + "）");
            if (asrMode) Log.Write("录音设备: " + MicRecorder.DescribeDevices());
        }

        private void OnTick(object sender, EventArgs e)
        {
            string text = lastText;
            statusItem.Text = !watcher.Connected
                ? "正在查找接收器…（插入米物接收器，键盘切到 2.4G 模式）"
                : (text != null
                    ? "上次识别：" + TextUtil.Trim(text, 40)
                    : ReadyHint());

            bool down;
            while (watcher.TryDequeue(out down))
            {
                if (asrMode) HandleAsr(down);
                else HandleHotkey(down);
            }
        }

        private string ReadyHint()
        {
            if (recording) return "录音中…松开结束";
            return asrMode ? "语音键已就绪：按住说话，松开出字" : "语音键已就绪：按一下开始 / 再按一下结束";
        }

        /// <summary>旧模式：把语音键翻译成 Win+H。</summary>
        private void HandleHotkey(bool down)
        {
            if (down)
            {
                KeySender.StartVoiceTyping();
                dictationOn = true;
            }
            else
            {
                if (dictationOn) KeySender.StopVoiceTyping();
                dictationOn = false;
            }
        }

        /// <summary>按住录音，松开后上传识别并打出文字。</summary>
        private void HandleAsr(bool down)
        {
            if (down)
            {
                try
                {
                    recorder.Start(60, preferKeyboardMic);
                    recording = true;
                    Log.Write("开始录音");
                }
                catch (Exception ex)
                {
                    recording = false;
                    Log.Write("录音启动失败: " + ex.Message);
                }
                return;
            }

            if (!recording) return;
            recording = false;

            byte[] wav;
            try { wav = recorder.Stop(); }
            catch (Exception ex) { Log.Write("录音停止失败: " + ex.Message); return; }

            if (wav == null || wav.Length <= 44)
            {
                Log.Write("没录到音频");
                return;
            }

            int milliseconds = (wav.Length - 44) / 32;   // 16000 Hz * 2 字节 = 32 字节/毫秒
            int loud = CountLoudBytes(wav);
            Log.Write("录音 " + milliseconds + " ms，非零字节 " + loud);
            if (milliseconds < 200)
            {
                Log.Write("录音太短，忽略");
                return;
            }
            if (loud < (wav.Length - 44) / 200)
            {
                Log.Write("录音几乎全零，键盘可能处于休眠；请先按一下键盘再试");
                return;
            }

            ThreadPool.QueueUserWorkItem(delegate { RecognizeAndType(wav); });
        }

        private void RecognizeAndType(byte[] wav)
        {
            string text = MimoAsr.Transcribe(wav);
            if (text == null)
            {
                lastText = "（识别请求失败，见日志）";
                return;
            }
            if (text.Length == 0)
            {
                Log.Write("识别结果为空");
                lastText = "（没听清）";
                return;
            }
            Log.Write("识别: " + text);
            lastText = text;
            Typer.TypeText(text);
        }

        private static int CountLoudBytes(byte[] wav)
        {
            int count = 0;
            for (int i = 44; i < wav.Length; i++) if (wav[i] != 0) count++;
            return count;
        }

        private void OnToggleStartup(object sender, EventArgs e)
        {
            SetStartup(!IsStartupEnabled());
            startupItem.Checked = IsStartupEnabled();
        }

        private void OnOpenLog(object sender, EventArgs e)
        {
            try { System.Diagnostics.Process.Start("notepad.exe", Log.Path); }
            catch (Exception ex) { Log.Write("打开日志失败: " + ex.Message); }
        }

        private void OnExit(object sender, EventArgs e)
        {
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Stop();
                timer.Dispose();
                watcher.Dispose();
                icon.Visible = false;
                icon.Dispose();
            }
            base.Dispose(disposing);
        }

        private static bool IsStartupEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
            {
                if (key == null) return false;
                return key.GetValue(RunValue) != null;
            }
        }

        private static void SetStartup(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (key == null) return;
                    if (enabled) key.SetValue(RunValue, "\"" + Application.ExecutablePath + "\"");
                    else key.DeleteValue(RunValue, false);
                }
            }
            catch (Exception ex)
            {
                Log.Write("设置开机启动失败: " + ex.Message);
            }
        }

        private static Icon CreateIcon()
        {
            try
            {
                Bitmap bitmap = new Bitmap(16, 16);
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using (SolidBrush body = new SolidBrush(Color.FromArgb(70, 165, 255)))
                    using (Pen line = new Pen(Color.FromArgb(70, 165, 255), 1.6f))
                    {
                        g.FillEllipse(body, 5, 2, 6, 7);
                        g.DrawArc(line, 3, 4, 10, 8, 0, 180);
                        g.DrawLine(line, 8, 12, 8, 14);
                    }
                }
                return Icon.FromHandle(bitmap.GetHicon());
            }
            catch
            {
                return SystemIcons.Application;
            }
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool keyboardMic = false;
            bool dump = false;
            bool startWithWindows = false;
            bool asrMode = true;
            bool systemMic = false;
            string baseUrl = null;
            string apiKey = null;

            foreach (string arg in args)
            {
                string a = arg.ToLowerInvariant();
                if (a == "--keyboard-mic") keyboardMic = true;                // 使用键盘自带麦克风
                else if (a == "--system-mic") systemMic = true;               // 用系统默认麦克风
                else if (a == "--dump") dump = true;                          // 记录所有原始报告
                else if (a == "--no-toggle") KeySender.StopByToggle = false;   // 松手发 Esc
                else if (a == "--slow") KeySender.SlowCombo = true;            // 分步发送
                else if (a == "--no-input") KeySender.Enabled = false;         // 只记日志，不注入按键
                else if (a == "--autostart") startWithWindows = true;
                else if (a == "--win-h") asrMode = false;                     // 退回 Win+H 模式
                else if (a == "--asr") asrMode = true;                        // 应用内识别（默认）
                else if (a.StartsWith("--base-url=")) baseUrl = arg.Substring(11);
                else if (a.StartsWith("--api-key=")) apiKey = arg.Substring(10);
            }

            MimoAsr.BaseUrl = baseUrl ?? MimoAsr.ReadSetting("MIMO_BASE_URL");
            MimoAsr.ApiKey = apiKey ?? MimoAsr.ReadSetting("MIMO_API_KEY");
            if (MimoAsr.BaseUrl.Length > 0) MimoAsr.BaseUrl = MimoAsr.BaseUrl.TrimEnd('/');
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; }
            catch { }

            // 应用内识别必须用键盘自带麦克风（否则语音键和麦克风对不上）且必须配置好接口。
            if (asrMode && !systemMic) keyboardMic = true;

            bool created;
            using (Mutex mutex = new Mutex(true, @"Local\MIIWVoice.SingleInstance", out created))
            {
                if (!created)
                {
                    MessageBox.Show("米物语音键已经在运行了。", "米物语音键",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
                {
                    Log.Write("界面线程异常: " + e.Exception);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
                {
                    Log.Write("未处理异常: " + e.ExceptionObject);
                };
                Log.Write("==== 启动 ====" +
                    (asrMode
                        ? "  模式=应用内识别（mimo-v2.5-asr），麦克风=" + (keyboardMic ? "键盘自带" : "系统默认")
                        : "  模式=Win+H 快捷键"));
                if (asrMode && !MimoAsr.Ready)
                {
                    Log.Write("未配置 MIMO_BASE_URL / MIMO_API_KEY，退回 Win+H 模式");
                    asrMode = false;
                }
                Application.Run(new TrayApp(keyboardMic, dump, startWithWindows, asrMode));
            }
        }
    }
}
