﻿using AutoKkutuLib.Extension;
using Dapper;
using Serilog;

namespace AutoKkutuLib.Database.Sql.Query;
public class WordAdditionQuery : SqlQuery<bool>
{
	public string? Word { get; set; }
	public WordFlags? WordFlags { get; set; }

	internal WordAdditionQuery(AbstractDatabaseConnection connection) : base(connection) { }

	public bool Execute(string word, WordFlags wordFlags)
	{
		Word = word;
		WordFlags = wordFlags;
		return Execute();
	}

	public override bool Execute()
	{
		if (string.IsNullOrWhiteSpace(Word))
			throw new InvalidOperationException(nameof(Word) + " not set.");
		if (WordFlags is null)
			throw new InvalidOperationException(nameof(WordFlags) + " not set.");

		Log.Debug(nameof(WordDeletionQuery) + ": Adding word {0} from database.", Word);
		if (Connection.ExecuteScalar<int>($"SELECT COUNT(*) FROM {DatabaseConstants.WordTableName} WHERE {DatabaseConstants.WordColumnName} = @Word;", new { Word }) > 0)
		{
			Log.Debug(nameof(WordDeletionQuery) + ": Word {0} already exists in database.", Word);
			return false;
		}

		var count = Connection.Execute(
			$"INSERT INTO {DatabaseConstants.WordTableName}({DatabaseConstants.WordColumnName}, {DatabaseConstants.WordIndexColumnName}, {DatabaseConstants.ReverseWordIndexColumnName}, {DatabaseConstants.KkutuWordIndexColumnName}, {DatabaseConstants.FlagsColumnName}) VALUES(@Word, @LaFHead, @FaLHead, @KkutuHead, @Flags);",
			new
			{
				Word,
				LaFHead = Word.GetLaFHeadNode(),
				FaLHead = Word.GetFaLHeadNode(),
				KkutuHead = Word.GetKkutuHeadNode(),
				Flags = (int)WordFlags
			});
		Log.Debug(nameof(WordAdditionQuery) + ": Added {0} of word {1} to database with flags {2}.", count, Word, WordFlags);
		return count > 0;
	}
}
