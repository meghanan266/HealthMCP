using System.ComponentModel;
using System.Text;
using Microsoft.Data.Sqlite;
using ModelContextProtocol.Server;

namespace HealthMCP.Server.Tools;

[McpServerToolType]
public static class ClinicalQueryTools
{
    public static readonly string DbPath = Path.Combine(AppContext.BaseDirectory, "data", "clinical.db");

    [McpServerTool(Name = "query_clinical_data")]
    [Description("Runs a read-only SQL SELECT against the clinical SQLite database. Results are pipe-formatted (max 50 rows).")]
    public static string QueryClinicalData([Description("A single SELECT statement only.")] string sql)
    {
        var trimmed = sql.TrimStart();
        if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            return "Error: Only SELECT queries are allowed.";

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DbPath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();

        try
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = trimmed;

            using var reader = command.ExecuteReader();
            var fieldCount = reader.FieldCount;
            var headers = new string[fieldCount];
            for (var i = 0; i < fieldCount; i++)
                headers[i] = reader.GetName(i);

            var rowLines = new List<string>(capacity: 50);
            while (reader.Read() && rowLines.Count < 50)
            {
                var cells = new string[fieldCount];
                for (var i = 0; i < fieldCount; i++)
                {
                    var v = reader.IsDBNull(i) ? "" : reader.GetValue(i);
                    cells[i] = v?.ToString() ?? "";
                }
                rowLines.Add(string.Join(" | ", cells));
            }

            if (rowLines.Count == 0)
                return "Query returned no results.";

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(" | ", headers));
            foreach (var line in rowLines)
                sb.AppendLine(line);
            return sb.ToString().TrimEnd();
        }
        catch (SqliteException ex)
        {
            return $"Query error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "list_clinical_tables")]
    [Description("Lists user-defined tables in the clinical database with their full CREATE TABLE statements.")]
    public static string ListClinicalTables()
    {
        const string metaSql = """
            SELECT name, sql
            FROM sqlite_master
            WHERE type = 'table'
              AND name NOT LIKE 'sqlite_%'
            ORDER BY name;
            """;

        return QueryClinicalData(metaSql);
    }
}
