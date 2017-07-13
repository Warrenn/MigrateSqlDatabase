using CommandLine;
using CommandLine.Text;

namespace MigrateSqlDatabase
{
    public class Options
    {
        [Option('l', "Libary", HelpText = "Assembly file to evaluate overrides Folder option")]
        public string Libary { get; set; }

        [Option('c', "connection-string", HelpText = "Default connection string of the database to update")]
        public string ConnectionString { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              current => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
