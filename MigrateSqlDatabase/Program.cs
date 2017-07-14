using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Reflection;
using CommandLine;
using MigrateSqlDatabase.EF;

namespace MigrateSqlDatabase
{
    class Program
    {
        private static readonly MethodInfo InitalizeMethod = typeof(Program).GetMethod("SetInitializer",
            BindingFlags.Static | BindingFlags.NonPublic);

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            var options = new Options();

            if (!Parser.Default.ParseArguments(args, options))
            {
                Console.WriteLine(options.GetUsage());
                Environment.Exit(-1);
            }

            if (string.IsNullOrEmpty(options.Libary) ||
                !File.Exists(options.Libary))
            {
                var currentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                foreach (var assemblyFile in Directory.GetFiles(currentDirectory, "*.dll"))
                {
                    MigrateAssembly(assemblyFile, options.ConnectionString);
                }
                Console.WriteLine("Migration Complete");
                Environment.Exit(0);
            }

            MigrateAssembly(options.Libary, options.ConnectionString);
            Console.WriteLine("Migration Complete");
            Environment.Exit(0);
        }

        private static IEnumerable<Type> GetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(_ => _ != null);
            }
        }

        private static void SetInitializerGeneric(Type type)
        {
            InitalizeMethod.MakeGenericMethod(type).Invoke(null, null);
        }

        private static void SetInitializer<T>() where T : DbContext
        {
            Database.SetInitializer(
                new MigrateDatabaseToLatestVersion<T, AutomaticMigrationsExistingDbConfiguration<T>>());
        }

        private static void MigrateAssembly(string assemblyFile, string connectionString)
        {
            Console.WriteLine($"Loading assembly {assemblyFile}");
            AppDomain.CurrentDomain.Load(File.ReadAllBytes(assemblyFile));
            var assembly = Assembly.LoadFile(assemblyFile);
            var contextTypes =
                GetTypes(assembly)
                    .Where(_ => typeof(DbContext).IsAssignableFrom(_) && _.GetConstructor(Type.EmptyTypes) != null);

            foreach (var type in contextTypes)
            {
                Console.WriteLine($"Initializing {type.FullName}");
                SetInitializerGeneric(type);
                using (var dbContext = (DbContext)Activator.CreateInstance(type))
                {
                    if (!string.IsNullOrEmpty(connectionString))
                    {
                        dbContext.Database.Connection.ConnectionString = connectionString;
                    }
                    Console.WriteLine($"Migrating {type.FullName} to {dbContext.Database.Connection.ConnectionString}");
                    dbContext.Database.Initialize(true);
                }
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.Error.WriteLine(e);
        }
    }

}
