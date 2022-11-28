﻿using System;
using System.Windows;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace AutoKkutu
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		private const int MaxSizeBytes = 8388608; // 64 MB
		private const string LoggingTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.ffff} [{Level:u3}] <{ThreadName} #{ThreadId}> {Message:lj}{NewLine}{Exception}";
		private readonly TimeSpan FlushPeriod = TimeSpan.FromSeconds(1);

		public App()
		{
			try
			{
				Log.Logger = new LoggerConfiguration()
					.MinimumLevel.Verbose()
					.WriteTo.Console(outputTemplate: LoggingTemplate, theme: AnsiConsoleTheme.Code)
					.WriteTo.Async(c => c.File(path: "AutoKkutu.log", outputTemplate: LoggingTemplate, fileSizeLimitBytes: MaxSizeBytes, rollOnFileSizeLimit: true, buffered: true, flushToDiskInterval: FlushPeriod))
					.CreateLogger();
			}
			catch (Exception e)
			{
				MessageBox.Show("Failed to initialize logger:" + e.ToString(), "Logger initialization failure", MessageBoxButton.OK, MessageBoxImage.Error);
				Shutdown(); // Can't continue execution
			}
		}
	}
}
