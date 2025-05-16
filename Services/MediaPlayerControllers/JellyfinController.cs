using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using WatchAlong.Models;
using WatchAlong.Utils;

namespace WatchAlong.Services.MediaPlayerControllers;

public class JellyfinController(string url, string token, string username) : MediaPlayerController {

	private string StateI {
		get => stateInternal;
		set {
			stateInternal = value;
			State = value;
		}
	}
	private int PositionI {
		get => positionInternal;
		set {
			positionInternal = value;
			Position = value;
		}
	}

	private readonly string url = url;
	private readonly string token = token;
	private readonly string username = username;
	private string recentSessionId = "";
	private string stateInternal = "NOT_CONNECTED";
	private int positionInternal = 0;

	public override async Task StartPolling(Action<string> statusCallback, Action<string, int> stateCallback) {
		statusCallback("Connecting");
		shouldPoll = true;
		while (shouldPoll) {
			await semaphoreLock.WaitAsync();
			try {
				HttpResponseMessage responseMessage = await HttpRequest(HttpMethod.Get, "/Sessions");
				if (!responseMessage.IsSuccessStatusCode) {
					StateI = "NOT_CONNECTED";
					PositionI = 0;
					statusCallback($"HTTP Status - {responseMessage.StatusCode}");
					continue;
				}
				string response = await responseMessage.Content.ReadAsStringAsync();
				List<JellyfinSession> sessions = JsonSerializer.Deserialize<List<JellyfinSession>>(response)!;
				if (sessions == null) {
					StateI = "NOT_CONNECTED";
					PositionI = 0;
					statusCallback("Deserialisation failed");
					continue;
				}
				sessions = [.. sessions.Where(session => session.UserName == username)];
				if (sessions.Count == 0) {
					StateI = "NOT_CONNECTED";
					PositionI = 0;
					statusCallback("No active sessions");
					continue;
				}
				JellyfinSession session = sessions.FirstOrDefault(session => session.PlayState.PlayMethod != null, sessions[0]);
				string statusPrepend = $"[{session.DeviceName}] ";
				recentSessionId = session.Id;
				if (session.PlayState.PlayMethod == null) {
					StateI = "STOPPED";
					PositionI = 0;
					statusCallback(statusPrepend + "No media playing");
					continue;
				}
				string currentState = session.PlayState.IsPaused ? "PAUSED" : "PLAYING";
				int currentPosition = (int) (session.PlayState.PositionTicks / 10000000);
				statusCallback(statusPrepend + $"{Strings.CapitaliseFirstLetter(currentState)}, {Strings.SecondsToTimestamp(currentPosition)}");
				if (!firstStateUpdate) {
					if (currentPosition <= 2 && !(StateI == "PLAYING" && currentState == "PAUSED")) {
						// Jellyfin starts reporting position at 0 seconds when resuming a title. This condition prevents the program from sending a play/pause request with the wrong position.
						State = currentState;
						Position = currentPosition;
						continue;
					}
					if (currentState != StateI || Math.Abs(PositionI - currentPosition) > seekThreshold) {
						stateCallback(currentState, currentPosition);
					}
				}
				StateI = currentState;
				PositionI = currentPosition;
			} catch (Exception e) {
				StateI = "ERROR";
				PositionI = 0;
				statusCallback($"Error: {e.Message}");
			} finally {
				firstStateUpdate = false;
				semaphoreLock.Release();
				await Task.Delay(1000);
			}
		}
		StateI = "NOT_CONNECTED";
		PositionI = 0;
	}

	public override async void Play(int position) {
		await PlayPause("Unpause", "PLAYING", position);
	}

	public override async void Pause(int position) {
		await PlayPause("Pause", "PAUSED", position);
	}

	private async Task PlayPause(string action, string state, int position) {
		await semaphoreLock.WaitAsync();
		await HttpRequest(HttpMethod.Post, $"/Sessions/{recentSessionId}/Playing/{action}");
		await HttpRequest(HttpMethod.Post, $"/Sessions/{recentSessionId}/Playing/Seek?seekPositionTicks={(long) position * 10000000}");
		StateI = state;
		PositionI = position;
		await Task.Delay(2000);
		semaphoreLock.Release();
	}

	private async Task<HttpResponseMessage> HttpRequest(HttpMethod method, string endpoint) {
		using HttpRequestMessage requestMessage = new(method, $"{url}{endpoint}");
		requestMessage.Headers.Add("X-Emby-Authorization", $"MediaBrowser Token=\"{token}\"");
		return await App.HttpClient.SendAsync(requestMessage);
	}

}
