using System.Text.Json.Serialization;

namespace WatchAlong.Models;

public class WebSocketEnvelope {

	[JsonPropertyName("type")]
	public required string Type { get; set; }

	[JsonPropertyName("data")]
	public dynamic? Data { get; set; }

}
