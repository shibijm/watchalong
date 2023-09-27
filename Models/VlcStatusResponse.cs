﻿using System.Text.Json.Serialization;

namespace WatchAlong.Models;

public class VlcStatusResponse {

	[JsonPropertyName("state")]
	public string State { get; set; }

	[JsonPropertyName("time")]
	public int Time { get; set; }

}
