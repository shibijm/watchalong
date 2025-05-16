using System;
using System.Text.Json.Serialization;
using WatchAlong.Utils;

namespace WatchAlong.Models;

public class Config {

	public string Name { get; set; } = "";
	public string WebSocketServerAddress { get; set; } = "wss://watchalong.shjm.in";
	public string Room { get; set; } = "public";
	public string MediaPlayer { get; set; } = "Jellyfin";
	public string JellyfinUrl { get; set; } = "http://localhost:8096/jellyfin";
	public string JellyfinUsername { get; set; } = "";
	public string JellyfinTokenEncrypted {
		get => jellyfinTokenEncrypted;
		set {
			jellyfinTokenEncrypted = value;
			try {
				jellyfinTokenDecrypted = Aes.CbcDecrypt(Convert.FromBase64String(jellyfinTokenEncrypted));
			} catch {
				JellyfinTokenDecrypted = "";
			}
		}
	}
	public string VlcUrl { get; set; } = "http://localhost:8080";
	public string VlcPasswordEncrypted {
		get => vlcPasswordEncrypted;
		set {
			vlcPasswordEncrypted = value;
			try {
				vlcPasswordDecrypted = Aes.CbcDecrypt(Convert.FromBase64String(vlcPasswordEncrypted));
			} catch {
				VlcPasswordDecrypted = "";
			}
		}
	}
	public bool Enabled { get; set; } = true;
	public bool JoinRoomOnStartup { get; set; } = false;
	public bool ConnectMediaPlayerOnStartup { get; set; } = false;
	public bool MinimiseToTray { get; set; } = false;

	[JsonIgnore]
	public string JellyfinTokenDecrypted {
		get => jellyfinTokenDecrypted;
		set {
			jellyfinTokenDecrypted = value;
			jellyfinTokenEncrypted = Convert.ToBase64String(Aes.CbcEncrypt(jellyfinTokenDecrypted));
		}
	}

	[JsonIgnore]
	public string VlcPasswordDecrypted {
		get => vlcPasswordDecrypted;
		set {
			vlcPasswordDecrypted = value;
			vlcPasswordEncrypted = Convert.ToBase64String(Aes.CbcEncrypt(vlcPasswordDecrypted));
		}
	}

	private string jellyfinTokenEncrypted = Convert.ToBase64String(Aes.CbcEncrypt(""));
	private string jellyfinTokenDecrypted = "";
	private string vlcPasswordEncrypted = Convert.ToBase64String(Aes.CbcEncrypt(""));
	private string vlcPasswordDecrypted = "";

}
