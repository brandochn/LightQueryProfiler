namespace LightQueryProfiler.Shared.Models
{
    public class Connection
    {
        public Connection(int id, string initialCatalog, DateTime creationDate, string dataSource, bool integratedSecurity, string? password, string? userId)
        {
            Id = id;
            InitialCatalog = initialCatalog;
            CreationDate = creationDate;
            DataSource = dataSource;
            IntegratedSecurity = integratedSecurity;
            Password = password;
            UserId = userId;
        }

        public int Id { get; }

        public string InitialCatalog { get; }

        public DateTime CreationDate { get; }

        public string DataSource { get; }

        public bool IntegratedSecurity { get; }

        public string? Password { get; }

        public string? UserId { get; }
    }
}