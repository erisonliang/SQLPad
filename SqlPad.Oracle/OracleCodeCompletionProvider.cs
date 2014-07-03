﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	public class OracleCodeCompletionProvider : ICodeCompletionProvider
	{
		private readonly OracleSqlParser _parser = new OracleSqlParser();
		private static readonly ICodeCompletionItem[] EmptyCollection = new ICodeCompletionItem[0];

		private const string JoinTypeJoin = "JOIN";
		private const string JoinTypeLeftJoin = "LEFT JOIN";
		private const string JoinTypeRightJoin = "RIGHT JOIN";
		private const string JoinTypeFullJoin = "FULL JOIN";
		private const string JoinTypeCrossJoin = "CROSS JOIN";

		private static readonly OracleCodeCompletionItem[] JoinClauses =
		{
			new OracleCodeCompletionItem { Name = JoinTypeJoin, Text = JoinTypeJoin, Priority = 0, Category = OracleCodeCompletionCategory.JoinMethod, CategoryPriority = 1 },
			new OracleCodeCompletionItem { Name = JoinTypeLeftJoin, Text = JoinTypeLeftJoin, Priority = 1, Category = OracleCodeCompletionCategory.JoinMethod, CategoryPriority = 1 },
			new OracleCodeCompletionItem { Name = JoinTypeRightJoin, Text = JoinTypeRightJoin, Priority = 2, Category = OracleCodeCompletionCategory.JoinMethod, CategoryPriority = 1 },
			new OracleCodeCompletionItem { Name = JoinTypeFullJoin, Text = JoinTypeFullJoin, Priority = 3, Category = OracleCodeCompletionCategory.JoinMethod, CategoryPriority = 1 },
			new OracleCodeCompletionItem { Name = JoinTypeCrossJoin, Text = JoinTypeCrossJoin, Priority = 4, Category = OracleCodeCompletionCategory.JoinMethod, CategoryPriority = 1 }
		};

		public ICollection<FunctionOverloadDescription> ResolveFunctionOverloads(StatementCollection statementCollection, IDatabaseModel databaseModel, int cursorPosition)
		{
			var emptyCollection = new FunctionOverloadDescription[0];
			var node = statementCollection.GetNodeAtPosition(cursorPosition, n => !n.Id.In(Terminals.Comma, Terminals.RightParenthesis));
			if (node == null)
				return emptyCollection;

			var oracleDatabaseModel = (OracleDatabaseModelBase)databaseModel;
			var semanticModel = new OracleStatementSemanticModel(null, (OracleStatement)node.Statement, oracleDatabaseModel);
			var queryBlock = semanticModel.GetQueryBlock(cursorPosition);
			var functionReference = queryBlock.AllFunctionReferences.FirstOrDefault(f => node.HasAncestor(f.RootNode));
			if (functionReference == null || functionReference.Metadata == null)
				return emptyCollection;

			var currentParameterIndex = -1;
			if (functionReference.ParameterNodes != null && functionReference.ParameterNodes.Count > 0)
			{
				var parameterNode = functionReference.ParameterNodes.FirstOrDefault(f => node.HasAncestor(f));
				currentParameterIndex = functionReference.ParameterNodes.ToList().IndexOf(parameterNode);
			}

			var functionOverloads = oracleDatabaseModel.AllFunctionMetadata.SqlFunctions.Where(m => functionReference.Metadata.Identifier.EqualsWithAnyOverload(m.Identifier) && (m.Parameters.Count == 0 || currentParameterIndex < m.Parameters.Count - 1)).ToArray();

			return functionOverloads.Select(o =>
				new FunctionOverloadDescription
				{
					Name = functionReference.Metadata.Identifier.FullyQualifiedIdentifier,
					Parameters = o.Parameters.Skip(1).Select(p => p.Name + ": " + p.DataType).ToArray(),
					CurrentParameterIndex = currentParameterIndex,
					ReturnedDatatype = o.Parameters.First().DataType
				}).ToArray();
		}

		internal ICollection<ICodeCompletionItem> ResolveItems(IDatabaseModel databaseModel, string statementText, int cursorPosition, params string[] categories)
		{
			var sourceItems = ResolveItems(SqlDocument.FromStatementCollection(_parser.Parse(statementText), statementText), databaseModel, statementText, cursorPosition);
			return sourceItems.Where(i => categories.Length == 0 || categories.Contains(i.Category)).ToArray();
		}

		public ICollection<ICodeCompletionItem> ResolveItems(SqlDocument sqlDocument, IDatabaseModel databaseModel, string statementText, int cursorPosition)
		{
			//Trace.WriteLine("OracleCodeCompletionProvider.ResolveItems called. Cursor position: "+ cursorPosition);

			if (sqlDocument == null || sqlDocument.StatementCollection == null)
				return EmptyCollection;

			//var e = new OracleCodeCompletionType(sqlDocument.StatementCollection, statementText, cursorPosition);
			//e.PrintSupportedCompletions();

			StatementDescriptionNode currentNode;

			var completionItems = Enumerable.Empty<ICodeCompletionItem>();
			var statement = (OracleStatement)sqlDocument.StatementCollection.SingleOrDefault(s => s.GetNodeAtPosition(cursorPosition) != null);
			//
			/*currentNode = statements.GetTerminalAtPosition(cursorPosition);
			var isCursorAtTerminal = true;
			if (currentNode == null)
			{
				var statement = (OracleStatement)statements.LastOrDefault(s => s.GetNearestTerminalToPosition(cursorPosition) != null);
				if (statement != null)
				{
					currentNode = statement.GetNearestTerminalToPosition(cursorPosition);
					isCursorAtTerminal = false;
				}
			}

			if (currentNode == null)
				return EmptyCollection;*/

			//

			var isCursorAtTerminal = true;
			if (statement == null)
			{
				isCursorAtTerminal = false;

				statement = (OracleStatement)sqlDocument.StatementCollection.LastOrDefault(s => s.GetNearestTerminalToPosition(cursorPosition) != null);
				if (statement == null)
				{
					return EmptyCollection;
				}

				currentNode = statement.GetNearestTerminalToPosition(cursorPosition);

				var extraLength = cursorPosition - currentNode.SourcePosition.IndexEnd - 1;
				if (extraLength > 0)
				{
					var substring = statementText.Substring(currentNode.SourcePosition.IndexEnd + 1, extraLength).Trim();
					if (!String.IsNullOrEmpty(substring))
					{
						return EmptyCollection;
					}
				}
			}
			else
			{
				currentNode = statement.GetNodeAtPosition(cursorPosition);
				if (currentNode.Type == NodeType.NonTerminal)
				{
					currentNode = statement.GetNearestTerminalToPosition(cursorPosition);
					isCursorAtTerminal = currentNode.SourcePosition.IndexEnd + 1 == cursorPosition;
				}
				else if (currentNode.Id.In(Terminals.RightParenthesis, Terminals.Comma, Terminals.Semicolon))
				{
					var precedingNode = statement.GetNearestTerminalToPosition(cursorPosition - 1);
					if (precedingNode != null)
					{
						currentNode = precedingNode;
						isCursorAtTerminal = false;
					}
				}
			}

			var oracleDatabaseModel = (OracleDatabaseModelBase)databaseModel;
			var semanticModel = new OracleStatementSemanticModel(null, (OracleStatement)currentNode.Statement, oracleDatabaseModel);
			var terminalCandidates = new HashSet<string>(_parser.GetTerminalCandidates(isCursorAtTerminal && !currentNode.Id.IsSingleCharacterTerminal() ? currentNode.PrecedingTerminal : currentNode));
			//var terminalCandidatesNew = new HashSet<string>(_parser.GetTerminalCandidatesNew(isCursorAtTerminal && !currentNode.Id.IsSingleCharacterTerminal() ? currentNode.PrecedingTerminal : currentNode));

			var cursorAtLastTerminal = cursorPosition <= currentNode.SourcePosition.IndexEnd + 1;
			var terminalToReplace = cursorAtLastTerminal ? currentNode : null;
			var queryBlock = semanticModel.GetQueryBlock(currentNode);
			var extraOffset = currentNode.SourcePosition.IndexStart + currentNode.SourcePosition.Length == cursorPosition && currentNode.Id != Terminals.LeftParenthesis ? 1 : 0;

			var fromClause = currentNode.GetPathFilterAncestor(n => n.Id != NonTerminals.NestedQuery, NonTerminals.FromClause);
			if ((currentNode.Id == Terminals.From && !cursorAtLastTerminal) ||
				(currentNode.Id.In(Terminals.ObjectIdentifier, Terminals.Comma) && fromClause != null))
			{
				var schemaName = databaseModel.CurrentSchema;
				var schemaFound = false;
				if (currentNode.Id == Terminals.ObjectIdentifier && currentNode.ParentNode.Id == NonTerminals.QueryTableExpression &&
				    currentNode.ParentNode.FirstTerminalNode.Id == Terminals.SchemaIdentifier)
				{
					schemaFound = true;
					schemaName = currentNode.ParentNode.FirstTerminalNode.Token.Value;
				}

				var currentName = currentNode.Id.In(Terminals.From, Terminals.Comma) ? null : statementText.Substring(currentNode.SourcePosition.IndexStart, cursorPosition - currentNode.SourcePosition.IndexStart);
				if (String.IsNullOrEmpty(currentName) || currentName == currentName.Trim())
				{
					completionItems = completionItems.Concat(GenerateSchemaObjectItems(oracleDatabaseModel, schemaName, currentName, terminalToReplace, extraOffset, true));

					if (!schemaFound)
					{
						completionItems = completionItems.Concat(GenerateSchemaItems(currentName, terminalToReplace, extraOffset, oracleDatabaseModel));
					}

					completionItems = completionItems.Concat(GenerateCommonTableExpressionReferenceItems(semanticModel, currentName, terminalToReplace, extraOffset));
				}
			}

			if (currentNode.Id == Terminals.Dot &&
				currentNode.ParentNode.Id == NonTerminals.SchemaPrefix &&
				!currentNode.IsWithinSelectClauseOrExpression())
			{
				var ownerName = currentNode.ParentNode.ChildNodes.Single(n => n.Id == Terminals.SchemaIdentifier).Token.Value;
				completionItems = completionItems.Concat(GenerateSchemaObjectItems(oracleDatabaseModel, ownerName, null, null, 0, true));
			}

			var joinClauseNode = currentNode.GetPathFilterAncestor(n => n.Id != NonTerminals.FromClause, NonTerminals.JoinClause);
			if (currentNode.Id.In(Terminals.ObjectIdentifier, Terminals.ObjectAlias, Terminals.On))
			{
				if (joinClauseNode != null && !cursorAtLastTerminal)
				{
					var isNotInnerJoin = joinClauseNode.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.InnerJoinClause) == null;
					if (isNotInnerJoin || (!joinClauseNode.FirstTerminalNode.Id.In(Terminals.Cross, Terminals.Natural)))
					{
						var joinedTableReferenceNodes = joinClauseNode.GetPathFilterDescendants(n => !n.Id.In(NonTerminals.JoinClause, NonTerminals.NestedQuery), NonTerminals.TableReference).ToArray();
						if (joinedTableReferenceNodes.Length == 1)
						{
							var joinedTableReference = queryBlock.ObjectReferences.SingleOrDefault(t => t.TableReferenceNode == joinedTableReferenceNodes[0]);
							if (joinedTableReference != null)
							{
								foreach (var parentTableReference in queryBlock.ObjectReferences
									.Where(t => t.TableReferenceNode.SourcePosition.IndexStart < joinedTableReference.TableReferenceNode.SourcePosition.IndexStart &&
									            (t.Type != TableReferenceType.InlineView || t.AliasNode != null)))
								{
									var joinSuggestions = GenerateJoinConditionSuggestionItems(parentTableReference, joinedTableReference, currentNode.Id == Terminals.On, extraOffset);
									completionItems = completionItems.Concat(joinSuggestions);
								}
							}
						}
					}
				}
			}

			if ((currentNode.Id.In(Terminals.ObjectIdentifier, Terminals.ObjectAlias) ||
			    (joinClauseNode != null && joinClauseNode.IsGrammarValid)) &&
				!cursorAtLastTerminal)
			{
				var tableReference = currentNode.GetPathFilterAncestor(n => n.Id != NonTerminals.NestedQuery, NonTerminals.TableReference);
				if ((tableReference != null && currentNode == tableReference.LastTerminalNode && tableReference.ParentNode.Id == NonTerminals.FromClause && tableReference == tableReference.ParentNode.ChildNodes.First()) ||
					(joinClauseNode != null && joinClauseNode.IsGrammarValid))
				{
					completionItems = completionItems.Concat(
						//JoinClauses.Where(j => alias == null || j.Name.Contains(alias.Token.Value.ToUpperInvariant()))
						JoinClauses);
				}
			}

			if (currentNode.Id == Terminals.Join ||
				(currentNode.Id == Terminals.ObjectAlias && currentNode.Token.Value.ToUpperInvariant() == Terminals.Join.ToUpperInvariant()))
			{
				completionItems = completionItems.Concat(GenerateSchemaObjectItems(oracleDatabaseModel, databaseModel.CurrentSchema, null, null, extraOffset, true));
				completionItems = completionItems.Concat(GenerateSchemaItems(null, null, extraOffset, oracleDatabaseModel));
				completionItems = completionItems.Concat(GenerateCommonTableExpressionReferenceItems(semanticModel, null, null, extraOffset));
			}

			if (queryBlock != null && !isCursorAtTerminal && joinClauseNode == null && fromClause == null && !currentNode.IsWithinHavingClause() &&
				terminalCandidates.Contains(Terminals.ObjectIdentifier))
			{
				var whereTableReferences = queryBlock.ObjectReferences
					.Where(o => !String.IsNullOrEmpty(o.FullyQualifiedName.ToString()))
					.Select(o => new OracleCodeCompletionItem
					             {
									 Name = o.FullyQualifiedName.ToString(),
									 Category = o.Type.ToCategoryLabel(),
									 Offset = extraOffset,
									 Text = o.FullyQualifiedName.ToString()
					             });

				completionItems = completionItems.Concat(whereTableReferences);
			}

			if (currentNode.IsWithinSelectClauseOrExpression() &&
				terminalCandidates.Contains(Terminals.Identifier) &&
				currentNode.Id.In(Terminals.ObjectIdentifier, Terminals.Identifier, Terminals.Comma, Terminals.Dot, Terminals.Select))
			{
				completionItems = completionItems.Concat(GenerateSelectListItems(currentNode, semanticModel, cursorPosition, oracleDatabaseModel));
			}

			return completionItems.OrderItems().ToArray();

			// TODO: Add option to search all/current/public schemas
		}

		private IEnumerable<ICodeCompletionItem> GenerateSelectListItems(StatementDescriptionNode currentNode, OracleStatementSemanticModel semanticModel, int cursorPosition, OracleDatabaseModelBase databaseModel)
		{
			var prefixedColumnReference = currentNode.GetPathFilterAncestor(n => n.Id != NonTerminals.Expression, NonTerminals.PrefixedColumnReference);
			var columnIdentifierFollowing = currentNode.Id != Terminals.Identifier && prefixedColumnReference != null && prefixedColumnReference.GetDescendants(Terminals.Identifier).FirstOrDefault() != null;
			if (!currentNode.IsWithinSelectClauseOrExpression() || columnIdentifierFollowing)
			{
				return EmptyCollection;
			}
			
			var queryBlock = semanticModel.GetQueryBlock(currentNode);
			var objectIdentifierNode = currentNode.ParentNode.Id == NonTerminals.ObjectPrefix ? currentNode.ParentNode.GetSingleDescendant(Terminals.ObjectIdentifier) : null;
			if (objectIdentifierNode == null && prefixedColumnReference != null)
			{
				objectIdentifierNode = prefixedColumnReference.ChildNodes[0].GetSingleDescendant(Terminals.ObjectIdentifier);
			}

			var partialName = currentNode.Id == Terminals.Identifier && cursorPosition <= currentNode.SourcePosition.IndexEnd + 1
				? currentNode.Token.Value.Substring(0, cursorPosition - currentNode.SourcePosition.IndexStart).Trim('"')
				: null;

			var tableReferences = queryBlock.ObjectReferences;
			var suggestedFunctions = Enumerable.Empty<ICodeCompletionItem>();
			if (objectIdentifierNode != null)
			{
				var schemaIdentifier = currentNode.ParentNode.ParentNode.GetSingleDescendant(Terminals.SchemaIdentifier);
				var schemaName = schemaIdentifier == null ? null : schemaIdentifier.Token.Value;
				var objectName = objectIdentifierNode.Token.Value;
				var fullyQualifiedName = OracleObjectIdentifier.Create(schemaName, objectName);
				tableReferences = tableReferences
					.Where(t => t.FullyQualifiedName == fullyQualifiedName || (String.IsNullOrEmpty(fullyQualifiedName.Owner) && fullyQualifiedName.NormalizedName == t.FullyQualifiedName.NormalizedName))
					.ToArray();

				if (tableReferences.Count == 0 && (partialName != null || currentNode.SourcePosition.IndexEnd < cursorPosition))
				{
					var functionReference = semanticModel.GetQueryBlock(currentNode).AllFunctionReferences.SingleOrDefault(f => f.FunctionIdentifierNode == currentNode);
					var addParameterList = functionReference == null;
					var currentName = partialName == null ? null : currentNode.Token.Value;

					if (String.IsNullOrEmpty(schemaName))
					{
						var matcher = new OracleFunctionMatcher(
							new FunctionMatchElement(objectName).SelectOwner(),
							new FunctionMatchElement(partialName) { AllowStartWithMatch = true, DeniedValue = currentName }.SelectPackage(),
							null);

						suggestedFunctions = GenerateCodeItems(m => m.Identifier.Package.ToSimpleIdentifier(), OracleCodeCompletionCategory.Package, partialName == null ? null : currentNode, 0, addParameterList, databaseModel, matcher);

						matcher = new OracleFunctionMatcher(
							new FunctionMatchElement(databaseModel.CurrentSchema).SelectOwner(), 
							new FunctionMatchElement(objectName).SelectPackage(),
							new FunctionMatchElement(partialName) { AllowStartWithMatch = true, DeniedValue = currentName }.SelectName());
						suggestedFunctions = suggestedFunctions.Concat(GenerateCodeItems(m => m.Identifier.Name.ToSimpleIdentifier(), OracleCodeCompletionCategory.PackageFunction, partialName == null ? null : currentNode, 0, addParameterList, databaseModel, matcher));

						matcher = new OracleFunctionMatcher(
							new FunctionMatchElement(objectName).SelectOwner(),
							new FunctionMatchElement(null).SelectPackage(),
							new FunctionMatchElement(partialName) { AllowStartWithMatch = true, DeniedValue = currentName }.SelectName());

						suggestedFunctions = suggestedFunctions.Concat(GenerateCodeItems(m => m.Identifier.Name.ToSimpleIdentifier(), OracleCodeCompletionCategory.SchemaFunction, partialName == null ? null : currentNode, 0, addParameterList, databaseModel, matcher));
					}
					else
					{
						var matcher = new OracleFunctionMatcher(
							new FunctionMatchElement(schemaName).SelectOwner(),
							new FunctionMatchElement(objectName).SelectPackage(),
							new FunctionMatchElement(partialName) { AllowStartWithMatch = true, DeniedValue = currentName }.SelectName());

						suggestedFunctions = GenerateCodeItems(m => m.Identifier.Name.ToSimpleIdentifier(), OracleCodeCompletionCategory.PackageFunction, partialName == null ? null : currentNode, 0, addParameterList, databaseModel, matcher);
					}
				}
			}

			var columnCandidates = tableReferences
				.SelectMany(t => t.Columns
					.Where(c =>
						(currentNode.Id != Terminals.Identifier || c.Name != currentNode.Token.Value.ToQuotedIdentifier()) &&
						(objectIdentifierNode == null && String.IsNullOrEmpty(partialName) ||
						(c.Name != partialName.ToQuotedIdentifier() && c.Name.ToRawUpperInvariant().Contains(partialName.ToRawUpperInvariant()))))
					.Select(c => new { TableReference = t, Column = c }))
					.GroupBy(c => c.Column.Name).ToDictionary(g => g.Key ?? String.Empty, g => g.Select(o => o.TableReference).ToArray());

			var suggestedColumns = new List<Tuple<string, OracleObjectIdentifier>>();
			foreach (var columnCandidate in columnCandidates)
			{
				suggestedColumns.AddRange(OracleObjectIdentifier.GetUniqueReferences(columnCandidate.Value.Select(t => t.FullyQualifiedName).ToArray())
					.Select(objectIdentifier => new Tuple<string, OracleObjectIdentifier>(columnCandidate.Key, objectIdentifier)));
			}

			var rowIdItems = columnCandidates.Values.SelectMany(v => v)
				.Distinct()
				.Where(o => o.SearchResult.SchemaObject != null && o.SearchResult.SchemaObject.Organization.In(OrganizationType.Heap, OrganizationType.Index) &&
							o.SearchResult.SchemaObject.Type == OracleObjectType.Table && suggestedColumns.Select(t => t.Item2).Contains(o.FullyQualifiedName))
				.Select(o => CreateColumnCodeCompletionItem(OracleColumn.RowId, objectIdentifierNode == null ? o.FullyQualifiedName.ToString() : null, currentNode, OracleCodeCompletionCategory.PseudoColumn));

			var suggestedItems = rowIdItems.Concat(suggestedColumns.Select(t => CreateColumnCodeCompletionItem(t.Item1, objectIdentifierNode == null ? t.Item2.ToString() : null, currentNode)));

			if (partialName == null && currentNode.IsWithinSelectClause() && currentNode.GetParentExpression().GetParentExpression() == null)
			{
				suggestedItems = suggestedItems.Concat(CreateAsteriskColumnCompletionItems(tableReferences, objectIdentifierNode != null, currentNode));
			}

			if (objectIdentifierNode == null)
			{
				suggestedItems = suggestedItems.Concat(GenerateSchemaItems(partialName, currentNode.Id == Terminals.Select ? null : currentNode, 0, databaseModel, 1));
			}

			return suggestedItems.Concat(suggestedFunctions);
		}

		private IEnumerable<OracleCodeCompletionItem> CreateAsteriskColumnCompletionItems(IEnumerable<OracleObjectReference> tables, bool skipFirstObjectIdentifier, StatementDescriptionNode currentNode)
		{
			var builder = new StringBuilder();
			
			foreach (var table in tables)
			{
				if (table.Columns.Count <= 1)
					continue;

				builder.Clear();
				var isFirstColumn = true;
				var skipTablePrefix = skipFirstObjectIdentifier;

				foreach (var column in table.Columns)
				{
					if (!isFirstColumn)
					{
						builder.Append(", ");
					}

					if (!skipTablePrefix && !String.IsNullOrEmpty(table.FullyQualifiedName.Name))
					{
						builder.Append(table.FullyQualifiedName);
						builder.Append(".");
					}
					
					builder.Append(column.Name.ToSimpleIdentifier());

					isFirstColumn = false;
					skipTablePrefix = false;
				}

				yield return new OracleCodeCompletionItem
				             {
								 Name = (skipFirstObjectIdentifier || String.IsNullOrEmpty(table.FullyQualifiedName.Name) ? String.Empty : table.FullyQualifiedName + ".") + "*",
								 Text = builder.ToString(),
								 StatementNode = currentNode.Id == Terminals.Identifier ? currentNode : null,
								 CategoryPriority = -2,
								 Category = OracleCodeCompletionCategory.AllColumns
				             };
			}
		}

		private ICodeCompletionItem CreateColumnCodeCompletionItem(string columnName, string objectPrefix, StatementDescriptionNode currentNode, string category = OracleCodeCompletionCategory.Column)
		{
			if (!String.IsNullOrEmpty(objectPrefix))
				objectPrefix += ".";

			var text = objectPrefix + columnName.ToSimpleIdentifier();

			return new OracleCodeCompletionItem
			       {
					   Name = text,
					   Text = text,
				       StatementNode = currentNode.Id == Terminals.Identifier ? currentNode : null,
				       Category = category,
					   CategoryPriority = -1
			       };
		}

		private IEnumerable<ICodeCompletionItem> GenerateSchemaItems(string schemaNamePart, StatementDescriptionNode node, int insertOffset, OracleDatabaseModelBase databaseModel, int priorityOffset = 0)
		{
			return databaseModel.AllSchemas
				.Where(s => s != OracleDatabaseModelBase.SchemaPublic && (MakeSaveQuotedIdentifier(schemaNamePart) != s && (String.IsNullOrEmpty(schemaNamePart) || s.ToUpperInvariant().Contains(schemaNamePart.Trim('"').ToUpperInvariant()))))
				.Select(s => new OracleCodeCompletionItem
				             {
								 Name = s.ToSimpleIdentifier(),
								 Text = s.ToSimpleIdentifier(),
								 StatementNode = node,
								 Category = OracleCodeCompletionCategory.DatabaseSchema,
								 Offset = insertOffset,
								 CategoryPriority = 1 + priorityOffset
				             });
		}

		private IEnumerable<ICodeCompletionItem> GenerateCodeItems(Func<OracleFunctionMetadata, string> identifierSelector, string category, StatementDescriptionNode node, int insertOffset, bool addParameterList, OracleDatabaseModelBase databaseModel, params OracleFunctionMatcher[] matchers)
		{
			string parameterList = null;
			var parameterListCaretOffset = 0;
			if (addParameterList)
			{
				parameterList = "()";
				parameterListCaretOffset = -1;
			}
			
			return databaseModel.AllFunctionMetadata.SqlFunctions
				.Where(f => matchers.Any(m => m.IsMatch(f, databaseModel.CurrentSchema)) && !String.IsNullOrEmpty(identifierSelector(f)))
				.Select(f => identifierSelector(f).ToSimpleIdentifier())
				.Distinct()
				.Select(i => new OracleCodeCompletionItem
				             {
					             Name = i,
								 Text = i + (category == OracleCodeCompletionCategory.Package ? "." : parameterList),
					             StatementNode = node,
					             Category = category,
					             Offset = insertOffset,
								 CaretOffset = category == OracleCodeCompletionCategory.Package ? 0 : parameterListCaretOffset,
					             CategoryPriority = 2
				             });
		}

		private IEnumerable<ICodeCompletionItem> GenerateSchemaObjectItems(OracleDatabaseModelBase databaseModel, string schemaName, string objectNamePart, StatementDescriptionNode node, int insertOffset, bool dataObjectsOnly)
		{
			return databaseModel.AllObjects.Values
						.Where(o => (!dataObjectsOnly || IsDataObject(o)) &&
							o.Owner == MakeSaveQuotedIdentifier(schemaName) && MakeSaveQuotedIdentifier(objectNamePart) != o.Name &&
							(node == null || node.Token.Value.ToQuotedIdentifier() != o.Name) &&
							(String.IsNullOrEmpty(objectNamePart) || o.Name.ToUpperInvariant().Contains(objectNamePart.ToUpperInvariant())))
						.Select(o => new OracleCodeCompletionItem
						{
							Name = o.Name.ToSimpleIdentifier(),
							Text = o.Name.ToSimpleIdentifier(),
							StatementNode = node,
							Category = OracleCodeCompletionCategory.SchemaObject,
							Offset = insertOffset
						});
		}

		private string MakeSaveQuotedIdentifier(string identifierPart)
		{
			if (String.IsNullOrEmpty(identifierPart) || identifierPart.All(c => c == '"'))
				return null;

			var preFix = identifierPart[0] != '"' && identifierPart[identifierPart.Length - 1] == '"' ? "\"" : null;
			var postFix = identifierPart[0] == '"' && identifierPart[identifierPart.Length - 1] != '"' ? "\"" : null;
			return String.Format("{0}{1}{2}", preFix, identifierPart, postFix).ToQuotedIdentifier();
		}

		private bool IsDataObject(OracleSchemaObject schemaObject)
		{
			var synonym = schemaObject as OracleSynonym;
			return schemaObject is OracleDataObject || (synonym != null && synonym.SchemaObject is OracleDataObject);
		}

		private IEnumerable<ICodeCompletionItem> GenerateCommonTableExpressionReferenceItems(OracleStatementSemanticModel model, string referenceNamePart, StatementDescriptionNode node, int insertOffset)
		{
			// TODO: Make proper resolution of CTE accessibility
			return model.QueryBlocks
						.Where(qb => qb.Type == QueryBlockType.CommonTableExpression && referenceNamePart.ToQuotedIdentifier() != qb.NormalizedAlias && (String.IsNullOrEmpty(referenceNamePart) || qb.Alias.ToUpperInvariant().Contains(referenceNamePart.ToUpperInvariant())))
						.Select(qb => new OracleCodeCompletionItem
						{
							Name = qb.Alias,
							Text = qb.Alias,
							StatementNode = node,
							Category = OracleCodeCompletionCategory.CommonTableExpression,
							Offset = insertOffset,
							CategoryPriority = -1
						});
		}

		private IEnumerable<ICodeCompletionItem> GenerateJoinConditionSuggestionItems(OracleObjectReference parentSchemaObject, OracleObjectReference joinedSchemaObject, bool skipOnTerminal, int insertOffset)
		{
			var codeItems = Enumerable.Empty<ICodeCompletionItem>();

			if (parentSchemaObject.Type == TableReferenceType.SchemaObject && joinedSchemaObject.Type == TableReferenceType.SchemaObject)
			{
				if (parentSchemaObject.SearchResult.SchemaObject == null || joinedSchemaObject.SearchResult.SchemaObject == null)
					return EmptyCollection;

				var parentObject = parentSchemaObject.SearchResult.SchemaObject;
				var joinedObject = joinedSchemaObject.SearchResult.SchemaObject;

				var joinedToParentKeys = parentObject.ForeignKeys.Where(k => k.TargetObject == joinedObject)
					.Select(k => GenerateJoinConditionSuggestionItem(parentSchemaObject.FullyQualifiedName, joinedSchemaObject.FullyQualifiedName, k.Columns, k.ReferenceConstraint.Columns, false, skipOnTerminal, insertOffset));

				codeItems = codeItems.Concat(joinedToParentKeys);

				var parentToJoinedKeys = joinedObject.ForeignKeys.Where(k => k.TargetObject == parentObject)
					.Select(k => GenerateJoinConditionSuggestionItem(joinedSchemaObject.FullyQualifiedName, parentSchemaObject.FullyQualifiedName, k.Columns, k.ReferenceConstraint.Columns, true, skipOnTerminal, insertOffset));

				codeItems = codeItems.Concat(parentToJoinedKeys);
			}
			else
			{
				var columnNameJoinConditions = parentSchemaObject.Columns
					.Where(c => !String.IsNullOrEmpty(c.Name)).Select(c => c.Name)
					.Intersect(
						joinedSchemaObject.Columns
						.Where(c => !String.IsNullOrEmpty(c.Name)).Select(c => c.Name))
					.Select(c => GenerateJoinConditionSuggestionItem(parentSchemaObject.FullyQualifiedName, joinedSchemaObject.FullyQualifiedName, new[] { c }, new[] { c }, false, skipOnTerminal, insertOffset));

				codeItems = codeItems.Concat(columnNameJoinConditions);
			}

			return codeItems;
		}

		private OracleCodeCompletionItem GenerateJoinConditionSuggestionItem(OracleObjectIdentifier sourceObject, OracleObjectIdentifier targetObject, IList<string> keySourceColumns, IList<string> keyTargetColumns, bool swapSides, bool skipOnTerminal, int insertOffset)
		{
			var builder = new StringBuilder();
			if (!skipOnTerminal)
			{
				builder.Append(Terminals.On.ToUpperInvariant());
				builder.Append(" ");
			}

			var logicalOperator = String.Empty;

			for (var i = 0; i < keySourceColumns.Count; i++)
			{
				builder.Append(logicalOperator);
				builder.Append(swapSides ? targetObject : sourceObject);
				builder.Append('.');
				builder.Append((swapSides ? keyTargetColumns[i] : keySourceColumns[i]).ToSimpleIdentifier());
				builder.Append(" = ");
				builder.Append(swapSides ? sourceObject : targetObject);
				builder.Append('.');
				builder.Append((swapSides ? keySourceColumns[i] : keyTargetColumns[i]).ToSimpleIdentifier());

				logicalOperator = " AND ";
			}

			return new OracleCodeCompletionItem { Name = builder.ToString(), Text = builder.ToString(), Offset = insertOffset };
		}
	}

	internal class OracleCodeCompletionType
	{
		private readonly OracleSqlParser _parser = new OracleSqlParser();

		private static readonly HashSet<string> ZeroOffsetTerminalIds =
			new HashSet<string>
			{
				Terminals.Dot,
				Terminals.Comma,
				Terminals.OperatorConcatenation,
				Terminals.LeftParenthesis,
				Terminals.RightParenthesis,
				Terminals.MathDivide,
				Terminals.MathEquals,
				Terminals.MathFactor,
				Terminals.MathGreatherThan,
				Terminals.MathGreatherThanOrEquals,
				Terminals.MathLessThan,
				Terminals.MathLessThanOrEquals,
				Terminals.MathMinus,
				Terminals.MathNotEqualsC,
				Terminals.MathNotEqualsCircumflex,
				Terminals.MathNotEqualsSql,
				Terminals.MathPlus
			};

		public bool Schema { get; private set; }

		public bool SchemaDataObject { get; private set; }
		
		public bool PipelinedFunction { get; private set; }
		
		public bool SchemaDataObjectReference { get; private set; }
		
		public bool Column { get; private set; }

		public bool AllColumns { get; private set; }
		
		public bool JoinType { get; private set; }
		
		public bool JoinCondition { get; private set; }
		
		public bool Program { get; private set; }

		public OracleCodeCompletionType(StatementCollection statementCollection, string statementText, int cursorPosition)
		{
			var statement = (OracleStatement)(statementCollection.GetStatementAtPosition(cursorPosition) ?? statementCollection.LastOrDefault());
			if (statement == null)
				return;

			var nearestTerminal = statement.GetNearestTerminalToPosition(cursorPosition);
			if (nearestTerminal == null)
				return;

			var requiredOffsetAfterToken = ZeroOffsetTerminalIds.Contains(nearestTerminal.Id) ? 0 : 1;
			var isCursorAfterToken = nearestTerminal.SourcePosition.IndexEnd + requiredOffsetAfterToken < cursorPosition;
			if (isCursorAfterToken)
			{
				var unparsedTextBetweenTokenAndCursor = statementText.Substring(nearestTerminal.SourcePosition.IndexEnd + 1, cursorPosition - nearestTerminal.SourcePosition.IndexEnd - 1).Trim();
				if (!String.IsNullOrEmpty(unparsedTextBetweenTokenAndCursor))
					return;
			}

			var terminalCandidates = new HashSet<string>(_parser.GetTerminalCandidates(isCursorAfterToken ? nearestTerminal : nearestTerminal.PrecedingTerminal));
			Schema = terminalCandidates.Contains(Terminals.SchemaIdentifier);
			Program = Column = terminalCandidates.Contains(Terminals.Identifier);
			JoinType = terminalCandidates.Contains(Terminals.Join);

			var isWithinFromClause = nearestTerminal.GetPathFilterAncestor(n => n.Id != NonTerminals.QueryBlock, NonTerminals.FromClause) != null || (isCursorAfterToken && nearestTerminal.Id == Terminals.From);
			var isWithinJoinCondition = nearestTerminal.GetPathFilterAncestor(n => n.Id != NonTerminals.JoinClause, NonTerminals.JoinColumnsOrCondition) != null;
			SchemaDataObject = isWithinFromClause && !isWithinJoinCondition && terminalCandidates.Contains(Terminals.ObjectIdentifier);

			var isWithinJoinClause = nearestTerminal.GetPathFilterAncestor(n => n.Id != NonTerminals.FromClause, NonTerminals.JoinClause) != null;
			JoinCondition = isWithinJoinClause && isCursorAfterToken && (terminalCandidates.Contains(Terminals.On) || nearestTerminal.Id == Terminals.On);

			var isWithinSelectList = (nearestTerminal.Id == Terminals.Select && isCursorAfterToken) || nearestTerminal.GetPathFilterAncestor(n => n.Id != NonTerminals.QueryBlock, NonTerminals.SelectList) != null;
			AllColumns = isWithinSelectList && terminalCandidates.Contains(Terminals.Asterisk);

			SchemaDataObjectReference = !isWithinFromClause && terminalCandidates.Contains(Terminals.ObjectIdentifier);
		}

		public void PrintSupportedCompletions()
		{
			var builder = new StringBuilder(255);
			builder.Append("Schema: ");
			builder.Append(Schema);
			builder.Append("; ");
			builder.Append("SchemaDataObject: ");
			builder.Append(SchemaDataObject);
			builder.Append("; ");
			builder.Append("SchemaDataObjectReference: ");
			builder.Append(SchemaDataObjectReference);
			builder.Append("; ");
			builder.Append("Column: ");
			builder.Append(Column);
			builder.Append("; ");
			builder.Append("AllColumns: ");
			builder.Append(AllColumns);
			builder.Append("; ");
			builder.Append("JoinType: ");
			builder.Append(JoinType);
			builder.Append("; ");
			builder.Append("JoinCondition: ");
			builder.Append(JoinCondition);
			builder.Append("; ");
			builder.Append("Program: ");
			builder.Append(Program);

			Trace.WriteLine(builder.ToString());
		}
	}
}
