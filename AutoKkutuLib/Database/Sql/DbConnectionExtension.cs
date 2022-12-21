﻿using Dapper;
using Serilog;
using System.Data;

namespace AutoKkutuLib.Database.Sql;
public static class DbConnectionExtension
{
	public static int TryExecute(this IDbConnection connection, string query, object? parameters = null)
	{
		try
		{
			return connection.Execute(query, parameters);
		}
		catch (Exception ex)
		{
			Log.Error(ex, "SQL execution error of query {query}.", query);
		}
		return 0;
	}

	public static T? TryExecuteScalar<T>(this IDbConnection connection, string query, object? parameters = null)
	{
		try
		{
			return connection.ExecuteScalar<T>(query, parameters);
		}
		catch (Exception ex)
		{
			Log.Error(ex, "SQL scalar-execution error of query {query}.", query);
		}
		return default;
	}
}
