using System.Text.Json.Serialization;

namespace Calendare.Server.Recorder;


[JsonConverter(typeof(JsonStringEnumConverter<RecorderOperationMode>))]
public enum RecorderOperationMode
{
    None,
    Files,
    Database,
}
