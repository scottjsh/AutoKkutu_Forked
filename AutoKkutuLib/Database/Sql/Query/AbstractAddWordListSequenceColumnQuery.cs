﻿namespace AutoKkutuLib.Database.Sql.Query;
public abstract class AbstractAddWordListSequenceColumnQuery : SqlQuery<bool>
{
	protected AbstractAddWordListSequenceColumnQuery(AbstractDatabaseConnection connection) : base(connection) { }
}
