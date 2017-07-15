using System.Data.Entity;
using System.Data.Entity.Migrations;

namespace MigrateSqlDatabase.EF
{
    public class AutomaticMigrationsWithDataLossConfiguration<T>: DbMigrationsConfiguration<T> where T : DbContext
    {
        public AutomaticMigrationsWithDataLossConfiguration()
        {
            AutomaticMigrationsEnabled = true;
            AutomaticMigrationDataLossAllowed = true;

            SetSqlGenerator("System.Data.SqlClient", new SqlServerMigrationExistingDbSqlGenerator());
        }
    }
}
