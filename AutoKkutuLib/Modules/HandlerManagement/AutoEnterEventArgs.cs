﻿namespace AutoKkutuLib.Modules.HandlerManagement;

public class AutoEnterEventArgs : EventArgs
{
	public string Content
	{
		get;
	}

	public AutoEnterEventArgs(string content) => Content = content;
}
