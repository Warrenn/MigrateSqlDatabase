using System.Data.Entity;
using System.Data.Entity.Migrations;

namespace MigrateSqlDatabase.EF
{
    public class AutomaticMigrationsExistingDbConfiguration<T>: DbMigrationsConfiguration<T> where T : DbContext
    {
        public AutomaticMigrationsExistingDbConfiguration()
        {
            AutomaticMigrationsEnabled = true;
            AutomaticMigrationDataLossAllowed = false;

            SetSqlGenerator("System.Data.SqlClient", new SqlServerMigrationExistingDbSqlGenerator());
        }
    }
}
