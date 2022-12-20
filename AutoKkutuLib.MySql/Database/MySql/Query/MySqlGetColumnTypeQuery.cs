﻿using Dapper;
using Serilog;

namespace AutoKkutuLib.Database.Sql.Query;
public class MySqlGetColumnTypeQuery : AbstractGetColumnTypeQuery
{
	private readonly string dbName;

	internal MySqlGetColumnTypeQuery(AbstractDatabaseConnection connection, string dbName, string tableName, string columnName) : base(connection, tableName, columnName) => this.dbName = dbName;

	public override string Execute()
	{
		try
		{
			return Connection.ExecuteScalar<string>("SELECT data_type FROM Information_schema.columns WHERE table_schema=@DbName AND table_name=@TableName AND column_name=@ColumnName;", new
			{
				DbName = dbName,
				TableName,
				ColumnName
			});
		}
		catch (Exception ex)
		{
			Log.Error(ex, DatabaseConstants.ErrorGetColumnType, ColumnName, TableName);
		}
		return "";
	}
}
