using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MigrateSqlDatabase
{
    public class AssemblyResolver
    {
        private static readonly Dictionary<string, string> Assemblies;
        private static readonly List<string> Paths;

        static AssemblyResolver()
        {
            var comparer = StringComparer.CurrentCultureIgnoreCase;
            Assemblies = new Dictionary<string, string>(comparer);
            Paths = new List<string>();
            AppDomain.CurrentDomain.AssemblyResolve += ResolveHandler;
        }

        public static void AddAssembly(string fullpath)
        {
            if (!File.Exists(fullpath)) throw new ArgumentException($"Invalid folderpath {fullpath}");
            var path = Path.GetDirectoryName(fullpath);

            foreach (
                var file in Directory.GetFiles(path, "*.*")
                    .Where(_ => new[] {".exe", ".dll"}
                        .Contains(Path.GetExtension(_))))
            {
                var assembly = Assembly.LoadFile(file);
                Assemblies[assembly.GetName(true).Name] = file;
            }
        }

        private static Assembly ResolveHandler(object sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name);
            if (Assemblies.ContainsKey(assemblyName.Name) && File.Exists(Assemblies[assemblyName.Name]))
            {
                return Assembly.LoadFrom(Assemblies[assemblyName.Name]);
            }

            foreach (var path in Paths)
            {
                var filename = Path.Combine(path, assemblyName.Name) + ".dll";
                if(File.Exists(filename)) return Assembly.LoadFrom(filename);
                filename = Path.Combine(path, assemblyName.Name) + ".exe";
                if(File.Exists(filename)) return Assembly.LoadFrom(filename);
            }
            return null;
        }
    }
}
