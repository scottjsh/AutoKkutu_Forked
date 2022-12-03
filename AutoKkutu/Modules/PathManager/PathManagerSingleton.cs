﻿using AutoKkutu.Constants;
using AutoKkutu.Database;
using AutoKkutu.Database.Extension;
using AutoKkutu.Utils;
using AutoKkutu.Utils.Extension;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace AutoKkutu.Modules.PathManager
{
	public static class PathManager
	{
		private static readonly Lazy<PathManagerCore> _impl = new();
		private static PathManagerCore Impl => _impl.Value;

		/* Word lists */

		public static ICollection<string>? AttackWordList => Impl.AttackNodes;
		public static ICollection<string>? EndWordList => Impl.EndNodes;
		public static ICollection<string>? KKTAttackWordList => Impl.KKTAttackNodes;
		public static ICollection<string>? KKTEndWordList => Impl.KKTEndNodes;
		public static ICollection<string>? KkutuAttackWordList => Impl.KkutuAttackNodes;
		public static ICollection<string>? KkutuEndWordList => Impl.KkutuEndNodes;
		public static ICollection<string>? ReverseAttackWordList => Impl.ReverseAttackNodes;
		public static ICollection<string>? ReverseEndWordList => Impl.ReverseEndNodes;

		/* Path lists */

		public static ICollection<string> InexistentPathList => Impl.InexistentPathList;

		public static ICollection<string> NewPathList => Impl.NewPathList;

		public static ICollection<string> PreviousPath => Impl.PreviousPath;

		public static ICollection<string> UnsupportedPathList => Impl.UnsupportedPathList;

		public static ReaderWriterLockSlim PathListLock => Impl.PathListLock;

		/* Initialization */

		public static void Initialize()
		{
			try
			{
				UpdateNodeLists(AutoKkutuMain.Database.Connection);
			}
			catch (Exception ex)
			{
				Log.Error(ex, I18n.PathFinder_Init_Error);
				DatabaseEvents.TriggerDatabaseError();
			}
		}

		public static void UpdateNodeLists(AbstractDatabaseConnection connection)
		{
			AttackWordList = connection.GetNodeList(DatabaseConstants.AttackNodeIndexTableName);
			EndWordList = connection.GetNodeList(DatabaseConstants.EndNodeIndexTableName);
			ReverseAttackWordList = connection.GetNodeList(DatabaseConstants.ReverseAttackNodeIndexTableName);
			ReverseEndWordList = connection.GetNodeList(DatabaseConstants.ReverseEndNodeIndexTableName);
			KkutuAttackWordList = connection.GetNodeList(DatabaseConstants.KkutuAttackNodeIndexTableName);
			KkutuEndWordList = connection.GetNodeList(DatabaseConstants.KkutuEndNodeIndexTableName);
			KKTAttackWordList = connection.GetNodeList(DatabaseConstants.KKTAttackNodeIndexTableName);
			KKTEndWordList = connection.GetNodeList(DatabaseConstants.KKTEndNodeIndexTableName);
		}

		/* Path-controlling */

		public static void AddPreviousPath(string word)
		{
			if (!string.IsNullOrWhiteSpace(word))
				PreviousPath.Add(word);
		}

		public static void AddToUnsupportedWord(string word, bool isNonexistent)
		{
			if (!string.IsNullOrWhiteSpace(word))
				try
				{
					PathListLock.EnterWriteLock();
					UnsupportedPathList.Add(word);
					if (isNonexistent)
						InexistentPathList.Add(word);
				}
				finally
				{
					PathListLock.ExitWriteLock();
				}
		}

		/* AutoDatabaseUpdate */

		public static string? UpdateDatabase()
		{
			if (!AutoKkutuMain.Configuration.AutoDBUpdateEnabled)
				return null;

			Log.Debug(I18n.PathFinder_AutoDBUpdate);
			try
			{
				PathListLock.EnterUpgradeableReadLock();
				int NewPathCount = NewPathList.Count;
				int InexistentPathCount = InexistentPathList.Count;
				if (NewPathCount + InexistentPathCount == 0)
					Log.Warning(I18n.PathFinder_AutoDBUpdate_Empty);
				else
				{
					Log.Debug(I18n.PathFinder_AutoDBUpdate_New, NewPathCount);
					int AddedPathCount = AddNewPaths();

					Log.Information(I18n.PathFinder_AutoDBUpdate_Remove, InexistentPathCount);

					int RemovedPathCount = RemoveInexistentPaths();
					string result = string.Format(CultureInfo.CurrentCulture, I18n.PathFinder_AutoDBUpdate_Result, AddedPathCount, NewPathCount, RemovedPathCount, InexistentPathCount);

					Log.Information(I18n.PathFinder_AutoDBUpdate_Finished, result);
					return result;
				}
			}
			finally
			{
				PathListLock.ExitUpgradeableReadLock();
			}

			return null;
		}

		private static int AddNewPaths()
		{
			int count = 0;
			var listCopy = new List<string>(NewPathList);
			try
			{
				PathListLock.EnterWriteLock();
				NewPathList.Clear();
			}
			finally
			{
				PathListLock.ExitWriteLock();
			}

			foreach (string word in listCopy)
			{
				WordDbTypes flags = DatabaseUtils.GetFlags(word);

				try
				{
					Log.Debug(I18n.PathFinder_AddPath, word, flags);
					if (AutoKkutuMain.Database.Connection.AddWord(word, flags))
					{
						Log.Information(I18n.PathFinder_AddPath_Success, word);
						count++;
					}
				}
				catch (Exception ex)
				{
					Log.Error(ex, I18n.PathFinder_AddPath_Failed, word);
				}
			}

			return count;
		}

		private static int RemoveInexistentPaths()
		{
			int count = 0;
			var listCopy = new List<string>(InexistentPathList);
			try
			{
				PathListLock.EnterWriteLock();
				InexistentPathList.Clear();
			}
			finally
			{
				PathListLock.ExitWriteLock();
			}

			foreach (string word in listCopy)
				try
				{
					count += AutoKkutuMain.Database.Connection.RequireNotNull().DeleteWord(word);
				}
				catch (Exception ex)
				{
					Log.Error(ex, I18n.PathFinder_RemoveInexistent_Failed, word);
				}

			return count;
		}

		/* Other utility things */

		public static bool CheckNodePresence(string? nodeType, string item, ICollection<string>? nodeList, WordDbTypes theFlag, ref WordDbTypes flags, bool tryAdd = false)
		{
			if (tryAdd && string.IsNullOrEmpty(nodeType) || string.IsNullOrWhiteSpace(item) || nodeList == null)
				return false;

			bool exists = nodeList.Contains(item);
			if (exists)
				flags |= theFlag;
			else if (tryAdd && flags.HasFlag(theFlag))
			{
				nodeList.Add(item);
				Log.Information(string.Format(CultureInfo.CurrentCulture, I18n.PathFinder_AddNode, nodeType, item));
				return true;
			}
			return false;
		}

		// TODO: Move to extension
		public static string? ConvertToPresentedWord(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				throw new ArgumentException("Parameter is null or blank", nameof(path));

			switch (AutoKkutuMain.Configuration.GameMode)
			{
				case GameMode.LastAndFirst:
				case GameMode.KungKungTta:
				case GameMode.LastAndFirstFree:
					return path.GetLaFTailNode();

				case GameMode.FirstAndLast:
					return path.GetFaLHeadNode();

				case GameMode.MiddleAndFirst:
					if (path.Length > 2 && path.Length % 2 == 1)
						return path.GetMaFTailNode();
					break;

				case GameMode.Kkutu:
					return path.GetKkutuTailNode();

				case GameMode.TypingBattle:
					break;

				case GameMode.All:
					break;

				case GameMode.Free:
					break;
			}

			return null;
		}

		public static IList<PathObject> CreateQualifiedWordList(IList<PathObject> wordList)
		{
			if (wordList is null)
				throw new ArgumentNullException(nameof(wordList));

			var qualifiedList = new List<PathObject>();
			foreach (PathObject word in wordList)
				try
				{
					PathListLock.EnterReadLock();
					if (InexistentPathList.Contains(word.Content))
						word.RemoveQueued = true;
					if (UnsupportedPathList.Contains(word.Content))
						word.Excluded = true;
					else if (!AutoKkutuMain.Configuration.ReturnModeEnabled && PreviousPath.Contains(word.Content))
						word.AlreadyUsed = true;
					else
						qualifiedList.Add(word);
				}
				finally
				{
					PathListLock.ExitReadLock();
				}

			return qualifiedList;
		}

		public static void ResetPreviousPath() => PreviousPath.Clear();
	}
}
