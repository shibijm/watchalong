using System;
using System.Threading;
using System.Threading.Tasks;

namespace WatchAlong.Services.MediaPlayerControllers;

public abstract class MediaPlayerController {

	public string State { get; protected set; } = "NOT_CONNECTED";
	public int Position { get; protected set; } = 0;
	public bool IsReady => State is "PLAYING" or "PAUSED";

	protected readonly SemaphoreSlim semaphoreLock = new(1, 1);
	protected bool shouldPoll = false;
	protected bool firstStateUpdate = true;

	protected readonly int seekThreshold = 4;

	public abstract Task StartPolling(Action<string> statusCallback, Action<string, int> stateCallback);

	public void StopPolling() {
		shouldPoll = false;
	}

	public abstract void Play(int position);

	public abstract void Pause(int position);

}
