namespace WatchAlong.Models;

public class JellyfinSession {

	public required string Id { get; set; }
	public required JellyfinPlayState PlayState { get; set; }
	public required string UserName { get; set; }
	public required string Client { get; set; }
	public required string DeviceName { get; set; }

}
