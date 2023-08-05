namespace LightQueryProfiler.WinFormsApp.Models
{
    public class AuthenticationMode
    {
        public int Value { get; private set; }
        public string? Name { get; private set; }

        public AuthenticationMode(string name, int value)
        {
            Name = name;
            Value = value;
        }
    }
}