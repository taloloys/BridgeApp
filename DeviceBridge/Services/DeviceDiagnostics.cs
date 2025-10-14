using System;
using System.Collections.Generic;
using System.Management;
using System.Security.Principal;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using DPFP.Capture;

namespace DeviceBridge.Services
{
    /// <summary>
    /// Comprehensive device diagnostics and troubleshooting for fingerprint readers
    /// </summary>
    public static class DeviceDiagnostics
    {
        public class DiagnosticResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public List<string> Details { get; set; } = new List<string>();
            public List<string> Recommendations { get; set; } = new List<string>();
        }

        public static DiagnosticResult RunFullDiagnostics()
        {
            var result = new DiagnosticResult();
            result.Details.Add($"Diagnostic run at: {DateTime.Now}");
            result.Details.Add($"Running as: {WindowsIdentity.GetCurrent().Name}");
            result.Details.Add($"Is Admin: {IsRunningAsAdministrator()}");

            // Check 1: Administrator privileges
            if (!IsRunningAsAdministrator())
            {
                result.Success = false;
                result.Message = "Application is not running with administrator privileges";
                result.Recommendations.Add("Run the application as administrator");
                result.Recommendations.Add("Check app.manifest has requireAdministrator set to true");
                return result;
            }

            // Check 2: Device detection via WMI
            var wmiResult = CheckDeviceViaWMI();
            result.Details.AddRange(wmiResult.Details);
            if (!wmiResult.Success)
            {
                result.Recommendations.AddRange(wmiResult.Recommendations);
            }

            // Check 3: DPFP SDK initialization
            var sdkResult = CheckDPFPSDK();
            result.Details.AddRange(sdkResult.Details);
            if (!sdkResult.Success)
            {
                result.Recommendations.AddRange(sdkResult.Recommendations);
            }

            // Check 4: Device drivers
            var driverResult = CheckDeviceDrivers();
            result.Details.AddRange(driverResult.Details);
            if (!driverResult.Success)
            {
                result.Recommendations.AddRange(driverResult.Recommendations);
            }

            // Check 5: Registry entries
            var registryResult = CheckRegistryEntries();
            result.Details.AddRange(registryResult.Details);
            if (!registryResult.Success)
            {
                result.Recommendations.AddRange(registryResult.Recommendations);
            }

            // Check 6: Process conflicts
            var processResult = CheckProcessConflicts();
            result.Details.AddRange(processResult.Details);
            if (!processResult.Success)
            {
                result.Recommendations.AddRange(processResult.Recommendations);
            }

            // Overall assessment
            if (wmiResult.Success && sdkResult.Success && driverResult.Success)
            {
                result.Success = true;
                result.Message = "All diagnostics passed - device should be working";
            }
            else
            {
                result.Success = false;
                result.Message = "One or more diagnostic checks failed";
            }

            return result;
        }

        private static bool IsRunningAsAdministrator()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private static DiagnosticResult CheckDeviceViaWMI()
        {
            var result = new DiagnosticResult();
            result.Details.Add("=== WMI Device Detection ===");

            try
            {
                var queries = new[]
                {
                    "SELECT * FROM Win32_PnPEntity WHERE PNPClass='Biometric'",
                    "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%Fingerprint%'",
                    "SELECT * FROM Win32_PnPEntity WHERE Manufacturer LIKE '%DigitalPersona%'",
                    "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%U.are.U%'",
                    "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%Upek%'",
                    "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%AuthenTec%'"
                };

                bool deviceFound = false;
                foreach (var query in queries)
                {
                    try
                    {
                        var searcher = new ManagementObjectSearcher(query);
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            var name = Convert.ToString(obj["Name"]) ?? "Unknown";
                            var manufacturer = Convert.ToString(obj["Manufacturer"]) ?? "Unknown";
                            var deviceId = Convert.ToString(obj["DeviceID"]) ?? "Unknown";
                            var status = Convert.ToString(obj["Status"]) ?? "Unknown";
                            
                            result.Details.Add($"Found: {name} (Manufacturer: {manufacturer}, Status: {status})");
                            result.Details.Add($"Device ID: {deviceId}");
                            deviceFound = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Details.Add($"WMI Query failed: {query} - {ex.Message}");
                    }
                }

                if (deviceFound)
                {
                    result.Success = true;
                    result.Message = "Fingerprint device detected via WMI";
                }
                else
                {
                    result.Success = false;
                    result.Message = "No fingerprint device detected via WMI";
                    result.Recommendations.Add("Check if fingerprint reader is properly connected");
                    result.Recommendations.Add("Verify device drivers are installed");
                    result.Recommendations.Add("Try reconnecting the USB device");
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"WMI check failed: {ex.Message}";
                result.Recommendations.Add("WMI service might be disabled or corrupted");
            }

            return result;
        }

        private static DiagnosticResult CheckDPFPSDK()
        {
            var result = new DiagnosticResult();
            result.Details.Add("=== DPFP SDK Check ===");

            try
            {
                // Test ReadersCollection
                var readers = new ReadersCollection();
                result.Details.Add($"ReadersCollection count: {readers.Count}");
                
                if (readers.Count > 0)
                {
                    for (int i = 0; i < readers.Count; i++)
                    {
                        var reader = readers[i];
                        result.Details.Add($"Reader {i}: Serial={reader.SerialNumber}");
                    }
                    result.Success = true;
                    result.Message = "DPFP SDK can detect readers";
                }
                else
                {
                    result.Success = false;
                    result.Message = "DPFP SDK cannot detect any readers";
                    result.Recommendations.Add("DPFP SDK might not be properly installed");
                    result.Recommendations.Add("Device drivers might be missing or incompatible");
                }

                // Test Capture object creation
                try
                {
                    using (var capture = new Capture())
                    {
                        result.Details.Add("Capture object created successfully");
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = $"Capture object creation failed: {ex.Message}";
                    result.Recommendations.Add("DPFP SDK initialization failed");
                    result.Recommendations.Add("Check if all DPFP DLLs are present and compatible");
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"DPFP SDK check failed: {ex.Message}";
                result.Recommendations.Add("DPFP SDK might not be installed");
                result.Recommendations.Add("Check if DPFP DLLs are in the application directory");
            }

            return result;
        }

        private static DiagnosticResult CheckDeviceDrivers()
        {
            var result = new DiagnosticResult();
            result.Details.Add("=== Device Driver Check ===");

            try
            {
                var query = "SELECT * FROM Win32_PnPEntity WHERE (PNPClass='Biometric') OR (Name LIKE '%Fingerprint%') OR (Manufacturer LIKE '%DigitalPersona%')";
                var searcher = new ManagementObjectSearcher(query);
                
                bool driverIssueFound = false;
                foreach (ManagementObject obj in searcher.Get())
                {
                    var name = Convert.ToString(obj["Name"]) ?? "Unknown";
                    var status = Convert.ToString(obj["Status"]) ?? "Unknown";
                    var deviceId = Convert.ToString(obj["DeviceID"]) ?? "Unknown";
                    
                    result.Details.Add($"Device: {name}");
                    result.Details.Add($"Status: {status}");
                    result.Details.Add($"Device ID: {deviceId}");

                    if (status != "OK")
                    {
                        driverIssueFound = true;
                        result.Details.Add($"WARNING: Device status is not OK: {status}");
                        result.Recommendations.Add($"Device driver issue detected: {status}");
                        result.Recommendations.Add("Try updating or reinstalling device drivers");
                    }
                }

                if (!driverIssueFound)
                {
                    result.Success = true;
                    result.Message = "Device drivers appear to be working";
                }
                else
                {
                    result.Success = false;
                    result.Message = "Device driver issues detected";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Driver check failed: {ex.Message}";
                result.Recommendations.Add("Unable to check device drivers");
            }

            return result;
        }

        private static DiagnosticResult CheckRegistryEntries()
        {
            var result = new DiagnosticResult();
            result.Details.Add("=== Registry Check ===");

            try
            {
                // Check for Digital Persona registry entries
                var registryPaths = new[]
                {
                    @"SOFTWARE\DigitalPersona",
                    @"SOFTWARE\WOW6432Node\DigitalPersona",
                    @"SYSTEM\CurrentControlSet\Services\DPFP",
                    @"SYSTEM\CurrentControlSet\Services\DPFPCtlX"
                };

                bool foundEntries = false;
                foreach (var path in registryPaths)
                {
                    try
                    {
                        using (var key = Registry.LocalMachine.OpenSubKey(path))
                        {
                            if (key != null)
                            {
                                result.Details.Add($"Found registry key: {path}");
                                foundEntries = true;
                                
                                // List subkeys
                                var subKeys = key.GetSubKeyNames();
                                foreach (var subKey in subKeys)
                                {
                                    result.Details.Add($"  Subkey: {subKey}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Details.Add($"Registry access failed for {path}: {ex.Message}");
                    }
                }

                if (foundEntries)
                {
                    result.Success = true;
                    result.Message = "Digital Persona registry entries found";
                }
                else
                {
                    result.Success = false;
                    result.Message = "No Digital Persona registry entries found";
                    result.Recommendations.Add("Digital Persona SDK might not be properly installed");
                    result.Recommendations.Add("Try reinstalling the Digital Persona SDK");
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Registry check failed: {ex.Message}";
            }

            return result;
        }

        private static DiagnosticResult CheckProcessConflicts()
        {
            var result = new DiagnosticResult();
            result.Details.Add("=== Process Conflict Check ===");

            try
            {
                var processes = Process.GetProcesses();
                var conflictingProcesses = new List<string>();

                foreach (var process in processes)
                {
                    try
                    {
                        var processName = process.ProcessName.ToLower();
                        if (processName.Contains("fingerprint") || 
                            processName.Contains("dpfp") || 
                            processName.Contains("digitalpersona") ||
                            processName.Contains("biometric"))
                        {
                            conflictingProcesses.Add($"{process.ProcessName} (PID: {process.Id})");
                        }
                    }
                    catch
                    {
                        // Ignore processes we can't access
                    }
                }

                if (conflictingProcesses.Count > 0)
                {
                    result.Success = false;
                    result.Message = "Potential conflicting processes detected";
                    result.Details.AddRange(conflictingProcesses);
                    result.Recommendations.Add("Close other fingerprint-related applications");
                    result.Recommendations.Add("Check if another application is using the fingerprint reader");
                }
                else
                {
                    result.Success = true;
                    result.Message = "No conflicting processes detected";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Process check failed: {ex.Message}";
            }

            return result;
        }

        public static string GenerateTroubleshootingReport()
        {
            var diagnostics = RunFullDiagnostics();
            var report = new System.Text.StringBuilder();
            
            report.AppendLine("=== DEVICE BRIDGE DIAGNOSTIC REPORT ===");
            report.AppendLine($"Generated: {DateTime.Now}");
            report.AppendLine($"Overall Status: {(diagnostics.Success ? "PASS" : "FAIL")}");
            report.AppendLine($"Message: {diagnostics.Message}");
            report.AppendLine();
            
            report.AppendLine("=== DETAILS ===");
            foreach (var detail in diagnostics.Details)
            {
                report.AppendLine(detail);
            }
            
            if (diagnostics.Recommendations.Count > 0)
            {
                report.AppendLine();
                report.AppendLine("=== RECOMMENDATIONS ===");
                foreach (var recommendation in diagnostics.Recommendations)
                {
                    report.AppendLine($"• {recommendation}");
                }
            }
            
            return report.ToString();
        }
    }
}
