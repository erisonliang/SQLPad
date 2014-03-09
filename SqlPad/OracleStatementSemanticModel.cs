using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NonTerminals = SqlPad.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.OracleGrammarDescription.Terminals;

namespace SqlPad
{
	public class OracleStatementSemanticModel
	{
		private readonly List<OracleQueryBlock> _queryBlockResults = new List<OracleQueryBlock>();
		//private readonly OracleStatement _statement;

		public ICollection<OracleQueryBlock> QueryBlocks
		{
			get { return _queryBlockResults.AsReadOnly(); }
		}

		public OracleStatementSemanticModel(string sqlText, OracleStatement statement)
		{
			if (statement == null)
				throw new ArgumentNullException("statement");

			//_statement = statement;

			var queryBlocks = statement.NodeCollection.SelectMany(n => n.GetDescendants(NonTerminals.QueryBlock))
				.OrderByDescending(q => q.Level).ToArray();

			foreach (var queryBlock in queryBlocks)
			{
				var item = new OracleQueryBlock
				           {
					           TableReferences = new List<OracleTableReference>(),
							   Columns = new List<OracleSelectListColumn>(),
					           RootNode = queryBlock
				           };
				
				_queryBlockResults.Add(item);

				var fromClause = queryBlock.GetDescendantsWithinSameQuery(NonTerminals.FromClause).First();
				var tableReferenceNonterminals = fromClause.GetDescendantsWithinSameQuery(NonTerminals.TableReference).ToArray();

				var scalarSubqueryExpression = queryBlock.GetAncestor(NonTerminals.Expression, false);
				if (scalarSubqueryExpression != null)
				{
					item.Type = QueryBlockType.ScalarSubquery;
				}

				var factoredSubqueryReference = queryBlock.GetPathFilterAncestor(NodeFilters.BreakAtNestedQueryBoundary, NonTerminals.SubqueryComponent, false);
				if (factoredSubqueryReference != null)
				{
					item.Alias = factoredSubqueryReference.ChildNodes.First().Token.Value.ToOracleIdentifier();
					item.Type = QueryBlockType.CommonTableExpression;
				}
				else
				{
					var selfTableReference = queryBlock.GetAncestor(NonTerminals.TableReference, false);
					if (selfTableReference != null)
					{
						item.Type = QueryBlockType.Normal;

						var nestedSubqueryAlias = selfTableReference.ChildNodes.SingleOrDefault(n => n.Id == Terminals.Alias);
						if (nestedSubqueryAlias != null)
						{
							item.Alias = nestedSubqueryAlias.Token.Value.ToOracleIdentifier();
						}
					}
				}

				foreach (var tableReferenceNonterminal in tableReferenceNonterminals)
				{
					var queryTableExpression = tableReferenceNonterminal.GetDescendantsWithinSameQuery(NonTerminals.QueryTableExpression).Single();
					var tableIdentifierNode = queryTableExpression.ChildNodes.FirstOrDefault(n => n.Id == Terminals.Identifier);

					if (tableIdentifierNode == null)
						continue;
					
					var schemaPrefixNode = queryTableExpression.ChildNodes.FirstOrDefault(n => n.Id == NonTerminals.SchemaPrefix);
					if (schemaPrefixNode != null)
					{
						schemaPrefixNode = schemaPrefixNode.ChildNodes.First();
					}

					var tableName = tableIdentifierNode.Token.Value.ToOracleIdentifier();
					var commonTableExpressions = schemaPrefixNode != null
						? new StatementDescriptionNode[0]
						: GetCommonTableExpressionReferences(queryBlock, tableName, sqlText).ToArray();
					
					var referenceType = commonTableExpressions.Length > 0 ? TableReferenceType.CommonTableExpression : TableReferenceType.PhysicalTable;

					var tableReferenceAlias = tableReferenceNonterminal.GetDescendantsWithinSameQuery(Terminals.Alias).SingleOrDefault();

					item.TableReferences.Add(new OracleTableReference
					                         {
						                         OwnerNode = schemaPrefixNode,
						                         TableNode = tableIdentifierNode,
						                         Type = referenceType, Nodes = commonTableExpressions,
												 AliasNode = tableReferenceAlias
					                         });
				}

				var selectList = queryBlock.GetDescendantsWithinSameQuery(NonTerminals.SelectList).Single();
				if (selectList.ChildNodes.Count == 1 && selectList.ChildNodes.Single().Id == Terminals.Asterisk)
				{

				}
				else
				{
					var columnExpressions = selectList.GetDescendantsWithinSameQuery(NonTerminals.AliasedExpressionOrAllTableColumns).ToArray();
					foreach (var columnExpression in columnExpressions)
					{
						var asteriskNode = columnExpression.GetDescendantsWithinSameQuery(Terminals.Asterisk).SingleOrDefault();
						var columnAliasNode = columnExpression.GetDescendantsWithinSameQuery(Terminals.Alias).SingleOrDefault();

						var column = new OracleSelectListColumn
						             {
										 AliasNode = columnAliasNode,
										 ColumnReferences = new List<OracleColumnReference>(),
										 RootNode = columnExpression
						             };

						var identifiers = columnExpression.GetDescendantsWithinSameQuery(Terminals.Identifier).ToArray();
						foreach (var identifier in identifiers)
						{
							column.IsPure = columnAliasNode == null && identifier.GetAncestor(NonTerminals.Expression).ChildNodes.Count == 1;
							if (column.IsPure)
							{
								column.AliasNode = identifier;
							}

							var prefixNonTerminal = identifier.GetPathFilterAncestor(n => n.Id != NonTerminals.Expression, NonTerminals.PrefixedColumnReference)
								.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.Prefix);

							var columnReference = new OracleColumnReference
							{
								ColumnNode = identifier,
								QueryNodes = new List<StatementDescriptionNode>()
							};

							if (prefixNonTerminal == null)
								continue;

							var objectIdentifier = prefixNonTerminal.GetSingleDescendant(Terminals.ObjectIdentifier);
							var schemaIdentifier = prefixNonTerminal.GetSingleDescendant(Terminals.SchemaIdentifier);

							columnReference.OwnerNode = schemaIdentifier;
							columnReference.TableNode = objectIdentifier;
							
							column.ColumnReferences.Add(columnReference);
						}

						item.Columns.Add(column);
					}
				}
			}
		}

		private IEnumerable<StatementDescriptionNode> GetNestedTableReferences(StatementDescriptionNode node, string normalizedReferenceName, string sqlText)
		{
			return null;
		}

		private IEnumerable<StatementDescriptionNode> GetCommonTableExpressionReferences(StatementDescriptionNode node, string normalizedReferenceName, string sqlText)
		{
			var queryRoot = node.GetAncestor(NonTerminals.NestedQuery, false);
			var subQueryCompondentDistance = node.GetAncestorDistance(NonTerminals.SubqueryComponent);
			if (subQueryCompondentDistance != null &&
			    node.GetAncestorDistance(NonTerminals.NestedQuery) > subQueryCompondentDistance)
			{
				queryRoot = queryRoot.GetAncestor(NonTerminals.NestedQuery, false);
			}

			if (queryRoot == null)
				return Enumerable.Empty<StatementDescriptionNode>();

			var commonTableExpressions = queryRoot
				.GetPathFilterDescendants(n => n.Id != NonTerminals.QueryBlock, NonTerminals.SubqueryComponent)
				.Where(cte => cte.ChildNodes.First().Token.Value.ToOracleIdentifier() == normalizedReferenceName);
			return commonTableExpressions.Concat(GetCommonTableExpressionReferences(queryRoot, normalizedReferenceName, sqlText));
		}
	}

	[DebuggerDisplay("OracleQueryBlock (Alias={Alias}; Type={Type}; RootNode={RootNode})")]
	public class OracleQueryBlock
	{
		public string Alias { get; set; }

		public QueryBlockType Type { get; set; }

		public StatementDescriptionNode RootNode { get; set; }

		public ICollection<OracleTableReference> TableReferences { get; set; }

		public ICollection<OracleSelectListColumn> Columns { get; set; }
	}

	[DebuggerDisplay("OracleTableReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Table={TableNode.Token.Value}; Alias={AliasNode == null ? null : AliasNode.Token.Value}; Type={Type})")]
	public class OracleTableReference
	{
		public StatementDescriptionNode OwnerNode { get; set; }

		public StatementDescriptionNode TableNode { get; set; }
		
		public StatementDescriptionNode AliasNode { get; set; }

		public ICollection<StatementDescriptionNode> Nodes { get; set; }

		public TableReferenceType Type { get; set; }
	}

	[DebuggerDisplay("OracleColumnReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Table={TableNode == null ? null : TableNode.Token.Value}; Column={ColumnNode.Token.Value})")]
	public class OracleColumnReference
	{
		public StatementDescriptionNode OwnerNode { get; set; }

		public StatementDescriptionNode TableNode { get; set; }

		public StatementDescriptionNode ColumnNode { get; set; }

		public ICollection<StatementDescriptionNode> QueryNodes { get; set; }
	}

	[DebuggerDisplay("OracleSelectListColumn (Alias={AliasNode == null ? null : AliasNode.Token.Value}; IsPure={IsPure})")]
	public class OracleSelectListColumn
	{
		public bool IsPure { get; set; }

		public StatementDescriptionNode AliasNode { get; set; }

		public StatementDescriptionNode RootNode { get; set; }

		public ICollection<OracleColumnReference> ColumnReferences { get; set; }

		public OracleColumn ColumnDescription { get; set; }
	}

	public enum TableReferenceType
	{
		PhysicalTable,
		CommonTableExpression
	}

	public enum QueryBlockType
	{
		Normal,
		ScalarSubquery,
		CommonTableExpression
	}

	public static class NodeFilters
	{
		public static bool BreakAtNestedQueryBoundary(StatementDescriptionNode node)
		{
			return node.Id != NonTerminals.NestedQuery;
		}
	}
}