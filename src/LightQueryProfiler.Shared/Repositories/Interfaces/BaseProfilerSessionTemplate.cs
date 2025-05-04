namespace LightQueryProfiler.Shared.Repositories.Interfaces
{
    public abstract class BaseProfilerSessionTemplate
    {
        public string Name { get; set; } = string.Empty;

        public abstract string GetDefaultView();

        public abstract string CreateSQLStatement(string sessionName);
    }
}