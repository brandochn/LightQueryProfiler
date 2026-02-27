using LightQueryProfiler.Shared.Enums;

namespace LightQueryProfiler.Shared.Extensions
{
    public static class AuthenticationModeExtensions
    {
        public static string GetString(this AuthenticationMode am)
        {
            switch (am)
            {
                case AuthenticationMode.WindowsAuth:
                    return "Windows Authentication";

                case AuthenticationMode.SQLServerAuth:
                    return "SQL Server Authentication";

                case AuthenticationMode.AzureSQLDatabase:
                    return "Azure SQL Database";

                default:
                    return string.Empty;
            }
        }
    }
}
