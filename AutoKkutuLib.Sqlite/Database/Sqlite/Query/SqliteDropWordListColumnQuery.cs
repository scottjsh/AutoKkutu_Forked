﻿using AutoKkutuLib.Sqlite.Database.Sqlite;

namespace AutoKkutuLib.Database.Sql.Query;
public class SqliteDropWordListColumnQuery : AbstractDropWordListColumnQuery
{
	internal SqliteDropWordListColumnQuery(AbstractDatabaseConnection connection, string columnName) : base(connection, columnName) { }

	public override int Execute()
	{
		Connection.RebuildWordList();
		return 0;
	}
}
