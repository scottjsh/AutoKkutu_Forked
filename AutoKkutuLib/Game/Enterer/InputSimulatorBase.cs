﻿using AutoKkutuLib.Hangul;
using Serilog;
using System.Collections.Immutable;

namespace AutoKkutuLib.Game.Enterer;
public abstract class InputSimulatorBase : EntererBase
{
	public InputSimulatorBase(EntererMode mode, IGame game) : base(mode, game)
	{
	}

	protected abstract Task AppendAsync(EnterOptions options, InputCommand input);
	protected virtual void SimulationStarted() { }
	protected virtual void SimulationFinished() { }

	protected override async Task SendAsync(EnterInfo info)
	{
		isPreinputSimInProg = info.HasFlag(PathFlags.PreSearch);
		IsPreinputFinished = false;

		var content = info.Content;
		var valid = true;

		var list = new List<HangulSplit>();
		foreach (var ch in content)
			list.Add(HangulSplit.Parse(ch));

		var recomp = new HangulRecomposer(KeyboardLayout.QWERTY, list.ToImmutableList()); // TODO: Make KeyboardLayout configurable with AutoEnterOptions
		IImmutableList<InputCommand> inputList = recomp.Recompose();

		var startDelay = info.Options.GetStartDelay();
		await Task.Delay(startDelay);

		Log.Information(I18n.Main_InputSimulating, content);
		game.UpdateChat("");

		foreach (InputCommand input in inputList)
		{
			Log.Debug("Input requested: {ipt}", input);
			if (!CanPerformAutoEnterNow(info.PathInfo, !isPreinputSimInProg))
			{
				valid = false; // Abort
				break;
			}

			var delay = info.Options.GetDelayPerChar();
			await AppendAsync(info.Options, input);
			await Task.Delay(delay);
		}

		if (isPreinputSimInProg) // As this function runs asynchronously, this value could have been changed.
		{
			isPreinputSimInProg = false;
			IsPreinputFinished = true;
			return; // Don't submit yet
		}

		TrySubmit(valid);
	}
}
