using System;

namespace WatchAlong.Utils;

public static class Strings {

	public static string CapitaliseFirstLetter(string word) {
		return word[0] + word[1..].ToLower();
	}

	public static string SecondsToTimestamp(int seconds) {
		TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);
		return timeSpan.ToString(@$"{(timeSpan.Hours > 0 ? @"hh\:" : "")}mm\:ss");
	}

}
