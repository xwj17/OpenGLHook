using System;
using System.IO;
using System.Reflection;

namespace HookLib
{
    internal static class DependencyResolver
    {
        private static bool _installed;
        private static string _baseDir;

        private static readonly string[] Targets =
        {
            "System.Runtime.CompilerServices.Unsafe",
            "System.Buffers",
            "System.Memory",
            "System.Numerics.Vectors",
            "Microsoft.ML.OnnxRuntime",
            "Microsoft.ML.OnnxRuntime.DirectML"
        };

        public static void Install()
        {
            if (_installed) return;
            _installed = true;
            try
            {
                _baseDir = Path.GetDirectoryName(typeof(DependencyResolver).Assembly.Location) ?? "";
                AppDomain.CurrentDomain.AssemblyResolve += OnResolve;
                Logging.Info("[Resolver] Installed. BaseDir=" + _baseDir);
            }
            catch (Exception ex)
            {
                Logging.Exception("[Resolver] Install failed", ex);
            }
        }

        private static Assembly OnResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                var asmName = new AssemblyName(args.Name);
                foreach (var t in Targets)
                {
                    if (asmName.Name.Equals(t, StringComparison.OrdinalIgnoreCase))
                    {
                        string candidate = Path.Combine(_baseDir, asmName.Name + ".dll");
                        if (File.Exists(candidate))
                        {
                            try { return Assembly.LoadFrom(candidate); }
                            catch (Exception exLoad) { Logging.Exception("[Resolver] LoadFrom fail " + candidate, exLoad); }
                        }
                        else
                        {
                            Logging.Warn("[Resolver] Missing dependency file: " + candidate);
                        }
                    }
                }
            }
            catch (Exception ex) { Logging.Exception("[Resolver] OnResolve error", ex); }
            return null;
        }
    }
}