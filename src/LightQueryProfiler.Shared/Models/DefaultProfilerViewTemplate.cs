namespace LightQueryProfiler.Shared.Models
{
    public class DefaultProfilerViewTemplate : BaseProfilerViewTemplate
    {
        public override string Name { get; set; } = "DefaultProfilerViewTemplate";
        public override IList<BaseColumnViewTemplate> Columns { get; set; }

        public DefaultProfilerViewTemplate()
        {
            Columns = new List<BaseColumnViewTemplate>();
            Columns.Add(new ColumnViewTemplate()
            {
                Name = "EventClass",
                EventsMapped = new List<string>() { "name" }
            });

            Columns.Add(new ColumnViewTemplate()
            {
                Name = "TextData",
                EventsMapped = new List<string>() { "options_text", "batch_text", "statement" }
            });

            Columns.Add(new ColumnViewTemplate()
            {
                Name = "ApplicationName",
                EventsMapped = new List<string>() { "client_app_name" }
            });

            Columns.Add(new ColumnViewTemplate()
            {
                Name = "NTUserName",
                EventsMapped = new List<string>() { "nt_username" }
            });

            Columns.Add(new ColumnViewTemplate()
            {
                Name = "LoginName",
                EventsMapped = new List<string>() { "server_principal_name", "username" }
            });

            Columns.Add(new ColumnViewTemplate()
            {
                Name = "ClientProcessID",
                EventsMapped = new List<string>() { "client_pid" }
            });

            Columns.Add(new ColumnViewTemplate()
            {
                Name = "SPID",
                EventsMapped = new List<string>() { "session_id" }
            });

            Columns.Add(new ColumnViewTemplate()
            {
                Name = "StartTime",
                EventsMapped = new List<string>() { "timestamp" }
            });

            Columns.Add(new ColumnViewTemplate()
            {
                Name = "CPU",
                EventsMapped = new List<string>() { "cpu_time" }
            });

            Columns.Add(new ColumnViewTemplate()
            {
                Name = "Reads",
                EventsMapped = new List<string>() { "logical_reads" }
            });

            Columns.Add(new ColumnViewTemplate()
            {
                Name = "Writes",
                EventsMapped = new List<string>() { "writes" }
            });

            Columns.Add(new ColumnViewTemplate()
            {
                Name = "Duration",
                EventsMapped = new List<string>() { "duration" }
            });

            Columns.Add(new ColumnViewTemplate()
            {
                Name = "DatabaseID",
                EventsMapped = new List<string>() { "database_id" }
            });

            Columns.Add(new ColumnViewTemplate()
            {
                Name = "DatabaseName",
                EventsMapped = new List<string>() { "database_name" }
            });
        }
    }
}