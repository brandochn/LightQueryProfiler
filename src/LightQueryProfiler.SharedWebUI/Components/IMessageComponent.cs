namespace LightQueryProfiler.SharedWebUI.Components
{
    public interface IMessageComponent
    {
        void ShowMessage(string title, string message, MessageType messageType);
    }

    public enum MessageType
    {
        Info = 0,
        Warning = 1,
        Error = 2,
        Success = 3,
    }
}