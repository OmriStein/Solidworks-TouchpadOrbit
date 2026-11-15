// Touchpad Orbit - SOLIDWORKS add-in for touchpad navigation
// https://github.com/OmriStein/Solidworks-TouchpadOrbit   (MIT License)
//  * Two-finger tap and hold, then drag -> rotate (like middle-mouse drag)
//    (a plain two-finger tap still right-clicks, just ~0.3 s later)
//  * Two-finger scroll / pinch -> zoom toward the cursor
// Settings: HKCU\Software\TouchpadOrbit (read when SolidWorks starts)
// Log:      %LOCALAPPDATA%\TouchpadOrbit\log.txt
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swpublished;

[assembly: AssemblyTitle("Touchpad Orbit for SOLIDWORKS")]
[assembly: AssemblyProduct("Touchpad Orbit")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Omri Stein - MIT License")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace TouchpadOrbit
{
    [ComVisible(true), Guid("8C3E1F2A-6B4D-4E7F-9A15-2D3C4B5A6E71"), ProgId("TouchpadOrbit.Addin")]
    public class Addin : ISwAddin
    {
        const string Key = @"SOFTWARE\SolidWorks\AddIns\{8C3E1F2A-6B4D-4E7F-9A15-2D3C4B5A6E71}";
        const string StartKey = @"Software\SolidWorks\AddInsStartup\{8C3E1F2A-6B4D-4E7F-9A15-2D3C4B5A6E71}";
        SldWorks sw; Engine eng;

        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            sw = (SldWorks)ThisSW;
            try { eng = new Engine(sw); Log.W("add-in connected"); }
            catch (Exception e) { Log.W("connect failed: " + e); }
            return true;
        }

        public bool DisconnectFromSW()
        {
            try { if (eng != null) eng.Dispose(); } catch { }
            eng = null;
            if (sw != null) Marshal.ReleaseComObject(sw);
            sw = null; GC.Collect(); GC.WaitForPendingFinalizers();
            return true;
        }

        [ComRegisterFunction]
        public static void Register(Type t)
        {
            using (var k = Registry.LocalMachine.CreateSubKey(Key))
            {
                k.SetValue(null, 1, RegistryValueKind.DWord);
                k.SetValue("Title", "Touchpad Orbit");
                k.SetValue("Description", "Two-finger tap + drag rotates; touchpad zoom goes to the cursor");
            }
            using (var k = Registry.CurrentUser.CreateSubKey(StartKey))
                k.SetValue(null, 1, RegistryValueKind.DWord);
        }

        [ComUnregisterFunction]
        public static void Unregister(Type t)
        {
            try { Registry.LocalMachine.DeleteSubKeyTree(Key, false); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(StartKey, false); } catch { }
        }
    }

    static class Log
    {
        public static bool On = true;
        static readonly string path = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "TouchpadOrbit", "log.txt");
        public static void W(string s)
        {
            if (!On) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var fi = new FileInfo(path);
                if (fi.Exists && fi.Length > 2000000) fi.Delete();
                File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss.fff") + " " + s + "\r\n");
            }
            catch { }
        }
    }

    class Cfg
    {
        public int TapMs = 250;           // max length of the first two-finger tap
        public int GapMs = 350;           // max gap between the tap and putting fingers down again
        public double MovePct = 3;        // max movement during the tap (% of pad width)
        public double RotSpeed = 1.0;     // 1.0 = 1.5*PI radians per full pad width
        public bool InvertX, InvertY;
        public bool FixZoom = true;
        public double ZoomSpeed = 1.0;
        public bool InvertZoom;
        public bool Debug = true;
        public static Cfg Load()
        {
            var c = new Cfg();
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(@"Software\TouchpadOrbit"))
                {
                    c.TapMs = I(k, "TapMs", c.TapMs);
                    c.GapMs = I(k, "GapMs", c.GapMs);
                    c.MovePct = D(k, "MovePercent", c.MovePct);
                    c.RotSpeed = D(k, "RotateSpeed", c.RotSpeed);
                    c.InvertX = I(k, "InvertHorizontal", 0) != 0;
                    c.InvertY = I(k, "InvertVertical", 0) != 0;
                    c.FixZoom = I(k, "ZoomToCursor", 1) != 0;
                    c.ZoomSpeed = D(k, "ZoomSpeed", c.ZoomSpeed);
                    c.InvertZoom = I(k, "InvertZoom", 0) != 0;
                    c.Debug = I(k, "Debug", 1) != 0;
                }
            }
            catch { }
            return c;
        }
        static int I(RegistryKey k, string n, int d)
        {
            var v = k.GetValue(n); if (v == null) { k.SetValue(n, d, RegistryValueKind.DWord); return d; }
            try { return Convert.ToInt32(v); } catch { return d; }
        }
        static double D(RegistryKey k, string n, double d)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var v = k.GetValue(n); if (v == null) { k.SetValue(n, d.ToString(inv)); return d; }
            double r; return double.TryParse(v.ToString(), System.Globalization.NumberStyles.Float, inv, out r) ? r : d;
        }
    }

    class Dev
    {
        public IntPtr PP; public ushort CcLink; public bool HasCc;
        public List<ushort> Fingers = new List<ushort>();
        public HashSet<ushort> HasId = new HashSet<ushort>();
        public double XRange = 1000;
        public int Expected, Seen;
        public Dictionary<uint, double[]> Frame = new Dictionary<uint, double[]>();
    }

    // Runs on SolidWorks' UI thread: raw touchpad input + thread mouse hook + direct API calls.
    class Engine : NativeWindow, IDisposable
    {
        enum Mode { Idle, Touch1, TapWait, Rotate, Passive }

        readonly SldWorks sw; readonly Cfg cfg;
        readonly Timer timer = new Timer();
        readonly Dictionary<IntPtr, Dev> devs = new Dictionary<IntPtr, Dev>();
        readonly Stopwatch clock = Stopwatch.StartNew();
        IntPtr hook; HookProc hookProc;
        bool reverseWheel;

        Mode mode = Mode.Idle; int fingers, maxFingers;
        double cx, cy, sx, sy, lx, ly, accX, accY;
        long t0, tapEnd, lastTwoFinger = -100000, rotateEnd = -100000;
        bool pendingRClick; long pendingRClickT;
        int zoomAcc; bool zoomCtrl, zoomPosted; int zoomX, zoomY;
        double kUnits;

        static readonly IntPtr Marker = (IntPtr)0x70A0B17;
        const int WM_INPUT = 0x00FF, WM_MOUSEWHEEL = 0x020A, WM_MOUSEHWHEEL = 0x020E;
        const int WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205, WM_APP_ZOOM = 0x8000 + 77;
        const int HIDP_OK = 0x00110000;

        public Engine(SldWorks sw)
        {
            this.sw = sw;
            cfg = Cfg.Load(); Log.On = cfg.Debug;
            try
            {
                int year = 2000 + int.Parse(sw.RevisionNumber().Split('.')[0]) - 8;
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\SolidWorks\SOLIDWORKS " + year + @"\General"))
                    if (k != null) reverseWheel = Convert.ToInt32(k.GetValue("Reverse Zoom Direction", 0)) != 0;
            }
            catch { }

            CreateHandle(new CreateParams { Caption = "TouchpadOrbitSink" });
            var rid = new[] { new RAWINPUTDEVICE { usUsagePage = 0x0D, usUsage = 0x05, dwFlags = 0x100, hwndTarget = Handle } };
            if (!RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
                Log.W("RegisterRawInputDevices failed " + Marshal.GetLastWin32Error());
            hookProc = HookCb;
            hook = SetWindowsHookEx(7 /*WH_MOUSE*/, hookProc, IntPtr.Zero, GetCurrentThreadId());
            if (hook == IntPtr.Zero) Log.W("hook failed " + Marshal.GetLastWin32Error());
            timer.Interval = 10; timer.Tick += (s, e) => Tick(); timer.Start();
            Log.W("engine up, reverseWheel=" + reverseWheel);
        }

        public void Dispose()
        {
            timer.Stop(); timer.Dispose();
            if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook); hook = IntPtr.Zero;
            var rid = new[] { new RAWINPUTDEVICE { usUsagePage = 0x0D, usUsage = 0x05, dwFlags = 0x1, hwndTarget = IntPtr.Zero } };
            RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
            foreach (var d in devs.Values) if (d != null && d.PP != IntPtr.Zero) Marshal.FreeHGlobal(d.PP);
            devs.Clear();
            DestroyHandle();
        }

        long Now { get { return clock.ElapsedMilliseconds; } }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_INPUT) { try { OnInput(m.LParam); } catch (Exception e) { Log.W("input err " + e.Message); } }
            else if (m.Msg == WM_APP_ZOOM) { zoomPosted = false; try { DoZoom(); } catch (Exception e) { Log.W("zoom err " + e.Message); } return; }
            base.WndProc(ref m);
        }

        // ---------------- raw touchpad input ----------------
        void OnInput(IntPtr hRaw)
        {
            uint hdr = (uint)(8 + 2 * IntPtr.Size), size = 0;
            GetRawInputData(hRaw, 0x10000003, IntPtr.Zero, ref size, hdr);
            if (size == 0) return;
            IntPtr buf = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetRawInputData(hRaw, 0x10000003, buf, ref size, hdr) != size) return;
                if (Marshal.ReadInt32(buf, 0) != 2) return;
                Dev d = GetDev(Marshal.ReadIntPtr(buf, 8)); if (d == null) return;
                int sizeHid = Marshal.ReadInt32(buf, (int)hdr), count = Marshal.ReadInt32(buf, (int)hdr + 4);
                var rep = new byte[sizeHid];
                for (int i = 0; i < count; i++)
                {
                    Marshal.Copy(buf + (int)hdr + 8 + i * sizeHid, rep, 0, sizeHid);
                    ParseReport(d, rep);
                }
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        Dev GetDev(IntPtr h)
        {
            Dev d;
            if (devs.TryGetValue(h, out d)) return d;
            uint sz = 0;
            GetRawInputDeviceInfo(h, 0x20000005, IntPtr.Zero, ref sz);
            if (sz == 0) { devs[h] = null; return null; }
            IntPtr pp = Marshal.AllocHGlobal((int)sz);
            GetRawInputDeviceInfo(h, 0x20000005, pp, ref sz);
            var caps = new byte[64];
            if (HidP_GetCaps(pp, caps) != HIDP_OK) { Marshal.FreeHGlobal(pp); devs[h] = null; return null; }
            ushort nv = BitConverter.ToUInt16(caps, 48);
            var vc = new byte[72 * Math.Max(1, (int)nv)];
            if (HidP_GetValueCaps(0, vc, ref nv, pp) != HIDP_OK) { Marshal.FreeHGlobal(pp); devs[h] = null; return null; }
            d = new Dev { PP = pp };
            for (int i = 0; i < nv; i++)
            {
                int o = i * 72;
                ushort page = BitConverter.ToUInt16(vc, o), link = BitConverter.ToUInt16(vc, o + 6), usage = BitConverter.ToUInt16(vc, o + 56);
                int lmin = BitConverter.ToInt32(vc, o + 40), lmax = BitConverter.ToInt32(vc, o + 44);
                if (page == 0x01 && usage == 0x30) { if (!d.Fingers.Contains(link)) d.Fingers.Add(link); if (lmax > lmin) d.XRange = lmax - lmin; }
                if (page == 0x0D && usage == 0x54) { d.CcLink = link; d.HasCc = true; }
                if (page == 0x0D && usage == 0x51) d.HasId.Add(link);
            }
            Log.W("touchpad: fingers/report=" + d.Fingers.Count + " xrange=" + d.XRange + " hasCC=" + d.HasCc);
            if (d.Fingers.Count == 0) { Marshal.FreeHGlobal(pp); d = null; }
            devs[h] = d;
            return d;
        }

        void ParseReport(Dev d, byte[] rep)
        {
            uint len = (uint)rep.Length, cc = 0;
            if (d.HasCc && HidP_GetUsageValue(0, 0x0D, d.CcLink, 0x54, out cc, d.PP, rep, len) != HIDP_OK) cc = 0;
            if (cc > 0) { d.Expected = (int)cc; d.Seen = 0; d.Frame.Clear(); }
            else if (d.Expected == 0) return;
            var usages = new ushort[32];
            foreach (var link in d.Fingers)
            {
                if (d.Seen >= d.Expected) break;
                uint x, y, id = link;
                if (HidP_GetUsageValue(0, 0x01, link, 0x30, out x, d.PP, rep, len) != HIDP_OK) continue;
                HidP_GetUsageValue(0, 0x01, link, 0x31, out y, d.PP, rep, len);
                if (d.HasId.Contains(link)) HidP_GetUsageValue(0, 0x0D, link, 0x51, out id, d.PP, rep, len);
                uint n = (uint)usages.Length; bool tip = false;
                if (HidP_GetUsages(0, 0x0D, link, usages, ref n, d.PP, rep, len) == HIDP_OK)
                    for (int i = 0; i < n; i++) if (usages[i] == 0x42) tip = true;
                d.Seen++;
                if (tip) d.Frame[id] = new[] { x / d.XRange, y / d.XRange };
            }
            if (d.Seen >= d.Expected) { d.Expected = 0; OnFrame(d.Frame); }
        }

        // ---------------- gesture: two-finger tap, then two-finger hold + drag ----------------
        void OnFrame(Dictionary<uint, double[]> pts)
        {
            int n = pts.Count; double x = 0, y = 0;
            foreach (var p in pts.Values) { x += p[0]; y += p[1]; }
            if (n > 0) { x /= n; y /= n; }
            if (n != fingers) Log.W("fingers " + fingers + "->" + n + " (" + mode + ")");
            fingers = n; cx = x; cy = y;
            if (n >= 2) lastTwoFinger = Now;

            switch (mode)
            {
                case Mode.Idle:
                    if (n == 2) { mode = Mode.Touch1; t0 = Now; sx = x; sy = y; maxFingers = 2; }
                    else if (n > 2) mode = Mode.Passive;
                    break;

                case Mode.Touch1:   // first two-finger contact: is it a short, still tap?
                    if (n > maxFingers) maxFingers = n;
                    if (maxFingers > 2) { mode = Mode.Passive; break; }
                    if (n == 2 && Dist(x, y, sx, sy) * 100 > cfg.MovePct) { mode = Mode.Passive; break; }   // normal scroll
                    if (Now - t0 > cfg.TapMs) { mode = Mode.Passive; break; }                              // too long for a tap
                    if (n == 0) { mode = Mode.TapWait; tapEnd = Now; Log.W("tap"); }
                    break;

                case Mode.TapWait:  // waiting for the fingers to come back down
                    if (n == 2 && CursorOverView()) { mode = Mode.Rotate; lx = x; ly = y; accX = accY = 0; pendingRClick = false; Log.W("ROTATE start"); }
                    else if (n > 2) mode = Mode.Passive;
                    break;

                case Mode.Rotate:
                    if (n != 2) { Flush(); rotateEnd = Now; Log.W("ROTATE end"); mode = n == 0 ? Mode.Idle : Mode.Passive; }
                    else { accX += x - lx; accY += y - ly; lx = x; ly = y; }
                    break;

                case Mode.Passive:
                    if (n == 0) mode = Mode.Idle;
                    break;
            }
        }

        static double Dist(double a, double b, double c, double d) { return Math.Sqrt((a - c) * (a - c) + (b - d) * (b - d)); }

        void Tick()
        {
            try
            {
                if (mode == Mode.TapWait && Now - tapEnd > cfg.GapMs && fingers < 2) mode = Mode.Idle;
                if (mode == Mode.Touch1 && Now - t0 > cfg.TapMs && fingers >= 2) mode = Mode.Passive;
                if (mode == Mode.Rotate) Flush();
                // a two-finger tap's right-click was held back; release it if no drag followed
                if (pendingRClick && mode != Mode.TapWait && mode != Mode.Touch1 && mode != Mode.Rotate && Now - pendingRClickT > 30)
                {
                    pendingRClick = false;
                    if (Now - rotateEnd > 500) ReplayRightClick();
                }
                if (mode != Mode.Idle && fingers < 2 && Now - lastTwoFinger > 1500 && mode != Mode.TapWait) mode = Mode.Idle;
            }
            catch (Exception e) { Log.W("tick err " + e.Message); }
        }

        void Flush()
        {
            if (Math.Abs(accX) < 1e-6 && Math.Abs(accY) < 1e-6) return;
            var v = ActiveView(); if (v == null) { accX = accY = 0; return; }
            double k = 1.5 * Math.PI * cfg.RotSpeed;
            double ax = accY * k * (cfg.InvertY ? -1 : 1);   // vertical finger motion -> about screen X
            double ay = accX * k * (cfg.InvertX ? -1 : 1);   // horizontal finger motion -> about screen Y
            accX = accY = 0;
            v.RotateAboutCenter(ax, ay);
        }

        void ReplayRightClick()
        {
            Log.W("replay right-click");
            var inp = new INPUT[2];
            inp[0].type = 0; inp[0].mi.dwFlags = 0x0008; inp[0].mi.dwExtraInfo = Marker;   // RIGHTDOWN
            inp[1].type = 0; inp[1].mi.dwFlags = 0x0010; inp[1].mi.dwExtraInfo = Marker;   // RIGHTUP
            SendInput(2, inp, Marshal.SizeOf(typeof(INPUT)));
        }

        // ---------------- mouse hook on SolidWorks' UI thread ----------------
        IntPtr HookCb(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0)
            {
                int msg = (int)wParam;
                IntPtr extra = Marshal.ReadIntPtr(lParam, 24);
                if (extra == Marker) return CallNextHookEx(hook, code, wParam, lParam);

                if (msg == WM_RBUTTONDOWN || msg == WM_RBUTTONUP)
                {
                    bool padTap = mode == Mode.Touch1 || mode == Mode.TapWait || mode == Mode.Rotate || Now - lastTwoFinger < 200 || Now - rotateEnd < 300;
                    if (padTap)
                    {
                        if (mode != Mode.Rotate && Now - rotateEnd > 300 && msg == WM_RBUTTONUP) { pendingRClick = true; pendingRClickT = Now; }
                        return (IntPtr)1;    // hold it back until we know whether a drag follows
                    }
                }
                else if (msg == WM_MOUSEWHEEL || msg == WM_MOUSEHWHEEL)
                {
                    if (mode == Mode.Rotate || Now - rotateEnd < 400) return (IntPtr)1;   // no scrolling while rotating
                    bool fromPad = fingers >= 2 || Now - lastTwoFinger < 600;
                    if (cfg.FixZoom && fromPad && msg == WM_MOUSEWHEEL)
                    {
                        int px = Marshal.ReadInt32(lParam, 0), py = Marshal.ReadInt32(lParam, 4);
                        if (ViewAt(px, py) != null)
                        {
                            int delta = (short)((Marshal.ReadInt32(lParam, 32) >> 16) & 0xFFFF);
                            bool ctrl = (GetKeyState(0x11) & 0x8000) != 0;
                            if (ctrl != zoomCtrl) zoomAcc = 0;
                            zoomCtrl = ctrl; zoomAcc += delta; zoomX = px; zoomY = py;
                            if (!zoomPosted) { zoomPosted = true; PostMessage(Handle, WM_APP_ZOOM, IntPtr.Zero, IntPtr.Zero); }
                            return (IntPtr)1;
                        }
                    }
                }
            }
            return CallNextHookEx(hook, code, wParam, lParam);
        }

        // ---------------- zoom toward cursor ----------------
        void DoZoom()
        {
            int delta = zoomAcc; zoomAcc = 0;
            if (delta == 0) return;
            IntPtr h; var v = ViewAt(zoomX, zoomY, out h); if (v == null) return;
            double steps = delta / 120.0;
            bool zoomIn = zoomCtrl ? steps > 0 : (steps < 0) ^ reverseWheel;   // pinch: spread = in; scroll: SolidWorks wheel convention
            if (cfg.InvertZoom && !zoomCtrl) zoomIn = !zoomIn;
            double f = Math.Pow(1.0 + 0.15 * cfg.ZoomSpeed, Math.Abs(steps));
            ZoomAt(v, h, zoomX, zoomY, zoomIn ? f : 1 / f);
        }

        // View transform maps model -> client pixels: q = s*R*p + t (y down). Keep the point under the cursor fixed.
        void ZoomAt(ModelView v, IntPtr h, int sxp, int syp, double f)
        {
            var pt = new POINT { X = sxp, Y = syp }; ScreenToClient(h, ref pt);
            double qx = pt.X, qy = pt.Y;
            var a = (double[])((MathTransform)v.Transform).ArrayData;
            double s = a[12], tx = a[9], ty = a[10];
            if (kUnits <= 0) kUnits = s / v.Scale2;
            v.ZoomByFactor(f);
            var b = (double[])((MathTransform)v.Transform).ArrayData;
            double s2 = b[12], tx2 = b[9], ty2 = b[10];
            double dx = qx - (s2 / s * (qx - tx) + tx2), dy = qy - (s2 / s * (qy - ty) + ty2);
            if (Math.Abs(dx) < 0.5 && Math.Abs(dy) < 0.5) return;
            double ux = dx / kUnits, uy = -dy / kUnits;
            v.TranslateBy(ux, uy);
            if (Math.Abs(dx) > 5)
            {
                var c = (double[])((MathTransform)v.Transform).ArrayData;
                double k = (c[9] - tx2) / ux; if (k > 0) kUnits = 0.5 * kUnits + 0.5 * k;
            }
        }

        // ---------------- helpers ----------------
        ModelView ActiveView()
        {
            var d = sw.ActiveDoc as ModelDoc2; if (d == null) return null;
            int t = d.GetType(); if (t != 1 && t != 2) return null;   // parts & assemblies
            return d.ActiveView as ModelView;
        }
        bool CursorOverView() { POINT p; GetCursorPos(out p); return ViewAt(p.X, p.Y) != null; }
        ModelView ViewAt(int x, int y) { IntPtr h; return ViewAt(x, y, out h); }
        ModelView ViewAt(int x, int y, out IntPtr h)
        {
            h = IntPtr.Zero;
            var v = ActiveView(); if (v == null) return null;
            h = (IntPtr)v.GetViewHWnd();
            return WindowFromPoint(new POINT { X = x, Y = y }) == h ? v : null;
        }

        // ---------------- P/Invoke ----------------
        delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
        [StructLayout(LayoutKind.Sequential)] struct RAWINPUTDEVICE { public ushort usUsagePage, usUsage; public uint dwFlags; public IntPtr hwndTarget; }
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Explicit)] struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(8)] public MOUSEINPUT mi; }
        [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] d, uint n, uint cb);
        [DllImport("user32.dll")] static extern uint GetRawInputData(IntPtr h, uint cmd, IntPtr data, ref uint size, uint hdr);
        [DllImport("user32.dll")] static extern uint GetRawInputDeviceInfo(IntPtr h, uint cmd, IntPtr data, ref uint size);
        [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr pp, byte[] caps);
        [DllImport("hid.dll")] static extern int HidP_GetValueCaps(int type, byte[] caps, ref ushort len, IntPtr pp);
        [DllImport("hid.dll")] static extern int HidP_GetUsageValue(int type, ushort page, ushort link, ushort usage, out uint val, IntPtr pp, byte[] rep, uint len);
        [DllImport("hid.dll")] static extern int HidP_GetUsages(int type, ushort page, ushort link, ushort[] list, ref uint len, IntPtr pp, byte[] rep, uint replen);
        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int id, HookProc p, IntPtr mod, uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int c, IntPtr w, IntPtr l);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] static extern short GetKeyState(int vk);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] inputs, int cb);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
        [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr h, ref POINT p);
    }
}
