using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace HotkeyClipboardPrototype
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new HotkeyClipboardForm());
        }
    }

    internal sealed class HotkeyClipboardForm : Form
    {
        private const int HotkeyId = 0x5452;
        private const int WmHotkey = 0x0312;
        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;
        private const uint ModNoRepeat = 0x4000;
        private const int CopyDelayMilliseconds = 100;

        private readonly TextBox _capturedTextBox;
        private readonly Label _statusLabel;
        private readonly CheckBox _restoreClipboardCheckBox;
        private readonly NotifyIcon _notifyIcon;
        private readonly System.Windows.Forms.Timer _controlReleaseTimer;
        private readonly System.Windows.Forms.Timer _clipboardTimer;
        private bool _captureInProgress;
        private bool _captureQueued;
        private IDataObject _previousClipboard;
        private string _selectedText;
        private ClipboardCaptureStage _captureStage;

        public HotkeyClipboardForm()
        {
            Text = "Hotkey Clipboard Prototype";
            Width = 720;
            Height = 460;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(520, 340);

            var instructionsLabel = new Label();
            instructionsLabel.AutoSize = true;
            instructionsLabel.Text = "Select text in any app, then press Ctrl+Alt+T. This prototype sends Ctrl+C and reads Clipboard.GetText().";
            instructionsLabel.Left = 12;
            instructionsLabel.Top = 14;

            _restoreClipboardCheckBox = new CheckBox();
            _restoreClipboardCheckBox.AutoSize = true;
            _restoreClipboardCheckBox.Checked = true;
            _restoreClipboardCheckBox.Text = "Restore previous clipboard content after capture";
            _restoreClipboardCheckBox.Left = 12;
            _restoreClipboardCheckBox.Top = 42;

            var captureButton = new Button();
            captureButton.Text = "Test Capture Now";
            captureButton.Left = 12;
            captureButton.Top = 72;
            captureButton.Width = 130;
            captureButton.Click += delegate { BeginCaptureSelectedText(); };

            var exitButton = new Button();
            exitButton.Text = "Exit";
            exitButton.Left = 152;
            exitButton.Top = 72;
            exitButton.Width = 80;
            exitButton.Click += delegate { Close(); };

            _statusLabel = new Label();
            _statusLabel.AutoSize = false;
            _statusLabel.Left = 12;
            _statusLabel.Top = 112;
            _statusLabel.Width = 660;
            _statusLabel.Height = 24;
            _statusLabel.Text = "Registering global hotkey Ctrl+Alt+T...";

            _capturedTextBox = new TextBox();
            _capturedTextBox.Multiline = true;
            _capturedTextBox.ScrollBars = ScrollBars.Vertical;
            _capturedTextBox.ReadOnly = true;
            _capturedTextBox.Left = 12;
            _capturedTextBox.Top = 144;
            _capturedTextBox.Width = 680;
            _capturedTextBox.Height = 255;
            _capturedTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            Controls.Add(instructionsLabel);
            Controls.Add(_restoreClipboardCheckBox);
            Controls.Add(captureButton);
            Controls.Add(exitButton);
            Controls.Add(_statusLabel);
            Controls.Add(_capturedTextBox);

            Resize += OnResize;
            FormClosing += OnFormClosing;

            var menu = new ContextMenuStrip();
            menu.Items.Add("Show", null, delegate { ShowMainWindow(); });
            menu.Items.Add("Capture Now", null, delegate { BeginCaptureSelectedText(); });
            menu.Items.Add("Exit", null, delegate { Close(); });

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Text = "Hotkey Clipboard Prototype";
            _notifyIcon.Icon = SystemIcons.Information;
            _notifyIcon.Visible = true;
            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.DoubleClick += delegate { ShowMainWindow(); };

            _controlReleaseTimer = new System.Windows.Forms.Timer();
            _controlReleaseTimer.Interval = 10;
            _controlReleaseTimer.Tick += OnControlReleaseTimerTick;

            _clipboardTimer = new System.Windows.Forms.Timer();
            _clipboardTimer.Interval = CopyDelayMilliseconds;
            _clipboardTimer.Tick += OnClipboardTimerTick;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            if (!RegisterHotKey(Handle, HotkeyId, ModControl | ModAlt | ModNoRepeat, (uint)Keys.T))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to register Ctrl+Alt+T global hotkey.");
            }

            SetStatus("Global hotkey registered: Ctrl+Alt+T");
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            UnregisterHotKey(Handle, HotkeyId);
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId)
            {
                BeginCaptureSelectedText();
                return;
            }

            base.WndProc(ref m);
        }

        private void BeginCaptureSelectedText()
        {
            if (_captureInProgress)
            {
                _captureQueued = true;
                SetStatus("Capture queued.");
                return;
            }

            _captureInProgress = true;
            SetStatus("Hotkey triggered. Release Ctrl to capture...");
            _controlReleaseTimer.Start();
        }

        private void OnControlReleaseTimerTick(object sender, EventArgs e)
        {
            if (IsKeyPressed(NativeMethods.VK_CONTROL))
            {
                return;
            }

            _controlReleaseTimer.Stop();
            _captureStage = ClipboardCaptureStage.SavePreviousClipboard;
            ProcessClipboardCapture();
        }

        private void OnClipboardTimerTick(object sender, EventArgs e)
        {
            _clipboardTimer.Stop();
            ProcessClipboardCapture();
        }

        private void ProcessClipboardCapture()
        {
            try
            {
                if (_captureStage == ClipboardCaptureStage.SavePreviousClipboard)
                {
                    _previousClipboard = _restoreClipboardCheckBox.Checked
                        ? ClipboardRetry.GetDataObject()
                        : null;

                    SendCtrlC();
                    _captureStage = ClipboardCaptureStage.ReadCopiedText;
                    ScheduleClipboardStep(CopyDelayMilliseconds);
                    return;
                }

                if (_captureStage == ClipboardCaptureStage.ReadCopiedText)
                {
                    _selectedText = ClipboardRetry.GetText();
                    _captureStage = ClipboardCaptureStage.RestorePreviousClipboard;
                    ProcessClipboardCapture();
                    return;
                }

                if (_captureStage == ClipboardCaptureStage.RestorePreviousClipboard &&
                    _restoreClipboardCheckBox.Checked &&
                    _previousClipboard != null)
                {
                    ClipboardRetry.SetDataObject(_previousClipboard);
                }

                DisplayCaptureResult();
                CompleteCapture();
            }
            catch (Win32Exception ex)
            {
                SetStatus("Input simulation failed: " + ex.Message);
                CompleteCapture();
            }
            catch (ExternalException)
            {
                ScheduleClipboardStep(50);
            }
        }

        private void ScheduleClipboardStep(int intervalMilliseconds)
        {
            _clipboardTimer.Interval = intervalMilliseconds;
            _clipboardTimer.Start();
        }

        private void DisplayCaptureResult()
        {
            if (string.IsNullOrWhiteSpace(_selectedText))
            {
                _capturedTextBox.Text = string.Empty;
                SetStatus("No text detected. Select text in another app and press Ctrl+Alt+T again.");
                return;
            }

            _capturedTextBox.Text = _selectedText;
            SetStatus("Captured " + _selectedText.Length + " character(s).");
        }

        private void CompleteCapture()
        {
            _captureInProgress = false;
            _previousClipboard = null;
            _selectedText = null;
            _captureStage = ClipboardCaptureStage.None;
            if (!_captureQueued)
            {
                return;
            }

            _captureQueued = false;
            BeginCaptureSelectedText();
        }

        private static bool IsKeyPressed(int virtualKey)
        {
            return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        private static void SendCtrlC()
        {
            var inputs = new INPUT[4];

            inputs[0].type = NativeMethods.InputKeyboard;
            inputs[0].U.ki.wVk = NativeMethods.VK_CONTROL;

            inputs[1].type = NativeMethods.InputKeyboard;
            inputs[1].U.ki.wVk = NativeMethods.VK_C;

            inputs[2].type = NativeMethods.InputKeyboard;
            inputs[2].U.ki.wVk = NativeMethods.VK_C;
            inputs[2].U.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

            inputs[3].type = NativeMethods.InputKeyboard;
            inputs[3].U.ki.wVk = NativeMethods.VK_CONTROL;
            inputs[3].U.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

            uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            if (sent != inputs.Length)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SendInput failed while sending Ctrl+C.");
            }
        }

        private void ShowMainWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void OnResize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                _notifyIcon.ShowBalloonTip(1500, "Prototype running", "Select text and press Ctrl+Alt+T.", ToolTipIcon.Info);
            }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            _controlReleaseTimer.Stop();
            _controlReleaseTimer.Dispose();
            _clipboardTimer.Stop();
            _clipboardTimer.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        private void SetStatus(string message)
        {
            _statusLabel.Text = DateTime.Now.ToString("HH:mm:ss") + " - " + message;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
    }

    internal static class ClipboardRetry
    {
        public static IDataObject GetDataObject()
        {
            return Clipboard.GetDataObject();
        }

        public static string GetText()
        {
            return Clipboard.ContainsText(TextDataFormat.UnicodeText)
                ? Clipboard.GetText(TextDataFormat.UnicodeText)
                : string.Empty;
        }

        public static void SetDataObject(IDataObject dataObject)
        {
            Clipboard.SetDataObject(dataObject, true);
        }
    }

    internal enum ClipboardCaptureStage
    {
        None,
        SavePreviousClipboard,
        ReadCopiedText,
        RestorePreviousClipboard
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint type;
        public INPUTUNION U;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct INPUTUNION
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;

        [FieldOffset(0)]
        public KEYBDINPUT ki;

        [FieldOffset(0)]
        public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    internal static class NativeMethods
    {
        public const uint InputKeyboard = 1;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const ushort VK_CONTROL = 0x11;
        public const ushort VK_MENU = 0x12;
        public const ushort VK_C = 0x43;
    }
}
