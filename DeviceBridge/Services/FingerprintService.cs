using System;
using System.Management;
using DPFP.Capture;

namespace DeviceBridge.Services
{
	public static class FingerprintService
	{
		public static bool TryDetectReader(out string modelName, out string error)
		{
			modelName = null;
			error = null;
			try
			{
				var query = "SELECT * FROM Win32_PnPEntity WHERE (PNPClass='Biometric') OR (Name LIKE '%Fingerprint%') OR (Manufacturer LIKE '%DigitalPersona%') OR (Name LIKE '%U.are.U%')";
				var searcher = new ManagementObjectSearcher(query);
				foreach (ManagementObject obj in searcher.Get())
				{
					modelName = Convert.ToString(obj["Name"]) ?? Convert.ToString(obj["Description"]) ?? "Fingerprint Reader";
					return true;
				}

				try
				{
					var readers = new ReadersCollection();
					if (readers.Count > 0)
					{
						modelName = "DPFP Reader";
						return true;
					}
				}
				catch { }

				return false;
			}
			catch (Exception ex)
			{
				error = ex.Message;
				return false;
			}
		}
	}
}
