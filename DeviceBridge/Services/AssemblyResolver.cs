using System;
using System.IO;
using System.Reflection;

namespace DeviceBridge.Services
{
    /// <summary>
    /// Custom assembly resolver to handle Digital Persona SDK loading in release mode
    /// </summary>
    public static class AssemblyResolver
    {
        private static bool _isInitialized = false;

        public static void Initialize()
        {
            if (_isInitialized) return;
            
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            _isInitialized = true;
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                var assemblyName = new AssemblyName(args.Name);
                var assemblyFileName = assemblyName.Name + ".dll";
                
                // Try to find the assembly in the Digital Persona folder
                var digitalPersonaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Digital Persona", assemblyFileName);
                if (File.Exists(digitalPersonaPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[AssemblyResolver] Loading {assemblyFileName} from Digital Persona folder");
                    return Assembly.LoadFrom(digitalPersonaPath);
                }

                // Try to find in the output directory
                var outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assemblyFileName);
                if (File.Exists(outputPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[AssemblyResolver] Loading {assemblyFileName} from output directory");
                    return Assembly.LoadFrom(outputPath);
                }

                // Try to find in the parent directory (for development scenarios)
                var parentPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", assemblyFileName);
                if (File.Exists(parentPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[AssemblyResolver] Loading {assemblyFileName} from parent directory");
                    return Assembly.LoadFrom(parentPath);
                }

                System.Diagnostics.Debug.WriteLine($"[AssemblyResolver] Could not resolve assembly: {args.Name}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AssemblyResolver] Error resolving assembly {args.Name}: {ex.Message}");
                return null;
            }
        }
    }
}
