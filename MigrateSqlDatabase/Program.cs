using System;
using System.Collections.Generic;
using System.Configuration;
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

            Configuration config = null;

            if (string.IsNullOrEmpty(options.ConfigFile) || !File.Exists(options.ConfigFile))
            {
                Console.WriteLine($"Warning the config file {options.ConfigFile} is invalid loading config from library");
                config = ConfigurationManager.OpenExeConfiguration(options.Libary);
            }
            if (Path.GetExtension(options.ConfigFile) == ".config")
            {
                var fileMap = new ExeConfigurationFileMap { ExeConfigFilename = options.ConfigFile };
                config = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);
            }
            if (config == null)
            {
                config = ConfigurationManager.OpenExeConfiguration(options.ConfigFile);
            }

            config.SaveAs(AppDomain.CurrentDomain.SetupInformation.ConfigurationFile);
            ConfigurationManager.RefreshSection("connectionStrings");

            MigrateAssembly(options.Libary, options.Force);
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

        private static void SetInitializerGeneric(Type type, bool dataloss)
        {
            InitalizeMethod.MakeGenericMethod(type).Invoke(null, new object[] { dataloss });
        }

        private static void SetInitializer<T>(bool dataloss) where T : DbContext
        {
            if (dataloss)
            {
                Database.SetInitializer(
                    new MigrateDatabaseToLatestVersion<T, AutomaticMigrationsWithDataLossConfiguration<T>>());
                return;
            }
            Database.SetInitializer(
                new MigrateDatabaseToLatestVersion<T, AutomaticMigrationsExistingDbConfiguration<T>>());
        }

        private static void MigrateAssembly(string assemblyFile, bool dataloss)
        {
            Console.WriteLine($"Loading assembly {assemblyFile}");

            AssemblyResolver.AddAssembly(assemblyFile);
            var assembly = Assembly.LoadFile(assemblyFile);
            var contextTypes = GetTypes(assembly)
                .Where(_ =>
                    typeof(DbContext).IsAssignableFrom(_) &&
                    _.GetConstructor(Type.EmptyTypes) != null);

            foreach (var type in contextTypes)
            {
                Console.WriteLine($"Initializing {type.FullName}");
                SetInitializerGeneric(type, dataloss);
                using (var dbContext = (DbContext)Activator.CreateInstance(type))
                {
                    Console.WriteLine($"Migrating {type.FullName} to {dbContext.Database.Connection.ConnectionString}");
                    dbContext.Database.Initialize(true);
                }
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.Error.WriteLine((Exception)e.ExceptionObject);
            Environment.Exit(-1);
        }
    }

}
