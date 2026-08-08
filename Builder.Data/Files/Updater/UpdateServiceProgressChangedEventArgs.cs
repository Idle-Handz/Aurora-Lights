using System;

namespace Builder.Data.Files.Updater;

public class UpdateServiceProgressChangedEventArgs : EventArgs
{
	public string StatusMessage { get; set; }

	public InformationSection Info { get; set; }

	public int ProgressPercentage { get; set; }

	public UpdateServiceProgressChangedEventArgs(string statusMessage, int progressPercentage, InformationSection information)
	{
		StatusMessage = statusMessage;
		ProgressPercentage = progressPercentage;
		Info = information;
	}
}
