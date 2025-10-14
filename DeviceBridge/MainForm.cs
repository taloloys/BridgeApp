using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Owin.Hosting;
using System.Threading.Tasks;



namespace DeviceBridge
{
    /// <summary>
    /// Main WinForms application window for Device Bridge
    /// Provides a user interface for managing fingerprint operations and system tray integration
    /// </summary>
    public partial class MainForm : Form
    {
        private NotifyIcon _trayIcon;
        private bool _isMinimizedToTray = false;

        private Task _webServerTask;
        private string _currentServerUrl = "";

        public MainForm()
        {
            try
            {
                InitializeComponent();
                this.Icon = new Icon("Images/Gemini_Generated_Image_jlm5pbjlm5pbjlm5.ico"); // Use correct relative path
                InitializeTrayIcon();
                InitializeWebServer();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing MainForm: {ex.Message}\n\nStack trace:\n{ex.StackTrace}", 
                    "MainForm Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }

        private void InitializeTrayIcon()
        {
            _trayIcon = new NotifyIcon()
            {
                Icon = new Icon("Images/Gemini_Generated_Image_jlm5pbjlm5pbjlm5.ico"), // Use correct relative path
                ContextMenuStrip = new ContextMenuStrip(),
                Visible = true,
                Text = "Device Bridge - Fingerprint Service"
            };


            var showMenuItem = new ToolStripMenuItem("Show Main Window", null, OnShowMainWindow);
            var exitMenuItem = new ToolStripMenuItem("Exit", null, OnExit);

            _trayIcon.ContextMenuStrip.Items.Add(showMenuItem);
            _trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _trayIcon.ContextMenuStrip.Items.Add(exitMenuItem);


            _trayIcon.DoubleClick += OnShowMainWindow;
        }

        private void InitializeWebServer()
        {
            try
            {

                _webServerTask = Task.Run(() =>
                {
                    var ports = new[] { 18420, 18421, 18422, 18423, 18424 };

                    foreach (var port in ports)
                    {
                        var url = $"http://127.0.0.1:{port}";
                        try
                        {
                            using (WebApp.Start<Startup>(url))
                            {
                                _currentServerUrl = url;
                                
                          
                                this.Invoke(new Action(() =>
                                {
                                    StatusLabel.Text = $"Web server running at {url}";
                                    StatusLabel.ForeColor = Color.Green;
                                    ServerUrlLabel.Text = url;
                                }));

                     
                                System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
                            }
                        }
                        catch (System.Net.HttpListenerException ex) when (ex.Message.Contains("conflicts with an existing registration"))
                        {
                            continue; // Try next port
                        }
                        catch (Exception ex)
                        {
                            this.Invoke(new Action(() =>
                            {
                                StatusLabel.Text = $"Failed to start on port {port}: {ex.Message}";
                                StatusLabel.ForeColor = Color.Red;
                            }));
                            if (port == ports[ports.Length - 1]) // Last port
                            {
                                return;
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Web server initialization failed: {ex.Message}";
                StatusLabel.ForeColor = Color.Red;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            
            // Show welcome message
            StatusLabel.Text = "Device Bridge starting...";
            StatusLabel.ForeColor = Color.Blue;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            
            // Minimize to tray when window is minimized
            if (WindowState == FormWindowState.Minimized && !_isMinimizedToTray)
            {
                HideToTray();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            
            if (e.CloseReason == CloseReason.UserClosing)
            {
                if (MessageBox.Show("Are you sure you want to exit Device Bridge?", 
                    "Exit Application", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                {
                    e.Cancel = true; 
                    return;
                }
            }
        }

        private void HideToTray()
        {
            _isMinimizedToTray = true;
            this.Hide();
            this.WindowState = FormWindowState.Normal; // Reset window state
            
            _trayIcon.ShowBalloonTip(3000, "Device Bridge", 
                "Application minimized to system tray. Right-click the tray icon to restore.", 
                ToolTipIcon.Info);
        }

        private void ShowFromTray()
        {
            _isMinimizedToTray = false;
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
            this.Activate();
        }

        private void OnShowMainWindow(object sender, EventArgs e)
        {
            ShowFromTray();
        }

        private void OnHideToTray(object sender, EventArgs e)
        {
            HideToTray();
        }

       

        private void OnExit(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to exit Device Bridge?", 
                "Exit Application", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                // Clean up and exit
                _trayIcon?.Dispose();
                Application.Exit();
            }
        }

        // Button event handlers
        private void MinimizeToTrayButton_Click(object sender, EventArgs e)
        {
            HideToTray();
        }

        private void TestFingerprintButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Fingerprint test functionality would be implemented here.", 
                "Test Fingerprint", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SettingsButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Settings dialog would be implemented here.", 
                "Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void AboutButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Device Bridge v1.0\n\nFingerprint Device Service\n\n" +
                "This application provides a bridge between fingerprint devices and web applications.", 
                "About Device Bridge", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OpenWebInterfaceButton_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentServerUrl))
            {
                System.Diagnostics.Process.Start(_currentServerUrl);
            }
            else
            {
                MessageBox.Show("Web server is not running.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
