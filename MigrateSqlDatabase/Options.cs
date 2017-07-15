using CommandLine;
using CommandLine.Text;

namespace MigrateSqlDatabase
{
    public class Options
    {
        [Option('l', "library", Required = true, HelpText = "The assembly file that contains the DbContext objects to migrate")]
        public string Libary { get; set; }

        [Option('c', "config", HelpText = "The configuration file used to get the connection strings for the DbContext objects if this is missing or invalid the default config file will be the library name ending in '.config'")]
        public string ConfigFile { get; set; }

        [Option('f', "force", HelpText = "Allow data loss to occur")]
        public bool Force { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              current => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
