﻿using AutoKkutuLib.Browser;
using Serilog;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoKkutuLib.Game;
public partial class Game
{
	private IDictionary<GameImplMode, IDictionary<string, Func<JsonNode, Task>>>? specializedSniffers;
	private IDictionary<string, Func<JsonNode, Task>>? baseSniffers;
	private readonly JsonSerializerOptions unescapeUnicodeJso = new()
	{
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	private void BeginWebSocketSniffing()
	{
		if (webSocketHandler == null)
			return;

		Func<JsonNode, Task> SimpleHandler<T>(string messageType, Action<T> handler, Func<JsonNode, ValueTask<T>> parser)
		{
			return async json =>
			{
				try
				{
					handler(await parser(json));
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Error processing {messageType} message.", messageType);
				}
			};
		}

		Log.Information("WebSocket Handler initialized.");

		baseSniffers = new Dictionary<string, Func<JsonNode, Task>>
		{
			[webSocketHandler.MessageType_Welcome] = SimpleHandler("welcome", OnWsWelcome, webSocketHandler.ParseWelcome),
			[webSocketHandler.MessageType_Room] = SimpleHandler("room", OnWsRoom, webSocketHandler.ParseRoom),
		};

		specializedSniffers = new Dictionary<GameImplMode, IDictionary<string, Func<JsonNode, Task>>>()
		{
			[GameImplMode.Classic] = new Dictionary<string, Func<JsonNode, Task>>
			{
				[webSocketHandler.MessageType_TurnStart] = SimpleHandler("turnStart", OnWsClassicTurnStart, webSocketHandler.ParseClassicTurnStart),
				[webSocketHandler.MessageType_TurnEnd] = SimpleHandler("turnEnd", OnWsClassicTurnEnd, webSocketHandler.ParseClassicTurnEnd),
				[webSocketHandler.MessageType_TurnError] = SimpleHandler("turnError", OnWsTurnError, webSocketHandler.ParseClassicTurnError)
			},
			[GameImplMode.TypingBattle] = new Dictionary<string, Func<JsonNode, Task>>
			{
				[webSocketHandler.MessageType_RoundReady] = SimpleHandler("roundReady", OnWsTypingBattleRoundReady, webSocketHandler.ParseTypingBattleRoundReady),
				[webSocketHandler.MessageType_TurnStart] = SimpleHandler("turnStart", OnWsTypingBattleTurnStart, webSocketHandler.ParseTypingBattleTurnStart),
				[webSocketHandler.MessageType_TurnEnd] = SimpleHandler("turnEnd", OnWsTypingBattleTurnEnd, webSocketHandler.ParseTypingBattleTurnEnd),
			}
		};
		Browser.WebSocketMessage += OnWebSocketMessage;
	}

	private void EndWebSocketSniffing()
	{
		if (webSocketHandler == null)
			return;

		Browser.WebSocketMessage -= OnWebSocketMessage;
		specializedSniffers = null;
		Log.Information("WebSocket Sniffer uninitialized.");
	}

	/// <summary>
	/// 과도한 양의 WebSocket 메시지 처리에 의한 부하와 프리징, 그리고 정작 중요한 JavaScript들의 실행 실패를 막기 위해
	/// 일차적으로 브라우저 단에서 받을 메시지들을 필터링합니다. (이전까지 만연했던 프리징과 JavaScript 실행 타임아웃 오류의 원인은 모두 이것 때문이었습니다...)
	/// 그 다음 이차적으로 프로그램 단에서 메시지를 검증하고 필터링하는 과정을 거칩니다.
	/// </summary>
	/// <returns></returns>
	private async Task RegisterWebSocketFilters()
	{
		if (webSocketHandler != null)
			await webSocketHandler.RegisterWebSocketFilter();
	}

	/// <summary>
	/// 웹소켓으로부터 메세지 수신 시, 핸들링을 위해 실행되는 제일 첫 단계의 함수.
	/// 메세지는 이 함수에서 메세지 종류에 따라 버려지거나, 다른 특화된 처리 함수들로 갈라져 들어갑니다.
	/// </summary>
	private void OnWebSocketMessage(object? sender, WebSocketMessageEventArgs args)
	{
		webSocketHandler?.OnWebSocketMessage(args.Json);
		Log.Verbose("WebSocket Message (type: {type}) - {json}", args.Type, args.Json.ToJsonString(unescapeUnicodeJso));
		if (specializedSniffers != null && specializedSniffers.TryGetValue(Session.GameMode.ToGameImplMode(), out var snifferTable) && snifferTable.TryGetValue(args.Type, out var mySpecialSniffer))
			Task.Run(async () => await mySpecialSniffer(args.Json));
		else if (baseSniffers?.TryGetValue(args.Type, out var myBaseSniffer) ?? false)
			Task.Run(async () => await myBaseSniffer(args.Json));
	}

	private void OnWsWelcome(WsWelcome data)
	{
		if (webSocketHandler == null)
			return;

		Log.Debug("WebSocket Handler detected game session: {userId: '{userId}'}", data.UserId);
		NotifyGameSession(data.UserId);
	}

	private void OnWsRoom(WsRoom data)
	{
		if (data.Mode == GameMode.None)
			Log.Warning("Unknown or unsupported game mode: {mode}", data.ModeString);

		if (data.Gaming)
		{
			Log.Debug("WebSocket Handler detected gameSeq: seq=[{seq}]", string.Join(", ", data.GameSequence));
			NotifyGameSequence(data.GameSequence);
		}

		Log.Debug("WebSocket Handler detected game mode change: mode={mode} modeName='{modeName}'", data.Mode, data.ModeString);
		NotifyGameMode(data.Mode);
	}

	private void OnWsClassicTurnStart(WsClassicTurnStart data)
	{
		Log.Debug("WebSocket Handler detected turn start: turn={turn} condition={condition}", data.Turn, data.Condition);
		NotifyClassicTurnStart(false, data.Turn, data.Condition);
	}

	private void OnWsClassicTurnEnd(WsClassicTurnEnd data)
	{
		if (data.Ok)
		{
			Log.Debug("WebSocket Handler detected turn end (ok): value='{value}'", data.Value);
			NotifyClassicTurnEnd(data.Value ?? "");

			if (!string.IsNullOrWhiteSpace(data.Value))
				NotifyWordHistory(data.Value);
		}

		if (!string.IsNullOrWhiteSpace(data.Hint))
		{
			Log.Debug("WebSocket Handler detected turn end (hint): hint='{hint}'", data.Hint);
			NotifyWordHint(data.Hint);
		}
	}

	private void OnWsTurnError(WsTurnError data)
	{
		if (!string.IsNullOrWhiteSpace(data.Value))
		{
			Log.Debug("WebSocket Handler detected turn error: value='{value}' errorCode={code}", data.Value, data.ErrorCode);
			NotifyTurnError(data.Value, data.ErrorCode, false);
		}
	}

	private void OnWsTypingBattleRoundReady(WsTypingBattleRoundReady data)
	{
		// TODO
	}


	private void OnWsTypingBattleTurnStart(WsTypingBattleTurnStart data)
	{
		// TODO
	}


	private void OnWsTypingBattleTurnEnd(WsTypingBattleTurnEnd data)
	{
		// TODO
	}
}
