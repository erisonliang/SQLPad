namespace SqlPad.Oracle
{
	public static class OracleSemanticErrorType
	{
		public const string None = null;
		public const string ClauseNotAllowed = "Clause not allowed";
		public const string ConnectByClauseRequired = "CONNECT BY clause required in this query block";
		public const string InvalidDateLiteral = "Invalid date literal";
		public const string InvalidTimestampLiteral = "Invalid timestamp literal";
		public const string AmbiguousReference = "Ambiguous reference";
		public const string InvalidParameterCount = "Invalid parameter count";
		public const string MissingParenthesis = "Missing parenthesis";
		public const string NonParenthesisFunction = "Non-parenthesis function";
		public const string InvalidIdentifier = "Invalid identifier";
		public const string AnalyticClauseNotSupported = "Analytic clause not supported";
		public const string ObjectStatusInvalid = "Object is invalid or unusable";
		public const string ObjectCannotBeUsed = "Object cannot be used here";
		public const string InvalidColumnCount = "Invalid column count";
		public const string FunctionReturningRowSetRequired = "Function must return a row set";
		public const string NamedParameterNotAllowed = "Named parameter not allowed";
		public const string PositionalParameterNotAllowed = "A positional parameter association may not follow a named association";
		public const string InvalidColumnIndex = "Invalid column index";
		public const string GroupFunctionNotAllowed = "Group function is not allowed here";
		public const string MissingWithClauseColumnAliasList = "WITH clause element did not have a column alias list";
		public const string InvalidCycleMarkValue = "Cycle mark value and non-cycle mark value must be one byte character string values";
		public const string IncorrectUseOfLnNvlOperator = "Incorrect use of LNNVL operator";
		public const string ExpectAggregateFunctionInsidePivotOperation = "Expect aggregate function inside pivot operation";
		public const string CannotAccessRowsFromNonNestedTableItem = "Cannot access rows from a non-nested table item";
		public const string NotEnoughArgumentsForFunction = "Not enough arguments for function";
	}

	public static class OracleSemanticErrorTooltipText
	{
		public const string InvalidDateLiteral = "ANSI format of DATE literal must be [-]YYYY-MM-DD";
		public const string InvalidTimestampLiteral = "ANSI format of TIMESTAMP literal must be [-]YYYY-MM-DD HH24:MI:SS[.1-9 digits] [time zone definition]";
	}

	public static class OracleSuggestionType
	{
		public const string None = null;
		public const string PotentialDatabaseLink = "Reference over database link? Use object qualifier. ";
		public const string UseExplicitColumnList = "Use explicit column list";
	}
}