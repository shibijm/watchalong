using System;
using System.IO;
using System.Text.Json;
using WatchAlong.Models;

namespace WatchAlong.Services;

public class ConfigService {

	public Config Config { get; }

	private readonly string configFilePath;

	public ConfigService(string configFilePath) {
		this.configFilePath = configFilePath;
		if (File.Exists(configFilePath)) {
			try {
				string text = File.ReadAllText(configFilePath);
				Config = JsonSerializer.Deserialize<Config>(text);
				if (Config == null) {
					throw new Exception("Config file deserialised to null");
				}
			} catch (Exception e) {
				App.Log(e.ToString());
				File.Move(configFilePath, configFilePath.Replace(".json", $"-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.json"));
				App.DisplayError("Failed to parse the application's config file. A new one has been created.");
			}
		}
		Config ??= new();
		SaveConfig();
	}

	public void SaveConfig() {
		try {
			File.WriteAllText(configFilePath, "");
			using FileStream fileStream = File.OpenWrite(configFilePath);
			JsonSerializer.Serialize(fileStream, Config, new JsonSerializerOptions() { WriteIndented = true });
		} catch (Exception e) {
			App.Log(e.ToString());
			App.DisplayError("Failed to write to the application's config file.");
		}
	}

}
