﻿using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoKkutu.Databases
{
	public partial class MySQLDatabase : CommonDatabase
	{
		private static MySqlConnection DatabaseConnection;

		private string ConnectionString;

		private string DatabaseName = "autokkutu";

		public MySQLDatabase(string connectionString) : base()
		{
			ConnectionString = connectionString;

			try
			{
				int databaseNameIndex = connectionString.ToLowerInvariant().IndexOf("database") + 9;
				int databaseNameIndexEnd = connectionString.Substring(databaseNameIndex).IndexOf(';');
				DatabaseName = connectionString.Substring(databaseNameIndex, databaseNameIndexEnd);
				Logger.InfoFormat("MySQL database name is '{0}'", DatabaseName);

				// Open the connection
				Logger.Info("Opening database connection...");
				DatabaseConnection = new MySqlConnection(connectionString);
				DatabaseConnection.Open();

				TryExecuteNonQuery("drop existing checkMissionCharFunc", $"DROP FUNCTION IF EXISTS {GetCheckMissionCharFuncName()};");
				TryExecuteNonQuery("register checkMissionCharFunc", $@"CREATE FUNCTION {GetCheckMissionCharFuncName()}(word VARCHAR(256), missionWord VARCHAR(2)) RETURNS INT
DETERMINISTIC
NO SQL
BEGIN
	DECLARE occurrence INT;

	SET occurrence = ROUND((LENGTH(word) - LENGTH(REPLACE(LOWER(word), LOWER(missionWord), ''))) / LENGTH(missionWord));
	IF occurrence > 0 THEN
		RETURN occurrence + {DatabaseConstants.MissionCharIndexPriority};
	ELSE
		RETURN 0;
	END IF;
END;
");

				// Check the database tables
				CheckTable();

				Logger.Info("Successfully established database connection.");
			}
			catch (Exception ex)
			{
				Logger.Error(DatabaseConstants.Error_Connect, ex);
				if (DBError != null)
					DBError(null, EventArgs.Empty);
			}
		}

		protected override string GetCheckMissionCharFuncName() => "__AutoKkutu_CheckMissionChar";

		public override string GetDBInfo() => "MySQL";

		protected override int ExecuteNonQuery(string query, IDisposable connection = null)
		{
			CheckConnectionType(connection);
			using (var command = new MySqlCommand(query, (MySqlConnection)(connection ?? DatabaseConnection)))
				return command.ExecuteNonQuery();
		}

		protected override object ExecuteScalar(string query, IDisposable connection = null)
		{
			CheckConnectionType(connection);
			using (var command = new MySqlCommand(query, (MySqlConnection)(connection ?? DatabaseConnection)))
				return command.ExecuteScalar();
		}

		protected override CommonDatabaseReader ExecuteReader(string query, IDisposable connection = null)
		{
			CheckConnectionType(connection);
			using (var command = new MySqlCommand(query, (MySqlConnection)(connection ?? DatabaseConnection)))
				return new MySQLDatabaseReader(command.ExecuteReader());
		}

		private void CheckConnectionType(object connection)
		{
			if (connection != null && connection.GetType() != typeof(MySqlConnection))
				throw new ArgumentException("Connection is not MySqlConnection");
		}

		protected override int DeduplicateDatabase(IDisposable connection)
		{
			CheckConnectionType(connection);

			// https://wiki.postgresql.org/wiki/Deleting_duplicates
			return ExecuteNonQuery(DatabaseConstants.DeduplicationQuery, (MySqlConnection)connection);
		}

		protected override IDisposable OpenSecondaryConnection()
		{
			var connection = new MySqlConnection(ConnectionString);
			connection.Open();
			return connection;
		}

		protected override bool IsColumnExists(string columnName, string tableName = null, IDisposable connection = null)
		{
			tableName = tableName ?? DatabaseConstants.WordListTableName;
			try
			{
				return Convert.ToInt32(ExecuteScalar($"SELECT COUNT(*) FROM Information_schema.columns WHERE table_schema='{DatabaseName}' AND table_name='{tableName}' AND column_name='{columnName}';")) > 0;
			}
			catch (Exception ex)
			{
				Logger.Info(string.Format(DatabaseConstants.Error_IsColumnExists, columnName, tableName), ex);
				return false;
			}
		}

		public override bool IsTableExists(string tableName, IDisposable connection = null)
		{
			try
			{
				return Convert.ToInt32(ExecuteScalar($"SELECT COUNT(*) FROM information_schema.tables WHERE table_name='{tableName}';", connection).ToString()) > 0;
			}
			catch (Exception ex)
			{
				Logger.Info(string.Format(DatabaseConstants.Error_IsTableExists, tableName), ex);
				return false;
			}
		}

		protected override void AddSequenceColumnToWordList() => ExecuteNonQuery($"ALTER TABLE {DatabaseConstants.WordListTableName} ADD COLUMN seq NOT NULL AUTO_INCREMENT PRIMARY KEY;");

		protected override string GetWordListColumnOptions() => "seq INT NOT NULL AUTO_INCREMENT PRIMARY KEY, word VARCHAR(256) UNIQUE NOT NULL, word_index CHAR(1) NOT NULL, reverse_word_index CHAR(1) NOT NULL, kkutu_index VARCHAR(2) NOT NULL, flags SMALLINT NOT NULL";

		protected override string GetColumnType(string columnName, string tableName = null, IDisposable connection = null)
		{
			tableName = tableName ?? DatabaseConstants.WordListTableName;
			try
			{
				return ExecuteScalar($"SELECT data_type FROM Information_schema.columns WHERE table_name='{tableName}' AND column_name='{columnName}';", connection).ToString();
			}
			catch (Exception ex)
			{
				Logger.Info(string.Format(DatabaseConstants.Error_GetColumnType, columnName, tableName), ex);
				return "";
			}
		}

		protected override void ChangeWordListColumnType(string columnName, string newType, string tableName = null, IDisposable connection = null) => ExecuteNonQuery($"ALTER TABLE {DatabaseConstants.WordListTableName} MODIFY {columnName} {newType}");

		protected override void DropWordListColumn(string columnName) => ExecuteNonQuery($"ALTER TABLE {DatabaseConstants.WordListTableName} DROP {columnName}");

		protected override void PerformVacuum()
		{
		}

		public override void Dispose() => DatabaseConnection.Dispose();
	}

	public class MySQLDatabaseReader : CommonDatabaseReader
	{
		private MySqlDataReader Reader;
		public MySQLDatabaseReader(MySqlDataReader reader) => Reader = reader;

		protected override object GetObject(string name) => Reader[name];
		public override string GetString(int index) => Reader.GetString(index);
		public override int GetOrdinal(string name) => Reader.GetOrdinal(name);
		public override int GetInt32(int index) => Reader.GetInt32(index);
		public override bool Read() => Reader.Read();
		public override void Dispose() => Reader.Dispose();
	}
}
