using LightQueryProfiler.Shared.Data;
using LightQueryProfiler.Shared.Repositories.Interfaces;
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace LightQueryProfiler.Shared.Repositories
{
    public class XEventRepository : IXEventRepository
    {
        private IApplicationDbContext _applicationDbContext;

        public XEventRepository(IApplicationDbContext applicationDbContext)
        {
            _applicationDbContext = applicationDbContext;
        }

        public IApplicationDbContext ApplicationDbContext
        {
            set
            {
                _applicationDbContext = value;
            }
        }

        public void CreateXEventSession(string sessionName, BaseProfilerSessionTemplate template)
        {
            if (string.IsNullOrEmpty(sessionName))
            {
                throw new Exception("sessionName cannot be null or empty");
            }

            if (template == null)
            {
                throw new Exception("template cannot be null or empty");
            }

            using (DbConnection connection = _applicationDbContext.GetConnection())
            {
                DbCommand cmd = new SqlCommand();
                cmd.Connection = connection;
                cmd.CommandText = template.CreateSQLStatement(sessionName);
                cmd.CommandType = CommandType.Text;

                cmd.Parameters.Add(new SqlParameter()
                {
                    ParameterName = "@sessionName",
                    Direction = ParameterDirection.Input,
                    SqlDbType = SqlDbType.VarChar,
                    Value = sessionName
                });

                connection.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void DeleteXEventSession(string sessionName)
        {
            if (string.IsNullOrEmpty(sessionName))
            {
                throw new Exception("sessionName cannot be null or empty");
            }

            using (DbConnection connection = _applicationDbContext.GetConnection())
            {
                DbCommand cmd = new SqlCommand();
                cmd.Connection = connection;
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = @$"IF EXISTS (SELECT TOP 1 1
						                FROM sys.server_event_sessions
						                WHERE name = @sessionName)
					                BEGIN
						                DROP EVENT SESSION [{sessionName}] ON SERVER
					                END";

                cmd.Parameters.Add(new SqlParameter()
                {
                    ParameterName = "@sessionName",
                    Direction = ParameterDirection.Input,
                    SqlDbType = SqlDbType.VarChar,
                    Value = sessionName
                });

                connection.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void DisconnectSession(string sessionName)
        {
            StopProfiling(sessionName);

            DeleteXEventSession(sessionName);
        }

        public async Task<string> GetXEventsDataAsync(string sessionName, string targetName)
        {
            string result = string.Empty;

            using (DbConnection connection = _applicationDbContext.GetConnection())
            {
                DbCommand cmd = new SqlCommand();
                cmd.Connection = connection;
                cmd.CommandText = @"SELECT target_data FROM sys.dm_xe_session_targets AS t
                                    JOIN sys.dm_xe_sessions AS s ON t.event_session_address=s.address
                                    WHERE s.name = @sessionName AND t.target_name = @targetName";
                cmd.CommandType = CommandType.Text;

                cmd.Parameters.Add(new SqlParameter()
                {
                    ParameterName = "@sessionName",
                    Direction = ParameterDirection.Input,
                    SqlDbType = SqlDbType.VarChar,
                    Value = sessionName
                });

                cmd.Parameters.Add(new SqlParameter()
                {
                    ParameterName = "@targetName",
                    Direction = ParameterDirection.Input,
                    SqlDbType = SqlDbType.VarChar,
                    Value = targetName
                });

                connection.Open();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (reader.HasRows == false)
                    {
                        return string.Empty;
                    }

                    while (reader.Read())
                    {
                        result = reader.GetString(0);
                    }
                }
            }

            return result;
        }

        public void PauseProfiling(string sessionName)
        {
            if (string.IsNullOrEmpty(sessionName))
            {
                throw new Exception("sessionName cannot be null or empty");
            }

            using (DbConnection connection = _applicationDbContext.GetConnection())
            {
                DbCommand cmd = new SqlCommand();
                cmd.Connection = connection;
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = @$"IF EXISTS (SELECT TOP 1 1
						                FROM sys.server_event_sessions
						                WHERE name = @sessionName)
					                BEGIN
						                ALTER EVENT SESSION [{sessionName}] ON SERVER STATE = STOP
					                END";

                cmd.Parameters.Add(new SqlParameter()
                {
                    ParameterName = "@sessionName",
                    Direction = ParameterDirection.Input,
                    SqlDbType = SqlDbType.VarChar,
                    Value = sessionName
                });

                connection.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void StartProfiling(string sessionName)
        {
            if (string.IsNullOrEmpty(sessionName))
            {
                throw new Exception("sessionName cannot be null or empty");
            }

            using (DbConnection connection = _applicationDbContext.GetConnection())
            {
                DbCommand cmd = new SqlCommand();
                cmd.Connection = connection;
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = @$"IF EXISTS (SELECT TOP 1 1
						                FROM sys.server_event_sessions
						                WHERE name = @sessionName)
					                BEGIN
						                ALTER EVENT SESSION [{sessionName}] ON SERVER STATE = START
					                END";

                cmd.Parameters.Add(new SqlParameter()
                {
                    ParameterName = "@sessionName",
                    Direction = ParameterDirection.Input,
                    SqlDbType = SqlDbType.VarChar,
                    Value = sessionName
                });

                connection.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void StopProfiling(string sessionName)
        {
            if (string.IsNullOrEmpty(sessionName))
            {
                throw new Exception("sessionName cannot be null or empty");
            }

            using (DbConnection connection = _applicationDbContext.GetConnection())
            {
                DbCommand cmd = new SqlCommand();
                cmd.Connection = connection;
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = @$"IF EXISTS (SELECT TOP 1 1
						                FROM sys.server_event_sessions
						                WHERE name = @sessionName)
					                BEGIN
						                ALTER EVENT SESSION [{sessionName}] ON SERVER STATE = STOP
					                END";

                cmd.Parameters.Add(new SqlParameter()
                {
                    ParameterName = "@sessionName",
                    Direction = ParameterDirection.Input,
                    SqlDbType = SqlDbType.VarChar,
                    Value = sessionName
                });

                connection.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}