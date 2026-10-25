using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace QingLi
{
    internal static class Program
    {
        public static Icon AppIcon;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [STAThread]
        private static void Main()
        {
            try
            {
                Run();
            }
            catch (Exception ex)
            {
                try
                {
                    string log = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "QingLi-crash.log");
                    System.IO.File.AppendAllText(log, DateTime.Now.ToString("s") + " " + ex + "\r\n");
                }
                catch { }
                MessageBox.Show("轻历启动失败：\n" + ex.Message, "轻历", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void Run()
        {
            bool created;
            using (Mutex mutex = new Mutex(true, "QingLi.Calendar.SingleInstance", out created))
            {
                if (!created)
                {
                    IntPtr h = FindWindow(null, "轻历 - 日程与提醒");
                    if (h != IntPtr.Zero)
                    {
                        ShowWindow(h, 9);
                        SetForegroundWindow(h);
                    }
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                try
                {
                    using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                        Theme.Scale = g.DpiX / 96f;
                }
                catch
                {
                    Theme.Scale = 1f;
                }

                try { AppIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
                catch { AppIcon = SystemIcons.Application; }

                Application.ThreadException += delegate (object s, ThreadExceptionEventArgs e)
                {
                    MessageBox.Show("程序遇到一个未处理的错误：\n" + e.Exception.Message, "轻历",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e)
                {
                    Exception ex = e.ExceptionObject as Exception;
                    if (ex != null)
                        MessageBox.Show("程序遇到一个未处理的错误：\n" + ex.Message, "轻历",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                };

                Application.AddMessageFilter(new WheelRouter());

                EventStore store = new EventStore();
                store.Load();

                HolidayService holidays = new HolidayService();
                holidays.Load();

                ReminderService reminders = new ReminderService(store);
                MainForm form = new MainForm(store, reminders, holidays);
                reminders.Start();
                try
                {
                    Application.Run(form);
                }
                finally
                {
                    reminders.Dispose();
                    store.Save();
                }
            }
        }
    }

    /// <summary>把滚轮消息转发给鼠标下方的周/日视图（即使它没有焦点）。</summary>
    internal class WheelRouter : IMessageFilter
    {
        private const int WM_MOUSEWHEEL = 0x020A;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT p);

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_MOUSEWHEEL) return false;
            Point p = Cursor.Position;
            POINT pt = new POINT();
            pt.X = p.X;
            pt.Y = p.Y;
            IntPtr h = WindowFromPoint(pt);
            if (h == IntPtr.Zero) return false;
            Control c = Control.FromChildHandle(h);
            TimeGridView grid = null;
            while (c != null)
            {
                grid = c as TimeGridView;
                if (grid != null) break;
                c = c.Parent;
            }
            if (grid == null) return false;
            int delta = (short)((long)m.WParam >> 16);
            grid.ScrollBy(-(delta / 120) * Theme.S(64));
            return true;
        }
    }
}
