using System;

namespace Builder.Data.Files.Updater;

public class StatusChangedEventArgs : EventArgs
{
	public string Message { get; set; }

	public int ProgressPercentage { get; set; }
}
