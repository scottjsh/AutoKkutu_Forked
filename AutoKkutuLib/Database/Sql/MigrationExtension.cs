﻿using Dapper;
using Serilog;

namespace AutoKkutuLib.Database.Sql;

public static class MigrationExtension
{
	private static void AddInexistentColumns(this AbstractDatabaseConnection connection)
	{
		if (connection == null)
			throw new ArgumentNullException(nameof(connection));

		if (!connection.Query.IsColumnExists(DatabaseConstants.WordTableName, DatabaseConstants.ReverseWordIndexColumnName).Execute())
		{
			connection.TryExecute($"ALTER TABLE {DatabaseConstants.WordTableName} ADD COLUMN {DatabaseConstants.ReverseWordIndexColumnName} CHAR(1) NOT NULL DEFAULT ' '");
			Log.Warning($"Added {DatabaseConstants.ReverseWordIndexColumnName} column.");
		}

		if (!connection.Query.IsColumnExists(DatabaseConstants.WordTableName, DatabaseConstants.KkutuWordIndexColumnName).Execute())
		{
			connection.TryExecute($"ALTER TABLE {DatabaseConstants.WordTableName} ADD COLUMN {DatabaseConstants.KkutuWordIndexColumnName} CHAR(2) NOT NULL DEFAULT ' '");
			Log.Warning($"Added {DatabaseConstants.KkutuWordIndexColumnName} column.");
		}
	}

	private static bool AddSequenceColumn(this AbstractDatabaseConnection connection)
	{
		if (connection == null)
			throw new ArgumentNullException(nameof(connection));

		if (!connection.Query.IsColumnExists(DatabaseConstants.WordTableName, DatabaseConstants.SequenceColumnName).Execute())
		{
			try
			{
				connection.Query.AddWordListSequenceColumn().Execute();
				Log.Warning("Added sequence column.");
				return true;
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Failed to add sequence column.");
			}
		}

		return false;
	}

	public static void CheckBackwardCompatibility(this AbstractDatabaseConnection connection)
	{
		if (connection == null)
			throw new ArgumentNullException(nameof(connection));

		var needToCleanUp = false;

		connection.AddInexistentColumns();
		needToCleanUp |= connection.DropIsEndWordColumn();
		needToCleanUp |= connection.AddSequenceColumn();
		needToCleanUp |= connection.UpdateKkutuIndexDataType();

		if (needToCleanUp)
		{
			Log.Warning("Executing vacuum...");
			connection.Query.Vacuum().Execute();
		}
	}

	private static bool DropIsEndWordColumn(this AbstractDatabaseConnection connection)
	{
		if (connection == null)
			throw new ArgumentNullException(nameof(connection));

		if (connection.Query.IsColumnExists(DatabaseConstants.WordTableName, DatabaseConstants.IsEndwordColumnName).Execute())
		{
			try
			{
				if (!connection.Query.IsColumnExists(DatabaseConstants.WordTableName, DatabaseConstants.FlagsColumnName).Execute())
				{
					connection.Execute($"ALTER TABLE {DatabaseConstants.WordTableName} ADD COLUMN {DatabaseConstants.FlagsColumnName} SMALLINT NOT NULL DEFAULT 0;");
					connection.Execute($"UPDATE {DatabaseConstants.WordTableName} SET {DatabaseConstants.FlagsColumnName} = CAST({DatabaseConstants.IsEndwordColumnName} AS SMALLINT);");
					Log.Warning($"Converted '{DatabaseConstants.IsEndwordColumnName}' into {DatabaseConstants.FlagsColumnName} column.");
				}

				connection.Query.DropWordListColumn(DatabaseConstants.IsEndwordColumnName).Execute();
				Log.Warning($"Dropped {DatabaseConstants.IsEndwordColumnName} column as it is no longer used.");
				return true;
			}
			catch (Exception ex)
			{
				Log.Error(ex, $"Failed to add {DatabaseConstants.FlagsColumnName} column.");
			}
		}

		return false;
	}

	private static bool UpdateKkutuIndexDataType(this AbstractDatabaseConnection connection)
	{
		if (connection == null)
			throw new ArgumentNullException(nameof(connection));

		var kkutuindextype = connection.Query.GetColumnType(DatabaseConstants.WordTableName, DatabaseConstants.KkutuWordIndexColumnName).Execute();
		if (kkutuindextype != null && (kkutuindextype.Equals("CHAR(2)", StringComparison.OrdinalIgnoreCase) || kkutuindextype.Equals("character", StringComparison.OrdinalIgnoreCase)))
		{
			connection.Query.ChangeWordListColumnType(DatabaseConstants.WordTableName, DatabaseConstants.KkutuWordIndexColumnName, newType: "VARCHAR(2)").Execute();
			Log.Warning($"Changed type of '{DatabaseConstants.KkutuWordIndexColumnName}' from CHAR(2) to VARCHAR(2).");
			return true;
		}

		return false;
	}
}
