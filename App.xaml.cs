using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Windows;
using WatchAlong.Models;
using WatchAlong.Services;

namespace WatchAlong;

public partial class App : Application {

	public static string Name { get; } = "WatchAlong";
	public static ConfigService ConfigService { get; }
	public static Config Config { get; }
	public static HttpClient HttpClient { get; } = new();

	private static readonly string appDataFolderPath;
	private static readonly string logFilePath;

	static App() {
		appDataFolderPath = $@"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\{Name}";
		if (!Directory.Exists(appDataFolderPath)) {
			Directory.CreateDirectory(appDataFolderPath);
		}
		logFilePath = $"{appDataFolderPath}\\app.log";
		ConfigService = new($"{appDataFolderPath}\\config.json");
		Config = ConfigService.Config;
	}

	public static void DisplayError(string error) {
		new Thread(() => MessageBox.Show(error, $"{Name} - Error", MessageBoxButton.OK, MessageBoxImage.Error)).Start();
	}

	public static void Log(string text) {
		Debug.WriteLine(text);
		try {
			File.AppendAllText(logFilePath, $"[{DateTime.Now.ToLocalTime()}] {text}\r\n");
		} catch (Exception e) {
			Debug.WriteLine(e.ToString());
		}
	}

}
