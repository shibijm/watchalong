using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WatchAlong.Models;

namespace WatchAlong.Services;

// TODO: Ping timeout
public class WebSocketClient {

	public bool IsConnected => ws.State == WebSocketState.Open;

	private readonly ClientWebSocket ws = new();

	public async Task Connect(string address) {
		await ws.ConnectAsync(new(address), CancellationToken.None);
	}

	public async Task Disconnect() {
		await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
	}

	public async Task JoinRoom(string room, string name) {
		await Send(new() { Type = "HANDSHAKE", Data = new { room, name } });
	}

	public async Task LifeCycle(Func<WebSocketEnvelope, Task> envelopeHandler) {
		while (IsConnected) {
			WebSocketEnvelope? envelope = await Receive();
			if (envelope == null) {
				continue;
			}
			try {
				switch (envelope.Type) {
					case "PING":
						await Send(new() { Type = "PONG" });
						break;
					default:
						await envelopeHandler(envelope);
						break;
				}
			} catch (Exception e) {
				App.Log(e.ToString());
				App.DisplayError($"Error while handling websocket envelope type {envelope.Type}:\n{e.Message}");
			}
		}
		if (ws.CloseStatus != WebSocketCloseStatus.NormalClosure) {
			throw new(
				$"The WebSocket connection has been closed abnormally"
				+ $"{(ws.CloseStatus != null ? $" ({ws.CloseStatus})" : "")}."
				+ $"{(!string.IsNullOrEmpty(ws.CloseStatusDescription) ? $"\n{ws.CloseStatusDescription}" : "")}"
			);
		}
	}

	public async Task Send(WebSocketEnvelope data) {
		if (!IsConnected) {
			return;
		}
		await ws.SendAsync(
			new(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data))),
			WebSocketMessageType.Text,
			true,
			CancellationToken.None
		);
	}

	private async Task<WebSocketEnvelope?> Receive() {
		ArraySegment<byte> envelopeBytes = new(new byte[1024]);
		WebSocketReceiveResult receiveResult = await ws.ReceiveAsync(envelopeBytes, CancellationToken.None);
		if (ws.State == WebSocketState.CloseReceived) {
			await Disconnect();
			return null;
		}
		if (envelopeBytes.Array == null) {
			return null;
		}
		try {
			return JsonSerializer.Deserialize<WebSocketEnvelope>(Encoding.UTF8.GetString(envelopeBytes.Array, 0, receiveResult.Count));
		} catch {
			return null;
		}
	}

}
