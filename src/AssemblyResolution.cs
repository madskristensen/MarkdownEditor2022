using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace MarkdownEditor2022
{
    /// <summary>
    /// Resolves Markdig and its transitive dependencies strictly from the extension directory
    /// for requests originating from this extension, avoiding global VS binding redirects
    /// that can break Visual Studio's built-in components (such as Diagnostics Tools).
    /// </summary>
    internal static class AssemblyResolution
    {
        private static readonly HashSet<string> _scopedAssemblies = new(StringComparer.OrdinalIgnoreCase)
        {
            "Markdig",
            "System.Memory",
            "System.Buffers",
            "System.Numerics.Vectors",
            "System.Runtime.CompilerServices.Unsafe"
        };

        private static readonly ConcurrentDictionary<string, Assembly> _loadedAssemblies = new(StringComparer.OrdinalIgnoreCase);
        private static readonly string _extensionDirectory = GetExtensionDirectory();
        private static int _registered;

        [ThreadStatic]
        private static bool _isResolving;

        [ModuleInitializer]
        internal static void Register()
        {
            if (Interlocked.Exchange(ref _registered, 1) == 0)
            {
                AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            }
        }

        internal static bool ShouldHandleRequest(Assembly requestingAssembly, string requestedAssemblyName)
        {
            if (string.IsNullOrEmpty(requestedAssemblyName) || requestingAssembly == null)
            {
                return false;
            }

            string simpleName = GetSimpleName(requestedAssemblyName);
            if (!_scopedAssemblies.Contains(simpleName))
            {
                return false;
            }

            if (requestingAssembly == typeof(AssemblyResolution).Assembly)
            {
                return true;
            }

            string requesterName = requestingAssembly.GetName().Name;
            return _scopedAssemblies.Contains(requesterName) && IsInExtensionDirectory(requestingAssembly);
        }

        internal static string GetCandidatePath(string requestedAssemblyName)
        {
            if (string.IsNullOrEmpty(_extensionDirectory) || string.IsNullOrEmpty(requestedAssemblyName))
            {
                return null;
            }

            string simpleName = GetSimpleName(requestedAssemblyName);
            return string.IsNullOrEmpty(simpleName)
                ? null
                : Path.Combine(_extensionDirectory, simpleName + ".dll");
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (_isResolving || !ShouldHandleRequest(args.RequestingAssembly, args.Name))
            {
                return null;
            }

            string simpleName = GetSimpleName(args.Name);
            if (_loadedAssemblies.TryGetValue(simpleName, out Assembly cached))
            {
                return cached;
            }

            string candidatePath = GetCandidatePath(args.Name);
            if (string.IsNullOrEmpty(candidatePath) || !File.Exists(candidatePath))
            {
                return null;
            }

            _isResolving = true;
            try
            {
                Assembly loaded = Assembly.LoadFrom(candidatePath);
                _loadedAssemblies.TryAdd(simpleName, loaded);
                return loaded;
            }
            catch
            {
                return null;
            }
            finally
            {
                _isResolving = false;
            }
        }

        private static string GetSimpleName(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                return string.Empty;
            }

            try
            {
                return new AssemblyName(assemblyName).Name ?? string.Empty;
            }
            catch
            {
                int commaIndex = assemblyName.IndexOf(',');
                return commaIndex >= 0 ? assemblyName.Substring(0, commaIndex).Trim() : assemblyName.Trim();
            }
        }

        private static string GetExtensionDirectory()
        {
            string location = typeof(AssemblyResolution).Assembly.Location;
            return string.IsNullOrEmpty(location) ? string.Empty : Path.GetDirectoryName(location) ?? string.Empty;
        }

        private static bool IsInExtensionDirectory(Assembly assembly)
        {
            if (assembly.IsDynamic || string.IsNullOrEmpty(_extensionDirectory))
            {
                return false;
            }

            string location = assembly.Location;
            if (string.IsNullOrEmpty(location))
            {
                return false;
            }

            string assemblyDir = Path.GetDirectoryName(location);
            return string.Equals(assemblyDir, _extensionDirectory, StringComparison.OrdinalIgnoreCase);
        }
    }
}
