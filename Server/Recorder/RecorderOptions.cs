namespace Calendare.Server.Recorder;

public class RecorderOptions
{
    public RecorderOperationMode Mode { get; set; } = RecorderOperationMode.None;
    public string? Directory { get; set; }
}
