using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WatchAlong.Models;
using WatchAlong.Services.MediaPlayerControllers;
using WatchAlong.Services;
using WatchAlong.Utils;

namespace WatchAlong.UI;

public partial class MainWindow : Window {

	private MediaPlayerController? mpc;
	private WebSocketClient? ws;
	private bool rendered = false;
	private readonly ObservableCollection<User> users = new();
	private readonly List<Control> jellyfinControls;
	private readonly List<Control> vlcControls;
	private readonly List<Control> wsControls;
	private readonly List<Control> mpcControls;
	private readonly List<string> mediaPlayers = new();
	private readonly System.Windows.Forms.NotifyIcon notifyIcon = new();
	private bool reconnecting = false;

	public MainWindow() {
		InitializeComponent();
		jellyfinControls = new() { JellyfinUrlBox, JellyfinUsernameLabel, JellyfinUsernameBox, JellyfinTokenBox };
		vlcControls = new() { VlcUrlBox, VlcPasswordBox };
		wsControls = new() { NameBox, WSServerAddressBox, RoomBox, JoinButton };
		mpcControls = new() { MediaPlayerComboBox };
		mpcControls.AddRange(jellyfinControls);
		mpcControls.AddRange(vlcControls);
		foreach (ComboBoxItem item in MediaPlayerComboBox.Items) {
			mediaPlayers.Add((string) item.Content);
		}
		if (!mediaPlayers.Contains(App.Config.MediaPlayer)) {
			App.Config.MediaPlayer = mediaPlayers[0];
			App.ConfigService.SaveConfig();
		}
		DataContext = App.Config;
		JellyfinTokenBox.Password = App.Config.JellyfinTokenDecrypted;
		VlcPasswordBox.Password = App.Config.VlcPasswordDecrypted;
		MediaPlayerComboBox.SelectedIndex = mediaPlayers.IndexOf(App.Config.MediaPlayer);
		UsersListBox.ItemsSource = users;
		if (App.Config.JoinRoomOnStartup) {
			JoinButton_Click(null, null);
		}
		if (App.Config.ConnectMediaPlayerOnStartup) {
			ConnectMediaPlayerButton_Click(null, null);
		}
		SendMediaStateUpdateContinuously();
		notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
		notifyIcon.Text = App.Name;
		notifyIcon.Visible = true;
		notifyIcon.MouseClick += NotifyIcon_Click;
		notifyIcon.ContextMenuStrip = new();
		notifyIcon.ContextMenuStrip.Items.Add("Exit", null, StartExitProcess);
	}

	private void NotifyIcon_Click(object? sender, System.Windows.Forms.MouseEventArgs e) {
		if (e.Button != System.Windows.Forms.MouseButtons.Left) {
			return;
		}
		if (Visibility == Visibility.Visible) {
			Hide();
		} else {
			Show();
			Activate();
		}
	}

	private void Window_ContentRendered(object sender, EventArgs e) {
		rendered = true;
	}

	private void BoundTextBox_TextChanged(object sender, TextChangedEventArgs e) {
		if (rendered) {
			App.ConfigService.SaveConfig();
		}
	}

	private void BoundCheckBox_Click(object sender, RoutedEventArgs e) {
		App.ConfigService.SaveConfig();
	}

	private void JellyfinTokenBox_PasswordChanged(object sender, RoutedEventArgs e) {
		if (rendered) {
			App.Config.JellyfinTokenDecrypted = JellyfinTokenBox.Password;
			App.ConfigService.SaveConfig();
		}
	}

	private void VlcPasswordBox_PasswordChanged(object sender, RoutedEventArgs e) {
		if (rendered) {
			App.Config.VlcPasswordDecrypted = VlcPasswordBox.Password;
			App.ConfigService.SaveConfig();
		}
	}

	private void WsTabs_SelectionChanged(object sender, SelectionChangedEventArgs e) {
		if (rendered && LogsTab.IsSelected) {
			LogBox.ScrollToEnd();
		}
	}

	private static void SetControlsVisible(List<Control> controls, bool visible) {
		foreach (Control control in controls) {
			control.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
		}
	}

	private static void SetControlsEnabled(List<Control> controls, bool enabled) {
		foreach (Control control in controls) {
			control.IsEnabled = enabled;
		}
	}

	private void MediaPlayerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
		if (rendered) {
			App.Config.MediaPlayer = mediaPlayers[MediaPlayerComboBox.SelectedIndex];
			App.ConfigService.SaveConfig();
		}
		switch (App.Config.MediaPlayer) {
			case "Jellyfin":
				SetControlsVisible(jellyfinControls, true);
				SetControlsVisible(vlcControls, false);
				TokenOrPasswordLabel.Content = "Token";
				break;
			case "VLC":
				SetControlsVisible(jellyfinControls, false);
				SetControlsVisible(vlcControls, true);
				TokenOrPasswordLabel.Content = "Password";
				break;
		}
	}

	private void Log(string message) {
		if (!string.IsNullOrEmpty(LogBox.Text)) {
			LogBox.AppendText(Environment.NewLine);
		}
		LogBox.AppendText($"[{DateTime.Now:t}] {message}");
		LogBox.ScrollToEnd();
	}

	private async void JoinButton_Click(object? sender, RoutedEventArgs? e) {
		if (reconnecting) { // TODO: Don't break on quick reconnect cancellation + new reconnecting connection
			SetControlsEnabled(wsControls, true);
			JoinButton.Content = "Join";
			reconnecting = false;
			Log("Reconnect cancelled");
			return;
		}
		if (ws != null && ws.IsConnected) {
			JoinButton.IsEnabled = false;
			await ws.Disconnect();
			return;
		}
		bool connected = false;
		bool error = false;
		SetControlsEnabled(wsControls, false);
		JoinButton.Content = "Connecting";
		try {
			Log("Connecting");
			ws = new();
			await ws.Connect(App.Config.WebSocketServerAddress);
			Log("Connected");
			connected = true;
			await ws.JoinRoom(App.Config.Room, App.Config.Name);
			await ws.LifeCycle(HandleWsEnvelope);
		} catch (Exception exc) {
			App.Log(exc.ToString());
			Log(exc.Message);
			error = true;
		}
		users.Clear();
		ws = null;
		if (connected) {
			Log("Disconnected");
		}
		if (error) {
			JoinButton.IsEnabled = true;
			JoinButton.Content = "Cancel Reconnect";
			reconnecting = true;
			Log("Reconnecting in 5 seconds");
			await Task.Delay(5000);
			if (reconnecting) {
				reconnecting = false;
				JoinButton_Click(sender, e);
			}
		} else {
			SetControlsEnabled(wsControls, true);
			JoinButton.Content = "Join";
		}
	}

	public async Task HandleWsEnvelope(WebSocketEnvelope envelope) {
		if (ws == null || !ws.IsConnected) {
			return;
		}
		switch (envelope.Type) {
			case "HANDSHAKE":
				Log($"Joined room {App.Config.Room}");
				JoinButton.IsEnabled = true;
				JoinButton.Content = "Leave";
				await ws.Send(new() { Type = "USERS" });
				break;
			default:
				if (envelope.Data is null) {
					return;
				}
				switch (envelope.Type) {
					case "USERS":
						foreach (dynamic u in envelope.Data.EnumerateArray()) {
							users.Add(new(u.GetProperty("name").GetString()));
						}
						break;
					case "USER_JOINED":
						string name = envelope.Data.GetProperty("name").GetString();
						Log($"{name} joined");
						users.Add(new(name));
						break;
					case "USER_LEFT":
						name = envelope.Data.GetProperty("name").GetString();
						string reason = envelope.Data.GetProperty("reason").GetString();
						Log($"{name} left ({reason})");
						users.Remove(users.Where(user => user.Name == name).First());
						break;
					case "CONTROL_MEDIA":
						string requestingUserName = envelope.Data.GetProperty("requestingUser").GetProperty("name").GetString();
						string action = envelope.Data.GetProperty("action").GetString();
						Log($"{Strings.CapitaliseFirstLetter(action)} request received from {requestingUserName}");
						if (mpc == null || !mpc.IsReady) {
							Log("Media player not ready");
							return;
						}
						if (!App.Config.AutoSync) {
							return;
						}
						int position = envelope.Data.GetProperty("position").GetInt32();
						string timestamp = Strings.SecondsToTimestamp(position);
						switch (action) {
							case "PLAY":
								Log($"Playing from {timestamp}");
								mpc.Play(position);
								break;
							case "PAUSE":
								Log($"Pausing at {timestamp}");
								mpc.Pause(position);
								break;
						}
						break;
					case "MEDIA_STATE":
						string userName = envelope.Data.GetProperty("user").GetProperty("name").GetString();
						string state = envelope.Data.GetProperty("state").GetString();
						position = envelope.Data.GetProperty("position").GetInt32();
						users.Where(user => user.Name == userName).First().UpdateText(state, position);
						break;
				}
				break;
		}
	}

	public async void SendMediaStateUpdateContinuously() {
		while (true) {
			try {
				if (ws == null || !ws.IsConnected) {
					continue;
				}
				string state = mpc?.State ?? "NOT_CONNECTED";
				int position = mpc?.Position ?? 0;
				await ws.Send(new() { Type = "MEDIA_STATE", Data = new { state, position } });
				users.Where(user => user.Name == App.Config.Name).FirstOrDefault()?.UpdateText(state, position);
			} catch (Exception e) {
				App.Log(e.ToString());
			} finally {
				await Task.Delay(3000);
			}
		}
	}

	private async void ConnectMediaPlayerButton_Click(object? sender, RoutedEventArgs? e) {
		if (mpc != null) {
			mpc.StopPolling();
			ConnectMediaPlayerButton.IsEnabled = false;
			return;
		}
		SetControlsEnabled(mpcControls, false);
		ConnectMediaPlayerButton.Content = "Disconnect";
		mpc = App.Config.MediaPlayer switch {
			"Jellyfin" => new JellyfinController(App.Config.JellyfinUrl, App.Config.JellyfinTokenDecrypted, App.Config.JellyfinUsername),
			"VLC" => new VlcController(App.Config.VlcUrl, App.Config.VlcPasswordDecrypted),
			_ => throw new Exception("Invalid media player"),
		};
		await mpc.StartPolling(HandleMpcStatusUpdate, HandleMpcStateChange);
		SetControlsEnabled(mpcControls, true);
		ConnectMediaPlayerButton.IsEnabled = true;
		ConnectMediaPlayerButton.Content = "Connect";
		HandleMpcStatusUpdate("Disconnected");
		mpc = null;
	}

	private void HandleMpcStatusUpdate(string status) {
		StatusLabel.Text = status;
	}

	private async void HandleMpcStateChange(string state, int position) {
		if (ws == null || !ws.IsConnected || !App.Config.AutoSync) {
			return;
		}
		string action = state == "PLAYING" ? "PLAY" : "PAUSE";
		Log($"{Strings.CapitaliseFirstLetter(action)} request sent ({Strings.SecondsToTimestamp(position)})");
		await ws.Send(new() {
			Type = "CONTROL_MEDIA",
			Data = new { action, position }
		});
	}

	private void Exit() {
		notifyIcon.Dispose();
		Application.Current.Shutdown();
	}

	private void StartExitProcess(object? sender, EventArgs e) {
		if (ws != null && ws.IsConnected) {
			ws.Disconnect().ContinueWith((Task task) => Dispatcher.Invoke(Exit));
		} else {
			Exit();
		}
	}

	private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
		e.Cancel = true;
		if (App.Config.MinimiseToTray) {
			Hide();
		} else {
			StartExitProcess(sender, e);
		}
	}

}
