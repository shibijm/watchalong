using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using WatchAlong.Models;
using WatchAlong.Utils;

namespace WatchAlong.Services.MediaPlayerControllers;

public class VlcController(string url, string password) : MediaPlayerController {

	private readonly string url = url;
	private readonly string password = password;

	public override async Task StartPolling(Action<string> statusCallback, Action<string, int> stateCallback) {
		statusCallback("Connecting");
		shouldPoll = true;
		while (shouldPoll) {
			await semaphoreLock.WaitAsync();
			try {
				HttpResponseMessage responseMessage = await HttpRequest(HttpMethod.Get, "/requests/status.json");
				if (!responseMessage.IsSuccessStatusCode) {
					State = "NOT_CONNECTED";
					Position = 0;
					statusCallback($"HTTP Status - {responseMessage.StatusCode}");
					continue;
				}
				string response = await responseMessage.Content.ReadAsStringAsync();
				VlcStatusResponse statusResponse = JsonSerializer.Deserialize<VlcStatusResponse>(response)!;
				if (statusResponse == null) {
					State = "NOT_CONNECTED";
					Position = 0;
					statusCallback("Deserialisation failed");
					continue;
				}
				if (statusResponse.State == "stopped") {
					State = "STOPPED";
					Position = 0;
					statusCallback("No media playing");
					continue;
				}
				string currentState = statusResponse.State == "paused" ? "PAUSED" : "PLAYING";
				int currentPosition = statusResponse.Time;
				statusCallback($"{Strings.CapitaliseFirstLetter(currentState)}, {Strings.SecondsToTimestamp(currentPosition)}");
				if (!firstStateUpdate && (currentState != State || Math.Abs(Position - currentPosition) > seekThreshold)) {
					stateCallback(currentState, currentPosition);
				}
				State = currentState;
				Position = currentPosition;
			} catch (Exception e) {
				State = "ERROR";
				Position = 0;
				statusCallback($"Error: {e.Message}");
			} finally {
				firstStateUpdate = false;
				semaphoreLock.Release();
				await Task.Delay(1000);
			}
		}
		State = "NOT_CONNECTED";
		Position = 0;
	}

	public override async void Play(int position) {
		await PlayPause("pl_forceresume", "PLAYING", position);
	}

	public override async void Pause(int position) {
		await PlayPause("pl_forcepause", "PAUSED", position);
	}

	private async Task PlayPause(string action, string state, int position) {
		await semaphoreLock.WaitAsync();
		await HttpRequest(HttpMethod.Post, $"/requests/status.json?command={action}");
		await HttpRequest(HttpMethod.Post, $"/requests/status.json?command=seek&val={position}");
		State = state;
		Position = position;
		await Task.Delay(1000);
		semaphoreLock.Release();
	}

	private async Task<HttpResponseMessage> HttpRequest(HttpMethod method, string endpoint) {
		using HttpRequestMessage requestMessage = new(method, $"{url}{endpoint}");
		requestMessage.Headers.Authorization = new("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($":{password}")));
		return await App.HttpClient.SendAsync(requestMessage);
	}

}
