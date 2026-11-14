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
using System.Runtime.InteropServices;
using System.Threading;
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

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_SetOutputReport(IntPtr device, byte[] buffer, int length);

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
        public static extern bool CloseHandle(IntPtr handle);

        public const uint DigcfPresent = 0x00000002;
        public const uint DigcfDeviceInterface = 0x00000010;
        public const uint GenericRead = 0x80000000;
        public const uint GenericWrite = 0x40000000;
        public const uint FileShareRead = 0x00000001;
        public const uint FileShareWrite = 0x00000002;
        public const uint OpenExisting = 3;
        public static readonly IntPtr InvalidHandle = new IntPtr(-1);
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

        private readonly Thread thread;
        private readonly object queueGate = new object();
        private readonly Queue<bool> pending = new Queue<bool>();
        private readonly bool keyboardMic;
        private readonly bool dump;

        private volatile bool stopping;
        private volatile bool connected;
        private IntPtr device = Native.InvalidHandle;

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
                ReadLoop(handle);
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
            byte[] buffer = new byte[64];
            int read;
            bool held = false;
            while (!stopping)
            {
                if (!Native.ReadFile(handle, buffer, buffer.Length, out read, IntPtr.Zero)) break;
                if (read < 3) continue;
                if (dump) Log.Write("report " + Hex(buffer, read));

                // 报告形如 01 B0 <state> ...：01 是 Report ID，B0 是语音键通道。
                if (buffer[0] != 0x01 || buffer[1] != 0xB0) continue;

                byte state = buffer[2];
                if (state != 0)
                {
                    if (held) continue;            // B0 03 是按住时的重复帧
                    held = true;
                    Log.Write("语音键按下 (state=" + state + ")");
                    if (keyboardMic) SetKeyboardMic(true);
                    Enqueue(true);
                }
                else if (held)
                {
                    held = false;
                    Log.Write("语音键松开");
                    Enqueue(false);
                    if (keyboardMic) SetKeyboardMic(false);
                }
            }
        }

        /// <summary>米物麦克风模式：按下时让接收器打开键盘麦克风，松手关闭。</summary>
        private void SetKeyboardMic(bool open)
        {
            IntPtr handle = device;
            if (handle == Native.InvalidHandle) return;

            Native.HidpCaps caps = new Native.HidpCaps();
            IntPtr preparsed;
            if (!Native.HidD_GetPreparsedData(handle, out preparsed)) return;
            try
            {
                if (Native.HidP_GetCaps(preparsed, ref caps) < 0) return;
            }
            finally
            {
                Native.HidD_FreePreparsedData(preparsed);
            }

            // 与 macOS 版一致：Report ID 2，B2 01/00，128 字节报文体，末字节校验（只用于写入）。
            byte[] buffer = new byte[128];
            buffer[0] = 0x02;
            buffer[1] = 0xB2;
            buffer[2] = (byte)(open ? 1 : 0);
            buffer[127] = (byte)(open ? 0x3D : 0x55);
            bool ok = Native.HidD_SetOutputReport(handle, buffer, buffer.Length);
            Log.Write("麦克风 " + (open ? "开" : "关") + " -> " + (ok ? "成功" : "失败 " + Marshal.GetLastWin32Error()));
        }

        private static IntPtr OpenVendorCollection()
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

        private static string Hex(byte[] data, int length)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(length * 3);
            for (int i = 0; i < length; i++) sb.Append(data[i].ToString("x2")).Append(' ');
            return sb.ToString().TrimEnd();
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
        private bool dictationOn;

        public TrayApp(bool keyboardMic, bool dump, bool startWithWindows)
        {
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
            Log.Write("启动（麦克风=" + (keyboardMic ? "键盘自带" : "系统默认") + "，松手=" +
                      (KeySender.StopByToggle ? "再按 Win+H" : "Esc") + "）");
        }

        private void OnTick(object sender, EventArgs e)
        {
            statusItem.Text = watcher.Connected
                ? "语音键已就绪：按一下开始 / 再按一下结束"
                : "正在查找接收器…（插入米物接收器，键盘切到 2.4G 模式）";

            bool down;
            while (watcher.TryDequeue(out down))
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

            foreach (string arg in args)
            {
                string a = arg.ToLowerInvariant();
                if (a == "--keyboard-mic") keyboardMic = true;                // 使用键盘自带麦克风
                else if (a == "--dump") dump = true;                          // 记录所有原始报告
                else if (a == "--no-toggle") KeySender.StopByToggle = false;   // 松手发 Esc
                else if (a == "--slow") KeySender.SlowCombo = true;            // 分步发送
                else if (a == "--no-input") KeySender.Enabled = false;         // 只记日志，不注入按键
                else if (a == "--autostart") startWithWindows = true;
            }

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
                Log.Write("==== 启动 ====");
                Application.Run(new TrayApp(keyboardMic, dump, startWithWindows));
            }
        }
    }
}
