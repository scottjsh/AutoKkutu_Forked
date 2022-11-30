﻿using Serilog;
using System;
using System.Windows;
using AutoKkutu.Utils;

namespace AutoKkutu
{
	/// <summary>
	/// ColorManagement.xaml에 대한 상호 작용 논리
	/// </summary>
	public partial class ColorManagement : Window
	{
		public ColorManagement(AutoKkutuColorPreference config)
		{
			if (config == null)
				throw new ArgumentNullException(nameof(config));

			InitializeComponent();
			EndWordColor.SelectedColor = config.EndWordColor;
			AttackWordColor.SelectedColor = config.AttackWordColor;
			MissionWordColor.SelectedColor = config.MissionWordColor;
			EndMissionWordColor.SelectedColor = config.EndMissionWordColor;
			AttackMissionWordColor.SelectedColor = config.AttackMissionWordColor;
		}

		private void OnSubmit(object sender, RoutedEventArgs e)
		{
			var newPref = new AutoKkutuColorPreference
			{
				EndWordColor = EndWordColor.SelectedColor ?? AutoKkutuColorPreference.DefaultEndWordColor,
				AttackWordColor = AttackWordColor.SelectedColor ?? AutoKkutuColorPreference.DefaultAttackWordColor,
				MissionWordColor = MissionWordColor.SelectedColor ?? AutoKkutuColorPreference.DefaultMissionWordColor,
				EndMissionWordColor = EndMissionWordColor.SelectedColor ?? AutoKkutuColorPreference.DefaultEndMissionWordColor,
				AttackMissionWordColor = AttackMissionWordColor.SelectedColor ?? AutoKkutuColorPreference.DefaultAttackMissionWordColor,
			};

			try
			{
				Settings config = Settings.Default;
				config.EndWordColor = newPref.EndWordColor.ToDrawingColor();
				config.AttackWordColor = newPref.AttackWordColor.ToDrawingColor();
				config.MissionWordColor = newPref.MissionWordColor.ToDrawingColor();
				config.EndMissionWordColor = newPref.EndMissionWordColor.ToDrawingColor();
				config.AttackMissionWordColor = newPref.AttackMissionWordColor.ToDrawingColor();
				Settings.Default.Save();
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Failed to save the configuration.");
			}

			AutoKkutuMain.ColorPreference = newPref;
			Close();
		}

		private void OnCancel(object sender, RoutedEventArgs e) => Close();
	}
}
