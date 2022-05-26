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
                    return "Windows Auth";

                case AuthenticationMode.SQLServerAuth:
                    return "SQL Server Auth";

                default:
                    return string.Empty;
            }
        }
    }
}