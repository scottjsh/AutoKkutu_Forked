﻿namespace AutoKkutuLib.Game.Events;

public class GameModeChangeEventArgs : EventArgs
{
	public GameMode GameMode
	{
		get;
	}

	public GameModeChangeEventArgs(GameMode gameMode) => GameMode = gameMode;
}
