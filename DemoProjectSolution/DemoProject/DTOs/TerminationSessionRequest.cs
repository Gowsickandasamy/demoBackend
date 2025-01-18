namespace DemoProject.DTOs
{
    public class TerminateSessionRequest
    {
        public Guid SessionId { get; set; }
        public string Message { get; set; }
    }
}
