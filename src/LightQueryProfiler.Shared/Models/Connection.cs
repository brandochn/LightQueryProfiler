using LightQueryProfiler.Shared.Enums;

namespace LightQueryProfiler.Shared.Models
{
    public class Connection
    {
        /// <summary>
        /// Initializes a new instance of <see cref="Connection"/>.
        /// </summary>
        /// <param name="id">Primary key (0 for new entities).</param>
        /// <param name="initialCatalog">Database name.</param>
        /// <param name="creationDate">When the connection was last used.</param>
        /// <param name="dataSource">Server address or named instance.</param>
        /// <param name="integratedSecurity">True when using Windows Authentication.</param>
        /// <param name="password">Plain-text password; encrypted by the repository layer before storage.</param>
        /// <param name="userId">SQL Server or Azure AD login name.</param>
        /// <param name="engineType">Detected or specified engine type. <see langword="null"/> when using <see cref="AuthenticationMode.ConnectionString"/> (detected at start-profiling time).</param>
        /// <param name="authenticationMode">Authentication method used.</param>
        /// <param name="connectionString">Raw ADO.NET connection string. Only set when <paramref name="authenticationMode"/> is <see cref="AuthenticationMode.ConnectionString"/>. Plain-text — the repository layer decrypts it before passing here. Never log this value.</param>
        public Connection(int id, string initialCatalog, DateTime creationDate, string dataSource, bool integratedSecurity, string? password, string? userId, DatabaseEngineType? engineType = null, AuthenticationMode authenticationMode = AuthenticationMode.WindowsAuth, string? connectionString = null)
        {
            Id = id;
            InitialCatalog = initialCatalog;
            CreationDate = creationDate;
            DataSource = dataSource;
            IntegratedSecurity = integratedSecurity;
            Password = password;
            UserId = userId;
            EngineType = engineType;
            AuthenticationMode = authenticationMode;
            ConnectionString = connectionString;
        }

        public int Id { get; }

        public string InitialCatalog { get; }

        public DateTime CreationDate { get; }

        public string DataSource { get; }

        public bool IntegratedSecurity { get; }

        public string? Password { get; }

        public string? UserId { get; }

        public DatabaseEngineType? EngineType { get; }

        /// <summary>
        /// Gets the authentication mode used for this connection
        /// </summary>
        public AuthenticationMode AuthenticationMode { get; }

        /// <summary>
        /// Gets the raw ADO.NET connection string entered by the user.
        /// Only populated when <see cref="AuthenticationMode"/> is <see cref="AuthenticationMode.ConnectionString"/>.
        /// Value is plain-text — the repository layer decrypts it before setting this property.
        /// </summary>
        /// <remarks>Never log this value — it may contain credentials.</remarks>
        public string? ConnectionString { get; }
    }
}
