﻿namespace AutoKkutuLib.Constants;
public static class EnumValuesHolder
{
	public static DatabaseUpdateTiming[] GetDBAutoUpdateModeValues() => (DatabaseUpdateTiming[])Enum.GetValues(typeof(DatabaseUpdateTiming));

	public static WordCategories[] GetWordPreferenceValues() => (WordCategories[])Enum.GetValues(typeof(WordCategories));

	public static GameMode[] GetGameModeValues() => (GameMode[])Enum.GetValues(typeof(GameMode));
}
