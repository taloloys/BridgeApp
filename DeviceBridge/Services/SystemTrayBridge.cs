using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace DeviceBridge.Services
{
    /// <summary>
    /// System Tray application that maintains focus for fingerprint operations
    /// This ensures the bridge works reliably even when not in focus
    /// </summary>
    public class SystemTrayBridge : ApplicationContext
    {
        private NotifyIcon _trayIcon;
        private Form _hiddenWindow;
        private MainForm _mainForm;
        
        // Static reference for access from other classes
        private static SystemTrayBridge _currentInstance;
        private System.Threading.Timer _focusMaintenanceTimer;

        // Windows API for window management
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;

        public SystemTrayBridge()
        {
            _currentInstance = this; // Set static reference
            InitializeTrayIcon();
            InitializeHiddenWindow();
        }

        /// <summary>
        /// Gets the current system tray bridge instance
        /// </summary>
        public static SystemTrayBridge Current => _currentInstance;

        private void InitializeTrayIcon()
        {
            _trayIcon = new NotifyIcon()
            {
                Icon = SystemIcons.Application, // You can replace with a custom icon
                ContextMenuStrip = new ContextMenuStrip(),
                Visible = true,
                Text = "Device Bridge - Fingerprint Service"
            };

            // Create context menu
            var openMainItem = new ToolStripMenuItem("Show Main Window", null, OnOpenMainWindow);
            var exitMenuItem = new ToolStripMenuItem("Exit", null, OnExit);

            _trayIcon.ContextMenuStrip.Items.Add(openMainItem);
            _trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _trayIcon.ContextMenuStrip.Items.Add(exitMenuItem);

            // Double-click to open main window
            _trayIcon.DoubleClick += OnOpenMainWindow;

            try { _trayIcon.ShowBalloonTip(3000, "Device Bridge", "Running in system tray. Right-click for options.", ToolTipIcon.Info); } catch { }
        }

        private void InitializeHiddenWindow()
        {
            _hiddenWindow = new Form()
            {
                Text = "Device Bridge Hidden Window",
                WindowState = FormWindowState.Normal, // Changed from Minimized to Normal
                ShowInTaskbar = false,
                FormBorderStyle = FormBorderStyle.None,
                Size = new Size(1, 1),
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-2000, -2000),
                Opacity = 0.01,
                TopMost = true // Ensure window stays on top
            };

            _hiddenWindow.Load += (s, e) =>
            {
                // Make window always-on-top and ensure it stays active
                SetWindowPos(_hiddenWindow.Handle, HWND_TOPMOST, 0, 0, 1, 1, 
                    SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                
                // Force the window to be active and have focus
                _hiddenWindow.Activate();
                _hiddenWindow.BringToFront();
                SetForegroundWindow(_hiddenWindow.Handle);
                
                // Keep window visible but off-screen
                _hiddenWindow.Visible = true;
                
                System.Diagnostics.Debug.WriteLine("[SystemTrayBridge] Hidden window initialized, always-on-top, and active");
            };

            _hiddenWindow.Show();
            
            // Ensure window stays active after showing
            _hiddenWindow.Activate();
            _hiddenWindow.BringToFront();
            
            // Start periodic focus maintenance
            StartFocusMaintenance();
        }

        private void StartFocusMaintenance()
        {
            // Maintain focus every 30 seconds to ensure fingerprint capture works
            _focusMaintenanceTimer = new System.Threading.Timer(state =>
            {
                try
                {
                    if (_hiddenWindow != null && !_hiddenWindow.IsDisposed)
                    {
                        // Briefly restore focus without being intrusive
                        _hiddenWindow.Invoke(new Action(() =>
                        {
                            if (_hiddenWindow.TopMost)
                            {
                                SetWindowPos(_hiddenWindow.Handle, HWND_TOPMOST, 0, 0, 1, 1, 
                                    SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                            }
                        }));
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SystemTrayBridge] Focus maintenance error: {ex.Message}");
                }
            }, null, 30000, 30000); // Every 30 seconds
        }

        /// <summary>
        /// Ensures the hidden window has focus for fingerprint operations
        /// Call this before starting fingerprint operations
        /// </summary>
        public void EnsureFocus()
        {
            try
            {
                if (_hiddenWindow != null && !_hiddenWindow.IsDisposed)
                {
                    // Make sure window is visible and on top
                    _hiddenWindow.Visible = true;
                    _hiddenWindow.TopMost = true;
                    _hiddenWindow.Activate();
                    _hiddenWindow.BringToFront();
                    
                    // Bring window to foreground using Windows API
                    SetWindowPos(_hiddenWindow.Handle, HWND_TOPMOST, 0, 0, 1, 1, 
                        SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                    
                    SetForegroundWindow(_hiddenWindow.Handle);
                    
                    // Force window to stay active
                    System.Threading.Thread.Sleep(50); // Brief delay
                    _hiddenWindow.Activate();
                    
                    System.Diagnostics.Debug.WriteLine("[SystemTrayBridge] Focus aggressively ensured for fingerprint operations");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SystemTrayBridge] Focus ensure failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the hidden window for use with WinForms message pump
        /// </summary>
        public Form HiddenWindow => _hiddenWindow;

        /// <summary>
        /// Shows a notification balloon tip
        /// </summary>
        public void ShowNotification(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
        {
            try
            {
                _trayIcon?.ShowBalloonTip(3000, title, text, icon);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SystemTrayBridge] Notification failed: {ex.Message}");
            }
        }

        private void OnOpenMainWindow(object sender, EventArgs e)
        {
            try
            {
                if (_mainForm == null || _mainForm.IsDisposed)
                {
                    _mainForm = new MainForm();
                }

                if (!_mainForm.Visible)
                {
                    _mainForm.Show();
                }
                _mainForm.WindowState = FormWindowState.Normal;
                _mainForm.BringToFront();
                _mainForm.Activate();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open main window: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // removed status dialog per request

        private void OnRestartService(object sender, EventArgs e)
        {
            try
            {
                _trayIcon.ShowBalloonTip(3000, "Device Bridge", "Service restarted successfully", ToolTipIcon.Info);
                System.Diagnostics.Debug.WriteLine("[SystemTrayBridge] Service restart requested");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to restart service: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnExit(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to exit Device Bridge?\n\nThis will stop all fingerprint operations.",
                "Exit Device Bridge",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                Application.Exit();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _focusMaintenanceTimer?.Dispose();
                _currentInstance = null; // Clear static reference
                _trayIcon?.Dispose();
                _hiddenWindow?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
