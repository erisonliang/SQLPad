﻿using System.Configuration;
using SqlPad.Commands;
using SqlPad.Oracle.Commands;

namespace SqlPad.Oracle
{
	public class OracleInfrastructureFactory : IInfrastructureFactory
	{
		private readonly OracleCommandFactory _commandFactory = new OracleCommandFactory();

		static OracleInfrastructureFactory()
		{
			OracleDatabaseModel.ValidateConfiguration();
		}

		#region Implementation of IInfrastructureFactory
		public ICommandFactory CommandFactory { get { return _commandFactory; } }
		
		public ITokenReader CreateTokenReader(string sqlText)
		{
			return OracleTokenReader.Create(sqlText);
		}

		public ISqlParser CreateParser()
		{
			return new OracleSqlParser();
		}

		public IStatementValidator CreateStatementValidator()
		{
			return new OracleStatementValidator();
		}

		public IDatabaseModel CreateDatabaseModel(ConnectionStringSettings connectionString)
		{
			return OracleDatabaseModel.GetDatabaseModel(connectionString);
		}

		public ICodeCompletionProvider CreateCodeCompletionProvider()
		{
			return new OracleCodeCompletionProvider();
		}

		public ICodeSnippetProvider CreateSnippetProvider()
		{
			return new OracleSnippetProvider();
		}

		public IContextActionProvider CreateContextActionProvider()
		{
			return new OracleContextActionProvider();

		}

		public IMultiNodeEditorDataProvider CreateMultiNodeEditorDataProvider()
		{
			return new OracleMultiNodeEditorDataProvider();
		}

		public IStatementFormatter CreateSqlFormatter(SqlFormatterOptions options)
		{
			return new OracleStatementFormatter(options);
		}

		public IToolTipProvider CreateToolTipProvider()
		{
			return new OracleToolTipProvider();
		}

		public INavigationService CreateNavigationService()
		{
			return new OracleNavigationService();
		}
		#endregion
	}
}
