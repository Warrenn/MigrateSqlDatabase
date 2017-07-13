using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Migrations.Model;
using System.Data.Entity.Migrations.Utilities;
using System.Data.Entity.SqlServer;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace MigrateSqlDatabase.EF
{
    public class SqlServerMigrationExistingDbSqlGenerator: SqlServerMigrationSqlGenerator
    {
        private const string NamePartRegex
          = @"(?:(?:\[(?<part{0}>(?:(?:\]\])|[^\]])+)\])|(?<part{0}>[^\.\[\]]+))";

        private static string Join(IEnumerable<string> parts, string seperator = ", ")
        {
            return string.Join(seperator, parts.ToArray());
        }

        private static bool EqualsIgnoreCase(string a, string b)
        {
            return string.Equals(a, b, StringComparison.InvariantCultureIgnoreCase);
        }

        private static void NotNull<T>(T value, string parameterName) where T : class
        {
            if (value == null)
                throw new ArgumentNullException(parameterName);
        }

        private static readonly Regex PartExtractor
            = new Regex(
                string.Format(
                    CultureInfo.InvariantCulture,
                    @"^{0}(?:\.{1})?$",
                    string.Format(CultureInfo.InvariantCulture, NamePartRegex, 1),
                    string.Format(CultureInfo.InvariantCulture, NamePartRegex, 2)),
                RegexOptions.Compiled);

        protected override void Generate(AddForeignKeyOperation addForeignKeyOperation)
        {
            NotNull(addForeignKeyOperation, "addForeignKeyOperation");

            using (var writer = Writer())
            {
                writer.Write("IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'");
                writer.Write(Name(addForeignKeyOperation.DependentTable));
                writer.Write("') AND referenced_object_id = OBJECT_ID('");
                writer.Write(Name(addForeignKeyOperation.PrincipalTable));
                writer.WriteLine("'))");
                writer.WriteLine("BEGIN");
                writer.Indent++;

                writer.Write("ALTER TABLE ");
                writer.Write(Name(addForeignKeyOperation.DependentTable));
                writer.Write(" ADD CONSTRAINT ");
                writer.Write(Quote(addForeignKeyOperation.Name));
                writer.Write(" FOREIGN KEY (");
                writer.Write(Join(addForeignKeyOperation.DependentColumns.Select(Quote)));
                writer.Write(") REFERENCES ");
                writer.Write(Name(addForeignKeyOperation.PrincipalTable));
                writer.Write(" (");
                writer.Write(Join(addForeignKeyOperation.PrincipalColumns.Select(Quote)));
                writer.Write(")");

                if (addForeignKeyOperation.CascadeDelete)
                {
                    writer.Write(" ON DELETE CASCADE");
                }

                writer.WriteLine("");
                writer.Indent--;
                writer.WriteLine("END");

                Statement(writer);
            }
        }

        protected override void Generate(AddPrimaryKeyOperation addPrimaryKeyOperation)
        {
            NotNull(addPrimaryKeyOperation, "addPrimaryKeyOperation");
            var tablename = ParseDatabaseName(addPrimaryKeyOperation.Table);

            using (var writer = Writer())
            {
                writer.Write("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE CONSTRAINT_TYPE = N'PRIMARY KEY' AND TABLE_NAME = N'");
                writer.Write(tablename.Item2);
                writer.Write("' AND TABLE_SCHEMA = N'");
                writer.Write(tablename.Item1);
                writer.WriteLine("')");
                writer.WriteLine("BEGIN");
                writer.Indent++;

                writer.Write("ALTER TABLE ");
                writer.Write(Name(addPrimaryKeyOperation.Table));
                writer.Write(" ADD CONSTRAINT ");
                writer.Write(Quote(addPrimaryKeyOperation.Name));
                writer.Write(" PRIMARY KEY ");

                if (!addPrimaryKeyOperation.IsClustered)
                {
                    writer.Write("NONCLUSTERED ");
                }

                writer.Write("(");
                writer.Write(Join(addPrimaryKeyOperation.Columns.Select(Quote)));
                writer.WriteLine(")");

                writer.Indent--;
                writer.WriteLine("END");

                Statement(writer);
            }
        }

        protected override void Generate(CreateIndexOperation createIndexOperation)
        {
            NotNull(createIndexOperation, "createIndexOperation");

            using (var writer = Writer())
            {
                writer.Write("IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'");
                writer.Write(createIndexOperation.Name);
                writer.Write("' AND object_id = OBJECT_ID(N'");
                writer.Write(Name(createIndexOperation.Table));
                writer.WriteLine("'))");
                writer.WriteLine("BEGIN");
                writer.Indent++;

                writer.Write("CREATE ");

                if (createIndexOperation.IsUnique)
                {
                    writer.Write("UNIQUE ");
                }

                if (createIndexOperation.IsClustered)
                {
                    writer.Write("CLUSTERED ");
                }

                writer.Write("INDEX ");
                writer.Write(Quote(createIndexOperation.Name));
                writer.Write(" ON ");
                writer.Write(Name(createIndexOperation.Table));
                writer.Write("(");
                writer.Write(Join(createIndexOperation.Columns.Select(Quote)));
                writer.WriteLine(")");

                writer.Indent--;
                writer.WriteLine("END");

                Statement(writer);
            }
        }

        protected override void WriteCreateTable(CreateTableOperation createTableOperation, IndentedTextWriter writer)
        {
            NotNull(createTableOperation, "createTableOperation");
            NotNull(writer, "writer");

            var tableName = ParseDatabaseName(createTableOperation.Name);

            writer.Write("IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'");
            writer.Write(tableName.Item2);
            writer.Write("' AND TABLE_SCHEMA = N'");
            writer.Write(tableName.Item1);
            writer.WriteLine("')");
            writer.WriteLine("BEGIN");
            writer.Indent++;

            writer.WriteLine("CREATE TABLE " + Name(createTableOperation.Name) + " (");
            writer.Indent++;

            var i = 0;
            foreach (var columnModel in createTableOperation.Columns)
            {
                Generate(columnModel, writer);

                if (i < createTableOperation.Columns.Count - 1)
                {
                    writer.WriteLine(",");
                }
                i++;
            }

            if (createTableOperation.PrimaryKey != null)
            {
                writer.WriteLine(",");
                writer.Write("CONSTRAINT ");
                writer.Write(Quote(createTableOperation.PrimaryKey.Name));
                writer.Write(" PRIMARY KEY ");

                if (!createTableOperation.PrimaryKey.IsClustered)
                {
                    writer.Write("NONCLUSTERED ");
                }

                writer.Write("(");
                writer.Write(Join(createTableOperation.PrimaryKey.Columns.Select(Quote)));
                writer.WriteLine(")");
            }
            else
            {
                writer.WriteLine();
            }

            writer.Indent--;
            writer.WriteLine(")");

            writer.Indent--;
            writer.WriteLine("END");
            writer.WriteLine("ELSE");
            writer.WriteLine("BEGIN");
            writer.Indent++;

            foreach (var columnModel in createTableOperation.Columns)
            {
                var alterColumn = new AddColumnOperation(createTableOperation.Name, columnModel);

                WriteCreateColumn(alterColumn, writer);
            }

            writer.Indent--;
            writer.WriteLine("END");
        }

        protected override void Generate(AddColumnOperation addColumnOperation)
        {
            NotNull(addColumnOperation, "addColumnOperation");

            using (var writer = Writer())
            {
                WriteCreateColumn(addColumnOperation, writer);
                Statement(writer);
            }
        }

        private void WriteCreateColumn(AddColumnOperation addColumnOperation, IndentedTextWriter writer)
        {
            NotNull(addColumnOperation, "addColumnOperation");
            NotNull(writer, "writer");

            var column = addColumnOperation.Column;

            writer.Write("IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE Name = N'");
            writer.Write(column.Name);
            writer.Write("' AND object_id = OBJECT_ID(N'");
            writer.Write(Name(addColumnOperation.Table));
            writer.WriteLine("'))");
            writer.WriteLine("BEGIN");
            writer.Indent++;

            writer.Write("ALTER TABLE ");
            writer.Write(Name(addColumnOperation.Table));
            writer.Write(" ADD ");

            Generate(column, writer);

            if ((column.IsNullable != null)
                && !column.IsNullable.Value
                && (column.DefaultValue == null)
                && (string.IsNullOrWhiteSpace(column.DefaultValueSql))
                && !column.IsIdentity
                && !column.IsTimestamp
                && !EqualsIgnoreCase(column.StoreType, "rowversion")
                && !EqualsIgnoreCase(column.StoreType, "timestamp"))
            {
                writer.Write(" DEFAULT ");

                if (column.Type == PrimitiveTypeKind.DateTime)
                {
                    writer.Write(Generate(DateTime.Parse("1900-01-01 00:00:00", CultureInfo.InvariantCulture)));
                }
                else
                {
                    writer.Write(Generate((dynamic)column.ClrDefaultValue));
                }
            }

            writer.WriteLine("");
            writer.Indent--;
            writer.WriteLine("END");
        }

        private static Tuple<string, string> ParseDatabaseName(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Invalid database name");

            var match = PartExtractor.Match(name.Trim());

            if (!match.Success)
                throw new ArgumentException("Invalid database name " + name);

            var part1 = match.Groups["part1"].Value.Replace("]]", "]");
            var part2 = match.Groups["part2"].Value.Replace("]]", "]");

            return !string.IsNullOrWhiteSpace(part2)
                    ? new Tuple<string, string>(part1, part2)
                    : new Tuple<string, string>(string.Empty, part1);
        }
    }
}
