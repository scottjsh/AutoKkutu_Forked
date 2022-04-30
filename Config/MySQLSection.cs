﻿using System.Configuration;

namespace AutoKkutu
{
	public class MySQLSection : ConfigurationSection
	{
		[ConfigurationProperty("connectionString")]
		public string ConnectionString
		{
			get
			{
				return (string)base["connectionString"];
			}
			set
			{
				base["connectionString"] = value;
			}
		}
	}
}
