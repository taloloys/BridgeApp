using KIOSKS;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DPFP;
using DPFP.Capture;
using DPFP.Processing;

namespace KIOSKS
{
    public partial class FingerprintRegistrationPanel : UserControl, DPFP.Capture.EventHandler
    {
        private MainWindow _parentWindow;
        private int _registrationStep = 0;
        private int _currentTemplateIndex = 1;
        private Capture _capture;
        private Enrollment _enroller;
        private byte[] _fingerprintTemplateBytes;
        private List<Database.FingerprintTemplate> _fingerprintTemplates = new List<Database.FingerprintTemplate>();
        private bool _isDeviceConnected = false;
        private const int MAX_TEMPLATES = 2; // Changed from 3 to 2 templates

        public FingerprintRegistrationPanel()
        {
            InitializeComponent();
        }

        public void SetParentWindow(MainWindow parentWindow)
        {
            _parentWindow = parentWindow;
        }

        private string _employeePhotoPath;
        private byte[] _employeePhotoData;
        private string _employeePhotoContentType;

        // Method to set employee information from the previous registration step
        public void SetEmployeeInfo(string name, string department, string photoPath = null)
        {
            EmployeeInfoLabel.Content = $"Name: {name} | Department: {department}";
            _employeePhotoPath = photoPath;

            // Get photo BLOB data from RegisterEmployeePanel
            if (_parentWindow?.RegisterEmployeePanelControl != null)
            {
                var (photoData, contentType) = _parentWindow.RegisterEmployeePanelControl.GetSelectedPhotoData();
                if (photoData != null && photoData.Length > 0)
                {
                    _employeePhotoData = photoData;
                    _employeePhotoContentType = contentType;
                    
                    // Display image from BLOB data
                    var bitmap = KIOSKS.Services.ImageService.ConvertBytesToBitmapImage(photoData);
                    if (bitmap != null)
                    {
                        EmployeePhotoImage.Source = bitmap;
                    }
                    return;
                }
            }
            
            // Fallback to file path
            if (!string.IsNullOrEmpty(photoPath))
            {
                SetEmployeePhoto(photoPath);
            }
        }

        // Method to set employee photo
        private void SetEmployeePhoto(string imagePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(imagePath))
                {
                    var bitmap = new BitmapImage(new Uri(imagePath));
                    EmployeePhotoImage.Source = bitmap;
                }
            }
            catch
            {
                // If image loading fails, keep the default image
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Return to the employee registration panel
            if (_parentWindow != null)
            {
                _parentWindow.ShowRegisterEmployeePanel();
            }
        }

        private void StartRegistrationButton_Click(object sender, RoutedEventArgs e)
        {
            // Reset template collection and start with first template
            _fingerprintTemplates.Clear();
            _currentTemplateIndex = 1;
            
            // Update UI for first template
            UpdateUIForCurrentTemplate();
            
            // Disable the start button to prevent multiple clicks
            StartRegistrationButton.IsEnabled = false;
            StartRegistrationButton.Content = "Registration in Progress...";
            // TODO: Uncomment when UI controls areadded to XAML
            // NextTemplateButton.Visibility = Visibility.Collapsed;
            // SkipTemplateButton.Visibility = Visibility.Collapsed;
            // ProceedToRfidButton.Visibility = Visibility.Collapsed;

            // Start the fingerprint registration using the fingerprint device
            BeginFingerprintEnrollment();
        }

        private void NextTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTemplateIndex < MAX_TEMPLATES)
            {
                _currentTemplateIndex++;
                UpdateUIForCurrentTemplate();
                
                // Hide buttons and start next enrollment
                // TODO: Uncomment when UI controls areadded to XAML
                // NextTemplateButton.Visibility = Visibility.Collapsed;
                // SkipTemplateButton.Visibility = Visibility.Collapsed;
                // ProceedToRfidButton.Visibility = Visibility.Collapsed;
                
                BeginFingerprintEnrollment();
            }
        }

        private void SkipTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            // Skip remaining templates and proceed to RFID registration
            ProceedToRfidRegistration();
        }

        private void ProceedToRfidButton_Click(object sender, RoutedEventArgs e)
        {
            // Proceed to RFID registration with all collected templates
            ProceedToRfidRegistration();
        }

        private void UpdateUIForCurrentTemplate()
        {
            // Show current template progress
            ScannerStatusLabel.Content = $"Registering Fingerprint {_currentTemplateIndex} of {MAX_TEMPLATES}";
            
            string fingerName;
            switch (_currentTemplateIndex)
            {
                case 1:
                    fingerName = "Index Finger (Primary)";
                    break;
                case 2:
                    fingerName = "Thumb (Backup)";
                    break;
                default:
                    fingerName = $"Finger {_currentTemplateIndex}";
                    break;
            }
            
            System.Diagnostics.Debug.WriteLine($"Starting template {_currentTemplateIndex}: {fingerName}");
            RegistrationProgressBar.Value = 0;
            ScannerStatusLabel.Content = $"Place your {fingerName.ToLower()} on the scanner...";
        }

        private void BeginFingerprintEnrollment()
        {
            _fingerprintTemplateBytes = null;
            RegistrationProgressBar.Value = 0;
            ScannerStatusLabel.Content = "Initializing fingerprint reader...";
            
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== Starting fingerprint enrollment for template {_currentTemplateIndex} ===");
                
                _enroller = new Enrollment();
                _capture = new Capture();
                _capture.EventHandler = this;
                
                System.Diagnostics.Debug.WriteLine("Capture object created, starting capture...");
                _capture.StartCapture();
                _isDeviceConnected = true;
                
                ScannerStatusLabel.Content = "Place your finger on the scanner...";
                System.Diagnostics.Debug.WriteLine("Fingerprint capture started successfully");
            }
            catch (DPFP.Error.SDKException sdkEx)
            {
                System.Diagnostics.Debug.WriteLine($"DPFP SDK Exception: {sdkEx.Message}");
                MessageBox.Show($"Fingerprint SDK error: {sdkEx.Message}\n\nThis usually means:\n• Device is in use by another application\n• SDK driver issue\n• Device needs to be reconnected", 
                    "Fingerprint SDK Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ResetRegistrationState();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"General exception: {ex.Message}");
                MessageBox.Show($"Fingerprint device initialization failed: {ex.Message}", 
                    "Fingerprint Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ResetRegistrationState();
            }
        }

        // DPFP EventHandler implementations
        public void OnComplete(object Capture, string ReaderSerialNumber, Sample Sample)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== OnComplete START for template {_currentTemplateIndex} ===");
                System.Diagnostics.Debug.WriteLine($"Reader: {ReaderSerialNumber}");
                System.Diagnostics.Debug.WriteLine($"Sample: {Sample != null}");
                
                var features = ExtractFeatures(Sample, DataPurpose.Enrollment);
                System.Diagnostics.Debug.WriteLine($"Features extracted: {features != null}");
                
                if (features != null)
                {
                    _enroller.AddFeatures(features);
                    var needed = _enroller.FeaturesNeeded;
                    var status = _enroller.TemplateStatus;
                    
                    System.Diagnostics.Debug.WriteLine($"Features needed: {needed}");
                    System.Diagnostics.Debug.WriteLine($"Template status: {status}");
                    
                    Dispatcher.Invoke(() =>
                    {
                        var progress = Math.Min(100, (4 - needed) * 25);
                        RegistrationProgressBar.Value = progress;
                        ScannerStatusLabel.Content = needed > 0 ? $"Lift and place finger again... ({needed} more)" : "Processing...";
                        System.Diagnostics.Debug.WriteLine($"Progress: {progress}%");
                    });
                    
                    if (status == Enrollment.Status.Ready)
                    {
                        System.Diagnostics.Debug.WriteLine($"=== ENROLLMENT READY for template {_currentTemplateIndex} - Creating template ===");
                        try
                        {
                            byte[] templateBytes;
                            using (var ms = new System.IO.MemoryStream())
                            {
                                var template = _enroller.Template;
                                template.Serialize(ms);
                                _fingerprintTemplateBytes = ms.ToArray();
                                templateBytes = ms.ToArray();
                            }
                            
                            // Create fingerprint template object
                            string fingerPosition;
                            switch (_currentTemplateIndex)
                            {
                                case 1:
                                    fingerPosition = "Index Finger (Primary)";
                                    break;
                                case 2:
                                    fingerPosition = "Thumb (Backup)";
                                    break;
                                default:
                                    fingerPosition = $"Finger {_currentTemplateIndex}";
                                    break;
                            }
                            
                            var fingerprintTemplate = new Database.FingerprintTemplate
                            {
                                TemplateIndex = _currentTemplateIndex,
                                TemplateBytes = templateBytes,
                                FingerPosition = fingerPosition,
                                Quality = 85.0m // Default quality - could be calculated from enrollment
                            };
                            
                            _fingerprintTemplates.Add(fingerprintTemplate);
                            
                            System.Diagnostics.Debug.WriteLine($"Template {_currentTemplateIndex} created: {templateBytes.Length} bytes ({fingerPosition})");
                            
                            StopCaptureSafe();
                            Dispatcher.Invoke(() =>
                            {
                                RegistrationProgressBar.Value = 100;
                                ScannerStatusLabel.Content = $"Template {_currentTemplateIndex} completed!";
                                
                                // Update completed templates display
                                UpdateCompletedTemplatesDisplay();
                                
                                // Show options for next steps
                                ShowPostTemplateOptions();
                            });
                        }
                        catch (Exception templateEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Template creation failed: {templateEx.Message}");
                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"Error creating fingerprint template: {templateEx.Message}", 
                                    "Template Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                ResetRegistrationState();
                            });
                        }
                    }
                    else if (status == Enrollment.Status.Failed)
                    {
                        System.Diagnostics.Debug.WriteLine("=== ENROLLMENT FAILED ===");
                        StopCaptureSafe();
                        _enroller.Clear();
                        Dispatcher.Invoke(() =>
                        {
                            RegistrationProgressBar.Value = 0;
                            ResetRegistrationState();
                            ScannerStatusLabel.Content = "Enrollment failed. Please try again.";
                        });
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Feature extraction failed - poor sample quality");
                    Dispatcher.Invoke(() => { 
                        ScannerStatusLabel.Content = "Poor quality scan, please try again..."; 
                    });
                }
                
                System.Diagnostics.Debug.WriteLine($"=== OnComplete END ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"=== OnComplete EXCEPTION: {ex.Message} ===");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                StopCaptureSafe();
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Fingerprint processing error: {ex.Message}", 
                        "Fingerprint Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    ResetRegistrationState();
                });
            }
        }

        private void UpdateCompletedTemplatesDisplay()
        {
            var completedText = $"✓ Completed: {_fingerprintTemplates.Count} of {MAX_TEMPLATES} template(s)\n";
            foreach (var template in _fingerprintTemplates)
            {
                completedText += $"  • {template.FingerPosition}\n";
            }
            
            // Output to debug console and update status
            System.Diagnostics.Debug.WriteLine($"Template Progress: {completedText.TrimEnd()}");
            
            // Update the scanner status with progress
            if (_fingerprintTemplates.Count == 1)
            {
                ScannerStatusLabel.Content = "✓ Primary fingerprint registered! Ready for backup.";
                ShowStepPromptAndProceedToBackup();
            }
            else if (_fingerprintTemplates.Count == 2)
            {
                ScannerStatusLabel.Content = "✓ Both fingerprints registered successfully!";
            }
        }

        private void ShowStepPromptAndProceedToBackup()
        {
            try
            {
                if (StepPromptOverlay == null) return;

                StepPromptOverlay.Visibility = Visibility.Visible;
                int countdown = 2;
                StepPromptCountdown.Content = $"Continuing in {countdown}...";

                var timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(1);
                timer.Tick += (s, e) =>
                {
                    countdown--;
                    if (countdown <= 0)
                    {
                        timer.Stop();
                        StepPromptOverlay.Visibility = Visibility.Collapsed;
                        // Advance to next finger without altering existing flow
                        NextTemplateButton_Click(null, null);
                    }
                    else
                    {
                        StepPromptCountdown.Content = $"Continuing in {countdown}...";
                    }
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FingerprintRegistration] Prompt error: {ex.Message}");
                // Fallback to immediate proceed
                NextTemplateButton_Click(null, null);
            }
        }

        private void ShowPostTemplateOptions()
        {
            if (_currentTemplateIndex < MAX_TEMPLATES)
            {
                // Automatically proceed to next finger in succession (no popups)
                NextTemplateButton_Click(null, null);
            }
            else
            {
                // Both templates completed, proceed to RFID registration
                ScannerStatusLabel.Content = $"✓ Both fingerprints registered successfully! Proceeding to RFID registration...";
                System.Diagnostics.Debug.WriteLine($"✓ All {MAX_TEMPLATES} fingerprint templates registered successfully!\n\n• Index Finger (Primary)\n• Thumb (Backup)\n\nProceeding to RFID registration.");
                ProceedToRfidRegistration();
            }
        }

        private void ProceedToRfidRegistration()
        {
            if (_fingerprintTemplates.Count == 0)
            {
                MessageBox.Show("At least one fingerprint template is required!", 
                    "No Templates", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            System.Diagnostics.Debug.WriteLine($"Proceeding to RFID registration with {_fingerprintTemplates.Count} fingerprint templates");
            
            if (_parentWindow != null)
            {
                _parentWindow.ShowRfidRegistrationPanel();
            }
        }

        public void OnFingerGone(object Capture, string ReaderSerialNumber)
        {
            Dispatcher.Invoke(() => { /* intentionally blank for discretion */ });
        }

        public void OnFingerTouch(object Capture, string ReaderSerialNumber)
        {
            Dispatcher.Invoke(() => { /* intentionally blank for discretion */ });
        }

        public void OnReaderConnect(object Capture, string ReaderSerialNumber)
        {
            Dispatcher.Invoke(() => { ScannerStatusLabel.Content = "Reader connected. Place finger..."; });
        }

        public void OnReaderDisconnect(object Capture, string ReaderSerialNumber)
        {
            Dispatcher.Invoke(() => { ScannerStatusLabel.Content = "Reader disconnected."; });
        }

        public void OnSampleQuality(object Capture, string ReaderSerialNumber, DPFP.Capture.CaptureFeedback CaptureFeedback)
        {
            Dispatcher.Invoke(() => { /* optionally update status based on CaptureFeedback */ });
        }

        private FeatureSet ExtractFeatures(Sample sample, DataPurpose purpose)
        {
            var extractor = new FeatureExtraction();
            var feedback = CaptureFeedback.None;
            var features = new FeatureSet();
            extractor.CreateFeatureSet(sample, purpose, ref feedback, ref features);
            return feedback == CaptureFeedback.Good ? features : null;
        }

        private void StopCaptureSafe()
        {
            try { _capture?.StopCapture(); } catch { }
            try { if (_capture != null) _capture.EventHandler = null; } catch { }
        }

        // Method to get the employee photo path
        public string GetEmployeePhotoPath()
        {
            return _employeePhotoPath;
        }

        // Legacy method for backward compatibility
        public byte[] GetFingerprintBytes()
        {
            // Return the first template for backward compatibility
            return _fingerprintTemplates.Count > 0 ? _fingerprintTemplates[0].TemplateBytes : null;
        }

        // New method to get all fingerprint templates
        public List<Database.FingerprintTemplate> GetFingerprintTemplates()
        {
            return new List<Database.FingerprintTemplate>(_fingerprintTemplates);
        }

        // Method to reset the panel for new registrations
        public void ResetPanel()
        {
            _registrationStep = 0;
            _currentTemplateIndex = 1;
            _fingerprintTemplates.Clear();
            RegistrationProgressBar.Value = 0;
            ScannerStatusLabel.Content = "Ready to scan fingerprint...";
            StartRegistrationButton.IsEnabled = true;
            StartRegistrationButton.Content = "Start Registration";
            
            // Hide all optional buttons
            // TODO: Uncomment when UI controls are added to XAML
            // NextTemplateButton.Visibility = Visibility.Collapsed;
            // SkipTemplateButton.Visibility = Visibility.Collapsed;
            // ProceedToRfidButton.Visibility = Visibility.Collapsed;
            
            // Reset labels
            // TODO: Uncomment when UI controls are added to XAML
            // TemplateProgressLabel.Content = "Ready to register fingerprints";
            // FingerPositionLabel.Content = "Multiple fingerprints provide backup authentication";
            // CompletedTemplatesLabel.Content = "";

            // Reset employee photo path and image
            _employeePhotoPath = null;
            EmployeePhotoImage.Source = new BitmapImage(new Uri("/images/employee_attendance_upscaled.png", UriKind.Relative));

            StopCaptureSafe();
        }

        private void ResetRegistrationState()
        {
            StartRegistrationButton.IsEnabled = true;
            StartRegistrationButton.Content = "Start Registration";
            ScannerStatusLabel.Content = "Ready to scan fingerprint...";
            
            // Hide optional buttons
            // TODO: Uncomment when UI controls are added to XAML
            // NextTemplateButton.Visibility = Visibility.Collapsed;
            // SkipTemplateButton.Visibility = Visibility.Collapsed;
            // ProceedToRfidButton.Visibility = Visibility.Collapsed;
        }

        public (byte[] photoData, string contentType) GetEmployeePhotoData()
        {
            return (_employeePhotoData, _employeePhotoContentType);
        }
    }
}
