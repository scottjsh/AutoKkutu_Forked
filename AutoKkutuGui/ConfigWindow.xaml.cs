﻿using AutoKkutuGui.Config;
using AutoKkutuLib;
using AutoKkutuLib.Game.Enterer;
using Serilog;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace AutoKkutuGui;

/// <summary>
/// ConfigWindow.xaml에 대한 상호 작용 논리
/// </summary>
public partial class ConfigWindow : Window
{
	private readonly ChoosableReorderableList<PreferenceItem> PreferenceReorderList;

	private EntererMode GetAutoEnterMode()
	{
		if (AutoEnterInstant.IsChecked ?? false)
			return EntererMode.EnterImmediately;
		if (AutoEnterInputSimulateJavaScript.IsChecked ?? false)
			return EntererMode.SimulateInputJavaScript;
		if (AutoEnterInputSimulateWin32.IsChecked ?? false)
			return EntererMode.SimulateInputWin32;
		if (AutoEnterInputSimulateArduino.IsChecked ?? false)
			return EntererMode.SimulateInputArduino;
		throw new InvalidOperationException("None of the AutoEnter options are selected");
	}

	private void SetAutoEnterMode(EntererMode mode)
	{
		// Reset selection
		AutoEnterInstant.IsChecked = false;
		AutoEnterInputSimulateJavaScript.IsChecked = false;
		AutoEnterInputSimulateWin32.IsChecked = false;
		AutoEnterInputSimulateArduino.IsChecked = false;

		switch (mode)
		{
			case EntererMode.EnterImmediately:
				AutoEnterInstant.IsChecked = true;
				break;
			case EntererMode.SimulateInputJavaScript:
				AutoEnterInputSimulateJavaScript.IsChecked = true;
				break;
			case EntererMode.SimulateInputWin32:
				AutoEnterInputSimulateWin32.IsChecked = true;
				break;
			case EntererMode.SimulateInputArduino:
				AutoEnterInputSimulateArduino.IsChecked = true;
				break;
			default:
				break;
		}
	}

	public ConfigWindow(Preference config)
	{
		if (config == null)
			throw new ArgumentNullException(nameof(config));

		InitializeComponent();

		// 단어 검색
		AttackWord.IsChecked = config.AttackWordEnabled;
		EndWord.IsChecked = config.EndWordEnabled;
		ReturnMode.IsChecked = config.ReturnModeEnabled;
		MissionDetection.IsChecked = config.MissionAutoDetectionEnabled;
		MaxWordCount.Text = config.MaxDisplayedWordCount.ToString(CultureInfo.InvariantCulture);

		// 자동 단어 입력
		AutoEnter.IsChecked = config.AutoEnterEnabled;
		Delay.IsChecked = config.AutoEnterDelayEnabled;
		StartDelayNumber.Text = config.AutoEnterStartDelay.ToString(CultureInfo.InvariantCulture);
		StartDelayRandomNumber.Text = config.AutoEnterStartDelayRandom.ToString(CultureInfo.InvariantCulture);
		DelayPerCharNumber.Text = config.AutoEnterDelayPerChar.ToString(CultureInfo.InvariantCulture);
		DelayPerCharRandomNumber.Text = config.AutoEnterDelayPerCharRandom.ToString(CultureInfo.InvariantCulture);

		SetAutoEnterMode(config.AutoEnterMode);
		DelayStartAfterWordEnter.IsChecked = config.AutoEnterDelayStartAfterWordEnterEnabled;
		AutoEnterInputSimulateJavaScriptSendKeys.IsChecked = config.AutoEnterInputSimulateJavaScriptSendKeys;

		AutoFix.IsChecked = config.AutoFixEnabled;
		FixDelay.IsChecked = config.FixDelayEnabled;
		FixStartDelayNumber.Text = config.FixStartDelay.ToString(CultureInfo.InvariantCulture);
		FixStartDelayRandomNumber.Text = config.FixStartDelayRandom.ToString(CultureInfo.InvariantCulture);
		FixDelayPerCharNumber.Text = config.FixDelayPerChar.ToString(CultureInfo.InvariantCulture);
		FixDelayPerCharRandomNumber.Text = config.FixDelayPerCharRandom.ToString(CultureInfo.InvariantCulture);

		RandomWordSelection.IsChecked = config.RandomWordSelection;
		RandomWordSelectionCount.Text = config.RandomWordSelectionCount.ToString(CultureInfo.InvariantCulture);

		// 데이터베이스 자동 업데이트
		DBAutoUpdate.IsChecked = config.AutoDBUpdateEnabled;
		DBAutoWordAdd.IsChecked = config.AutoDBWordAddEnabled;
		DBAutoWordRemove.IsChecked = config.AutoDBWordRemoveEnabled;
		DBAutoWordAddEnd.IsChecked = config.AutoDBAddEndEnabled;

		// 우회 관련
		HijackACPackets.IsChecked = config.HijackACPackets;

		SimulateAntiCheat.IsChecked = config.SimulateAntiCheat;
		DetectInputLogging.IsChecked = config.DetectInputLogging;
		LogChatting.IsChecked = config.LogChatting;
		SelfAntiCheat.IsChecked = config.SelfAntiCheat;

		PreferenceReorderList = new ChoosableReorderableList<PreferenceItem>(new ChoosableReorderableListUIElements(InactivePreferences, ActivePreferences, ActivatePreferenceButton, DeactivatePreferenceButton, MoveUpPreferenceButton, MoveDownPreferenceButton), "Name");

		if (config.ActiveWordPreference != null)
		{
			foreach (WordCategories attr in config.ActiveWordPreference.GetAttributes())
				PreferenceReorderList.AddActive(new PreferenceItem(attr, WordPreference.GetName(attr)));
		}

		if (config.InactiveWordPreference != null)
		{
			foreach (WordCategories attr in config.InactiveWordPreference.GetAttributes())
				PreferenceReorderList.AddInactive(new PreferenceItem(attr, WordPreference.GetName(attr)));
		}
	}

	private static int Parse(string elementName, string valueString, int defaultValue)
	{
		if (int.TryParse(valueString, out var _delay))
			return _delay;

		Log.Warning("Can't parse {elemName} value {string}, will be reset to {default:l}.", elementName, valueString, defaultValue);
		return defaultValue;
	}

	private void OnApply(object sender, RoutedEventArgs e)
	{
		var maxWordCount = Parse("max word count", MaxWordCount.Text, 20);

		var startDelay = Parse("start delay", StartDelayNumber.Text, 10);
		var startDelayRandom = Parse("start delay randomization", StartDelayRandomNumber.Text, 10);
		var delayPerChar = Parse("delay per char", DelayPerCharNumber.Text, 10);
		var delayPerCharRandom = Parse("delay per char randomization", DelayPerCharRandomNumber.Text, 10);

		var fixStartDelay = Parse("fix start delay", FixStartDelayNumber.Text, 10);
		var fixStartDelayRandom = Parse("fix start delay randomization", FixStartDelayRandomNumber.Text, 10);
		var fixDelayPerChar = Parse("fix delay per char", FixDelayPerCharNumber.Text, 10);
		var fixDelayPerCharRandom = Parse("fix delay per char randomization", FixDelayPerCharRandomNumber.Text, 10);

		var rwsSelect = Parse("random word selection", RandomWordSelectionCount.Text, 5);

		var conf = new Preference
		{
			// 단어 검색
			EndWordEnabled = EndWord.IsChecked ?? false,
			AttackWordEnabled = AttackWord.IsChecked ?? false,
			ReturnModeEnabled = ReturnMode.IsChecked ?? false,
			MissionAutoDetectionEnabled = MissionDetection.IsChecked ?? false,
			MaxDisplayedWordCount = maxWordCount,

			// 자동 단어 입력
			AutoEnterEnabled = AutoEnter.IsChecked ?? false,
			AutoEnterDelayEnabled = Delay.IsChecked ?? false,
			AutoEnterStartDelay = startDelay,
			AutoEnterStartDelayRandom = startDelayRandom,
			AutoEnterDelayPerChar = delayPerChar,
			AutoEnterDelayPerCharRandom = delayPerCharRandom,

			AutoEnterMode = GetAutoEnterMode(),
			AutoEnterDelayStartAfterWordEnterEnabled = DelayStartAfterWordEnter.IsChecked ?? false,
			AutoEnterInputSimulateJavaScriptSendKeys = AutoEnterInputSimulateJavaScriptSendKeys.IsChecked ?? false,

			AutoFixEnabled = AutoFix.IsChecked ?? false,
			FixDelayPerChar = fixDelayPerChar,
			FixDelayEnabled = FixDelay.IsChecked ?? false,
			FixStartDelay = fixStartDelay,
			FixStartDelayRandom = fixStartDelayRandom,
			FixDelayPerCharRandom = fixDelayPerCharRandom,

			RandomWordSelection = RandomWordSelection.IsChecked ?? false,
			RandomWordSelectionCount = rwsSelect,

			// 데이터베이스 자동 업데이트
			AutoDBUpdateEnabled = DBAutoUpdate.IsChecked ?? false,
			AutoDBWordAddEnabled = DBAutoWordAdd.IsChecked ?? false,
			AutoDBWordRemoveEnabled = DBAutoWordRemove.IsChecked ?? false,
			AutoDBAddEndEnabled = DBAutoWordAddEnd.IsChecked ?? false,

			// 우회 관련
			HijackACPackets = HijackACPackets.IsChecked ?? false,
			SimulateAntiCheat = SimulateAntiCheat.IsChecked ?? false,
			DetectInputLogging = DetectInputLogging.IsChecked ?? false,
			LogChatting = LogChatting.IsChecked ?? false,
			SelfAntiCheat = SelfAntiCheat.IsChecked ?? false,

			InactiveWordPreference = new WordPreference(PreferenceReorderList.GetInactiveItemArray().Select(s => s.NodeType).ToArray()),
			ActiveWordPreference = new WordPreference(PreferenceReorderList.GetActiveItemArray().Select(s => s.NodeType).ToArray()),
		};

		try
		{
			// FIXME: 프로그램 설정을 저장하면 색 강조 설정이 초기화됨(날라감). 그 반대도 마찬가지.
			Settings config = Settings.Default;

			// 단어 검색
			config.EndWordEnabled = conf.EndWordEnabled;
			config.AttackWordEnabled = conf.AttackWordEnabled;
			config.ReturnModeEnabled = conf.ReturnModeEnabled;
			config.MissionAutoDetectionEnabled = conf.MissionAutoDetectionEnabled;
			config.MaxDisplayedWordCount = conf.MaxDisplayedWordCount;

			// 자동 단어 입력
			config.AutoEnterEnabled = conf.AutoEnterEnabled;
			config.DelayEnabled = conf.AutoEnterDelayEnabled;
			config.StartDelay = conf.AutoEnterStartDelay;
			config.StartDelayRandom = conf.AutoEnterStartDelayRandom;
			config.DelayPerChar = conf.AutoEnterDelayPerChar;
			config.DelayPerCharRandom = conf.AutoEnterDelayPerCharRandom;

			config.AutoEnterMode = (int)conf.AutoEnterMode;
			config.DelayStartAfterWordEnterEnabled = conf.AutoEnterDelayStartAfterWordEnterEnabled;
			config.AutoEnterInputSimulateJavaScriptSendKeys = conf.AutoEnterInputSimulateJavaScriptSendKeys;

			config.AutoFixEnabled = conf.AutoFixEnabled;
			config.FixDelayEnabled = conf.FixDelayEnabled;
			config.FixStartDelay = conf.FixStartDelay;
			config.FixStartDelayRandom = conf.FixStartDelayRandom;
			config.FixDelayPerChar = conf.FixDelayPerChar;
			config.FixDelayPerCharRandom = conf.FixDelayPerCharRandom;

			config.RandomWordSelectionCount = conf.RandomWordSelectionCount;
			config.RandomWordSelection = conf.RandomWordSelection;

			// 데이터베이스 자동 업데이트
			config.AutoDBUpdateEnabled = conf.AutoDBUpdateEnabled;
			config.AutoDBWordAddEnabled = conf.AutoDBWordAddEnabled;
			config.AutoDBWordRemoveEnabled = conf.AutoDBWordRemoveEnabled;
			config.AutoDBAddEndEnabled = conf.AutoDBAddEndEnabled;

			// 우회 관련
			config.HijackACPackets = conf.HijackACPackets;
			config.SimulateAntiCheat = conf.SimulateAntiCheat;
			config.DetectInputLogging = conf.DetectInputLogging;
			config.LogChatting = conf.LogChatting;
			config.SelfAntiCheat = conf.SelfAntiCheat;

			// 단어 우선순외
			config.ActiveWordPreference = conf.ActiveWordPreference;
			config.InactiveWordPreference = conf.InactiveWordPreference;

			// 더 이상 사용되지 않는 옵션들
			config.DelayInMillis = -1;
			config.DelayPerCharEnabled = false;
			config.FixDelayInMillis = -1;
			config.FixDelayPerCharEnabled = false;

			config.Save();
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to save the configuration.");
		}

		Main.Prefs = conf;
		Close();
	}

	private void OnCancel(object sender, RoutedEventArgs e) => Close();

	//https://stackoverflow.com/a/10238715
	private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
	{
		Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
		e.Handled = true;
	}
}

public sealed class PreferenceItem : IEquatable<PreferenceItem>
{
	public string Name
	{
		get;
	}

	public WordCategories NodeType
	{
		get;
	}

	public PreferenceItem(WordCategories nodeType, string name)
	{
		NodeType = nodeType;
		Name = name;
	}

	public override int GetHashCode() => HashCode.Combine(Name.GetHashCode(StringComparison.OrdinalIgnoreCase), NodeType.GetHashCode());

	public override bool Equals(object? obj) => obj is PreferenceItem other && Equals(other);

	public bool Equals(PreferenceItem? other) => other != null && Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase) && NodeType == other.NodeType;
}
