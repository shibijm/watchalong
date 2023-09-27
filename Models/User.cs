using System;
using System.ComponentModel;
using System.Timers;
using WatchAlong.Utils;

namespace WatchAlong.Models;

public class User : INotifyPropertyChanged {

	public event PropertyChangedEventHandler? PropertyChanged;

	public string Name { get; }
	public string Text => mediaState is "PLAYING" or "PAUSED" ? $"{Name}\n{Strings.CapitaliseFirstLetter(mediaState)}, {MediaTimestamp}" : !string.IsNullOrEmpty(mediaState) ? $"{Name}\nMedia State: {Strings.CapitaliseFirstLetter(mediaState).Replace("_", " ")}" : Name;
	private string MediaTimestamp => Strings.SecondsToTimestamp(mediaPosition);

	private string mediaState = "";
	private int mediaPosition = 0;
	private readonly Timer timer = new(1000) { AutoReset = true };

	public User(string name) {
		Name = name;
		timer.Elapsed += IncrementPosition;
	}

	private void IncrementPosition(object? sender, ElapsedEventArgs args) {
		mediaPosition++;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
	}

	public void UpdateText(string state, int position) {
		mediaState = state;
		if ((state == "PLAYING" && Math.Abs(mediaPosition - position) > 1) || state != "PLAYING") {
			timer.Stop();
			mediaPosition = position;
		}
		if (state == "PLAYING" && !timer.Enabled) {
			timer.Start();
		}
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
	}

}
