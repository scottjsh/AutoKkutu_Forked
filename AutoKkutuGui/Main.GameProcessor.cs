﻿//#define SELENIUM
using AutoKkutuLib;
using AutoKkutuLib.Database.Jobs;
using AutoKkutuLib.Database.Path;
using AutoKkutuLib.Extension;
using AutoKkutuLib.Game;
using AutoKkutuLib.Game.Enterer;
using Serilog;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace AutoKkutuGui;

public partial class Main
{
	/// <summary>
	/// 마지막 Pre-search 결과를 나타냅니다. 마지막 Pre-search가 실패하였거나, 실행 조건이 만족되지 않은 경우 <c>null</c>로 설정됩니다.
	/// </summary>
	/// <remarks>
	/// 턴이 시작될 때, 만약 이 검색 결과가 턴 조건과 일치한다면, 새로운 검색을 시작하는 대신 이 검색 결과를 사용함으로서 지연을 줄일 수 있습니다.
	/// </remarks>
	private PathUpdateEventArgs? preSearch;

	/// <summary>
	/// 마지막으로 자동 검색된 단어 목록을 나타냅니다.
	/// </summary>
	/// <remarks>
	/// 오답 자동 수정을 위해서 사용될 목적으로 설계되었습니다.
	/// </remarks>
	private PathUpdateEventArgs? autoPathFindCache;

	// TODO: 내가 이번 턴에 패배했고, 라운드가 끝났을 경우
	// 다음 라운드 시작 단어에 대해 미리 Pre-search 및 입력 수행.
	private void OnRoundChanged(object? sender, EventArgs e) => preSearch = null; // Invalidate pre-search result on round changed

	private void OnPathUpdated(object? sender, PathUpdateEventArgs args)
	{
		Log.Verbose(I18n.Main_PathUpdateReceived);
		if (!args.HasFlag(PathFlags.DoNotAutoEnter))
			autoPathFindCache = args;

		if (args.HasFlag(PathFlags.PreSearch))
		{
			// 내 턴이 이미 시작한 상태에서 Pre-search 결과 입력을 시작할 경우, 매우 높은 확률로 레이스 컨디션이 발생해 2개의 자동입력이 동시에 진행됨 -> (특히 입력 시뮬레이션 시) 꼬임
			if (AutoKkutu.Game.Session.IsMyTurn() && AutoKkutu.Game.Session.IsTurnInProgress)
			{
				preSearch = null;
				Log.Debug("Pre-search result received but my turn is started. Dropping pre-search result.");
				return;
			}

			preSearch = args;
		}

		var autoEnter = Preference.AutoEnterEnabled && !args.HasFlag(PathFlags.DoNotAutoEnter) /*&& !args.HasFlag(PathFinderFlags.PreSearch)*/;

		if (args.Result == PathFindResultType.NotFound && !args.HasFlag(PathFlags.DoNotAutoEnter))
			UpdateStatusMessage(StatusMessage.NotFound); // Not found
		else if (args.Result == PathFindResultType.Error)
			UpdateStatusMessage(StatusMessage.Error); // Error occurred
		else if (!autoEnter)
			UpdateStatusMessage(StatusMessage.Normal);

		if (AutoKkutu.Game.RequestRescanIfPathExpired(args.Details))
		{
			Log.Warning("Expired word condition {path} rejected. Rescanning...", args.Details.Condition);
			return;
		}

		UpdateSearchState(args);
		PathListUpdated?.Invoke(this, new PathListUpdateEventArgs(args.FoundWordList.Select(po => new GuiPathObject(po)).ToImmutableList()));

		if (autoEnter)
		{
			Log.Verbose("Auto-entering on path update...");
			TryAutoEnter(args);
		}
	}

	private void TryAutoEnter(PathUpdateEventArgs args, bool usedPresearchResult = false)
	{
		if (!EntererManager.TryGetEnterer(AutoKkutu.Game, Preference.AutoEnterMode, out var enterer))
		{
			Log.Error("AutoEnter interrupted because the enterer {name} is not available.", Preference.AutoEnterMode);
			return;
		}

		if (args.Result == PathFindResultType.NotFound)
		{
			Log.Warning(I18n.Auto_NoMorePathAvailable);
			UpdateStatusMessage(StatusMessage.NotFound);
		}
		else
		{
			var opt = new EnterOptions(Preference.AutoEnterDelayEnabled, Preference.AutoEnterDelayStartAfterWordEnterEnabled, Preference.AutoEnterInputSimulateJavaScriptSendKeys, Preference.AutoEnterStartDelay, Preference.AutoEnterStartDelayRandom, Preference.AutoEnterDelayPerChar, Preference.AutoEnterDelayPerCharRandom);
			var time = AutoKkutu.Game.GetTurnTimeMillis();
			(var wordToEnter, var timeover) = args.FilteredWordList.ChooseBestWord(opt, time);
			if (string.IsNullOrEmpty(wordToEnter))
			{
				if (timeover)
				{
					Log.Warning(I18n.Auto_TimeOver);
					UpdateStatusMessage(StatusMessage.AllWordTimeOver, time);
				}
				else
				{
					Log.Warning(I18n.Auto_NoMorePathAvailable);
					UpdateStatusMessage(StatusMessage.NotFound);
				}
			}
			else
			{
				var param = args.Details;
				if (usedPresearchResult)
					param = param.WithoutFlags(PathFlags.PreSearch); // Fixme: 이런 번거로운 방법 대신 더 나은 방법 생각해보기
				enterer.RequestSend(new EnterInfo(opt, param, wordToEnter));
			}
		}
	}

	/* EVENTS: Handler */

	private void OnGameEnded(object? sender, EventArgs e)
	{
		UpdateSearchState(null, false);
		AutoKkutu.PathFilter.UnsupportedPaths.Clear(); // todo: move this to lib
		if (Preference.AutoDBUpdateEnabled)
		{
			UpdateStatusMessage(StatusMessage.DatabaseIntegrityCheck, I18n.Status_AutoUpdate);
			var updateTask = new DbUpdateTask(AutoKkutu.NodeManager, AutoKkutu.PathFilter);
			var opts = DbUpdateTask.DbUpdateCategories.None;
			if (Preference.AutoDBWordAddEnabled)
				opts |= DbUpdateTask.DbUpdateCategories.Add;
			if (Preference.AutoDBWordRemoveEnabled)
				opts |= DbUpdateTask.DbUpdateCategories.Remove;
			if (Preference.AutoDBAddEndEnabled)
				opts |= DbUpdateTask.DbUpdateCategories.AddEnd;
			var result = updateTask.Execute(opts);
			UpdateStatusMessage(StatusMessage.DatabaseIntegrityCheckDone, I18n.Status_AutoUpdate, result);
		}
		else
		{
			UpdateStatusMessage(StatusMessage.Wait);
		}
	}

	private int wordIndex;

	private void OnGameModeChange(object? sender, GameModeChangeEventArgs args) => Log.Information(I18n.Main_GameModeUpdated, ConfigEnums.GetGameModeName(args.GameMode));

	private void OnGameStarted(object? sender, EventArgs e)
	{
		Log.Debug("WordIndex reset on game start.");
		UpdateStatusMessage(StatusMessage.Normal);
		wordIndex = 0;
	}

	private void OnMyPathIsUnsupported(object? sender, UnsupportedWordEventArgs args)
	{
		if (!args.Session.IsMyTurn())
			return;

		if (autoPathFindCache is null)
		{
			Log.Warning("이전에 수행한 단어 검색 결과를 찾을 수 없습니다!");
			return;
		}

		var word = args.Word;
		Log.Warning(I18n.Main_MyPathIsUnsupported, word);

		if (Preference.AutoEnterEnabled && Preference.AutoFixEnabled)
		{

			if (!EntererManager.TryGetEnterer(AutoKkutu.Game, Preference.AutoEnterMode, out var enterer))
			{
				Log.Error("AutoFix interrupted because the enterer {name} is not available.", Preference.AutoEnterMode);
				return;
			}

			var parameter = new EnterInfo(
							new EnterOptions(Preference.AutoEnterDelayEnabled, Preference.AutoEnterDelayStartAfterWordEnterEnabled, Preference.AutoEnterInputSimulateJavaScriptSendKeys, Preference.AutoEnterStartDelay, Preference.AutoEnterStartDelayRandom, Preference.AutoEnterDelayPerChar, Preference.AutoEnterDelayPerCharRandom),
							autoPathFindCache.Details.WithoutFlags(PathFlags.PreSearch));

			(var content, var _) = autoPathFindCache.FilteredWordList.ChooseBestWord(parameter.Options, AutoKkutu.Game.GetTurnTimeMillis(), ++wordIndex);
			if (string.IsNullOrEmpty(content))
			{
				Log.Warning(I18n.Main_NoMorePathAvailable);
				//TODO: NoPathAvailable?.Invoke(this, new NoPathAvailableEventArgs(timeover, AutoKkutu.Game.GetTurnTimeMillis()));
				return;
			}

			enterer.RequestSend(parameter with { Content = content });
		}
	}

	private void OnTurnStarted(object? sender, TurnStartEventArgs args)
	{
		var isMyTurn = args.Session.IsMyTurn();
		if (isMyTurn && Preference.AutoEnterEnabled)
		{
			if (preSearch?.Details.Condition.IsSimilar(args.Condition) == true)
			{
				Log.Debug("Using the pre-search result for: {condition}", preSearch.Details.Condition);
				TryAutoEnter(preSearch, usedPresearchResult: true);
				return;
			}

			if (preSearch == null)
				Log.Debug("Pre-search data not available. Starting the search.");
			else
				Log.Warning("Pre-search path is expired! Presearch: {pre}, Search: {now}", preSearch.Details.Condition, args.Condition);
		}

		if (!Preference.OnlySearchOnMyTurn || isMyTurn)
		{
			StartPathScan(
				args.Session.GameMode,
				args.Condition,
				isMyTurn ? PathFlags.None : PathFlags.DoNotAutoEnter);  // 다른 사람 턴에 검색된 단어는 자동입력하면 안됨
		}
	}

	private void OnPathRescanRequested(object? sender, WordConditionPresentEventArgs args) => StartPathScan(AutoKkutu.Game.Session.GameMode, args.Condition);

	private void OnTurnEnded(object? sender, TurnEndEventArgs args)
	{
		var turn = args.Session.GetRelativeTurn();

		if (args.Session.IsMyTurn())
		{
			Log.Debug(I18n.Main_WordIndexReset);
			wordIndex = 0;
			return;
		}

		// Pre-search
		if (turn < 0 || turn != args.Session.GetMyPreviousUserTurn() || string.IsNullOrEmpty(args.Value))
			return;

		var missionChar = args.Session.PreviousTurnMission;
		if (!string.IsNullOrEmpty(missionChar) && args.Value.Contains(missionChar, StringComparison.OrdinalIgnoreCase))
		{
			Log.Information("Unable to pre-search because previous turn value contains mission char '{char}'.", missionChar);
			goto presearchFail;
		}

		var condition = args.Session.GameMode.ConvertWordToCondition(args.Value, args.Session.PreviousTurnMission);
		if (condition == null)
		{
			Log.Information("Unable to pre-search due to the failure of condition extraction from previous turn value.");
			goto presearchFail;
		}

		Log.Verbose("Performing pre-search on: {condition}", condition);
		AutoKkutu.PathFinder.FindPath(
			args.Session.GameMode,
			new PathDetails((WordCondition)condition, SetupPathFinderFlags() | PathFlags.PreSearch, Preference.ReturnModeEnabled, Preference.MaxDisplayedWordCount),
			Preference.ActiveWordPreference);

		return;

presearchFail:
		Log.Verbose("Pre-search result flushed.");
		preSearch = null;
	}

	private void OnTypingWordPresented(object? sender, WordPresentEventArgs args)
	{
		var word = args.Word;

		if (!Preference.AutoEnterEnabled)
			return;

		if (!EntererManager.TryGetEnterer(AutoKkutu.Game, Preference.AutoEnterMode, out var enterer))
		{
			Log.Error("TypingBattle Auto-Enter interrupted because the enterer {name} is not available.", Preference.AutoEnterMode);
			return;
		}

		enterer.RequestSend(new EnterInfo(
			new EnterOptions(Preference.AutoEnterDelayEnabled, Preference.AutoEnterDelayStartAfterWordEnterEnabled, Preference.AutoEnterInputSimulateJavaScriptSendKeys, Preference.AutoEnterStartDelay, Preference.AutoEnterStartDelayRandom, Preference.AutoEnterDelayPerChar, Preference.AutoEnterDelayPerCharRandom),
			PathDetails.Empty.WithFlags(PathFlags.DoNotCheckExpired),
			word));
	}

	private void OnChatUpdated(object? sender, EventArgs args) => ChatUpdated?.Invoke(this, args);
}
