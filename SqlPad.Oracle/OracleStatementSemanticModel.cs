using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	public class OracleStatementSemanticModel
	{
		private readonly Dictionary<StatementDescriptionNode, OracleQueryBlock> _queryBlockResults = new Dictionary<StatementDescriptionNode, OracleQueryBlock>();
		//private readonly OracleStatement _statement;

		public ICollection<OracleQueryBlock> QueryBlocks
		{
			get { return _queryBlockResults.Values; }
		}

		public OracleStatementSemanticModel(string sqlText, OracleStatement statement, DatabaseModelFake databaseModel)
		{
			if (statement == null)
				throw new ArgumentNullException("statement");

			//_statement = statement;

			_queryBlockResults = statement.NodeCollection.SelectMany(n => n.GetDescendants(NonTerminals.QueryBlock))
				.OrderByDescending(q => q.Level).ToDictionary(n => n, n => new OracleQueryBlock { RootNode = n });

			var allColumnReferences = new Dictionary<OracleSelectListColumn, ICollection<OracleTableReference>>();

			foreach (var queryBlockNode in _queryBlockResults)
			{
				var queryBlock = queryBlockNode.Key;
				var item = queryBlockNode.Value;

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

					var tableReferenceAlias = tableReferenceNonterminal.GetDescendantsWithinSameQuery(Terminals.Alias).SingleOrDefault();
					
					var nestedQueryTableReference = queryTableExpression.GetPathFilterDescendants(f => f.Id != NonTerminals.Subquery, NonTerminals.NestedQuery).SingleOrDefault();
					if (nestedQueryTableReference != null)
					{
						var nestedQueryTableReferenceQueryBlock = nestedQueryTableReference.GetPathFilterDescendants(n => n.Id != NonTerminals.NestedQuery && n.Id != NonTerminals.SubqueryFactoringClause, NonTerminals.QueryBlock).Single();

						item.TableReferences.Add(new OracleTableReference
						{
							TableNode = nestedQueryTableReferenceQueryBlock,
							Type = TableReferenceType.NestedQuery,
							AliasNode = tableReferenceAlias
						});

						continue;
					}

					var tableIdentifierNode = queryTableExpression.ChildNodes.SingleOrDefault(n => n.Id == Terminals.Identifier);

					if (tableIdentifierNode == null)
						continue;

					var schemaPrefixNode = queryTableExpression.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.SchemaPrefix);
					if (schemaPrefixNode != null)
					{
						schemaPrefixNode = schemaPrefixNode.ChildNodes.First();
					}

					var tableName = tableIdentifierNode.Token.Value.ToOracleIdentifier();
					var commonTableExpressions = schemaPrefixNode != null
						? new StatementDescriptionNode[0]
						: GetCommonTableExpressionReferences(queryBlock, tableName, sqlText).ToArray();
					
					var referenceType = commonTableExpressions.Length > 0 ? TableReferenceType.CommonTableExpression : TableReferenceType.PhysicalObject;

					// TODO: Resolve physical table columns

					item.TableReferences.Add(new OracleTableReference
					                         {
						                         OwnerNode = schemaPrefixNode,
						                         TableNode = tableIdentifierNode,
						                         Type = referenceType,
												 Nodes = commonTableExpressions,
												 AliasNode = tableReferenceAlias,
					                         });
				}

				var selectList = queryBlock.GetDescendantsWithinSameQuery(NonTerminals.SelectList).Single();
				if (selectList.ChildNodes.Count == 1 && selectList.ChildNodes.Single().Id == Terminals.Asterisk)
				{
					var asteriskNode = selectList.ChildNodes.Single();
					var column = new OracleSelectListColumn
					{
						RootNode = asteriskNode,
						Owner = item,
						ExplicitDefinition = true,
						IsAsterisk = true
					};

					column.ColumnReferences.Add(CreateColumnReference(column, asteriskNode, null));

					allColumnReferences[column] = new HashSet<OracleTableReference>(item.TableReferences);

					item.Columns.Add(column);
				}
				else
				{
					var columnExpressions = selectList.GetDescendantsWithinSameQuery(NonTerminals.AliasedExpressionOrAllTableColumns).ToArray();
					foreach (var columnExpression in columnExpressions)
					{
						var columnAliasNode = columnExpression.GetDescendantsWithinSameQuery(Terminals.Alias).SingleOrDefault();

						var column = new OracleSelectListColumn
						             {
										 AliasNode = columnAliasNode,
										 RootNode = columnExpression,
										 Owner = item,
										 ExplicitDefinition = true
						             };

						allColumnReferences.Add(column, new List<OracleTableReference>());

						var asteriskNode = columnExpression.GetDescendantsWithinSameQuery(Terminals.Asterisk).SingleOrDefault();
						if (asteriskNode != null)
						{
							column.IsAsterisk = true;

							var prefixNonTerminal = asteriskNode.ParentNode.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.Prefix);
							var columnReference = CreateColumnReference(column, asteriskNode, prefixNonTerminal);
							column.ColumnReferences.Add(columnReference);

							var tableReferences = item.TableReferences.Where(t => t.FullyQualifiedName == columnReference.FullyQualifiedObjectName || (!columnReference.HasTableReference && t.FullyQualifiedName.NormalizedName == columnReference.FullyQualifiedObjectName.NormalizedName));
							foreach (var tableReference in tableReferences)
							{
								allColumnReferences[column].Add(tableReference);
							}
						}
						else
						{
							var identifiers = columnExpression.GetDescendantsWithinSameQuery(Terminals.Identifier).ToArray();
							foreach (var identifier in identifiers)
							{
								column.IsDirectColumnReference = columnAliasNode == null && identifier.GetAncestor(NonTerminals.Expression).ChildNodes.Count == 1;
								if (column.IsDirectColumnReference)
								{
									column.AliasNode = identifier;
								}

								var prefixNonTerminal = identifier.GetPathFilterAncestor(n => n.Id != NonTerminals.Expression, NonTerminals.PrefixedColumnReference)
									.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.Prefix);

								var columnReference = CreateColumnReference(column, identifier, prefixNonTerminal);
								column.ColumnReferences.Add(columnReference);
							}
						}

						item.Columns.Add(column);
					}
				}
			}

			foreach (var queryBlock in _queryBlockResults.Values)
			{
				foreach (var nestedQueryReference in queryBlock.TableReferences.Where(t => t.Type != TableReferenceType.PhysicalObject))
				{
					if (nestedQueryReference.Type == TableReferenceType.NestedQuery)
					{
						nestedQueryReference.QueryBlocks.Add(_queryBlockResults[nestedQueryReference.TableNode]);
					}
					else
					{
						foreach (var cteNode in nestedQueryReference.Nodes)
							nestedQueryReference.QueryBlocks.Add(_queryBlockResults[cteNode.GetDescendantsWithinSameQuery(NonTerminals.QueryBlock).Single()]);
					}
				}

				foreach (var columnReference in queryBlock.Columns.SelectMany(c => c.ColumnReferences))
				{
					foreach (var queryNode in columnReference.QueryNodes)
					{
						columnReference.QueryBlocks.Add(_queryBlockResults[queryNode]);
					}
				}
			}

			foreach (var asteriskTableReference in allColumnReferences)
			{
				foreach (var tableReference in asteriskTableReference.Value)
				{
					if (tableReference.Type == TableReferenceType.PhysicalObject)
					{
						var result = databaseModel.GetObject(tableReference.FullyQualifiedName);
						if (result.SchemaObject == null)
							continue;

						foreach (OracleColumn physicalColumn in result.SchemaObject.Columns)
						{
							var column = new OracleSelectListColumn
							             {
											 Owner = asteriskTableReference.Key.Owner,
											 ExplicitDefinition = false,
											 IsDirectColumnReference = true,
											 PhysicalColumn = physicalColumn
							             };

							asteriskTableReference.Key.Owner.Columns.Add(column);
						}
					}
					else
					{
						foreach (var exposedColumn in tableReference.Columns)
						{
							var implicitColumn = exposedColumn.AsImplicit();
							implicitColumn.Owner = asteriskTableReference.Key.Owner;
							asteriskTableReference.Key.Owner.Columns.Add(implicitColumn);
						}
					}
				}
			}

		}

		private static OracleColumnReference CreateColumnReference(OracleSelectListColumn owner, StatementDescriptionNode rootNode, StatementDescriptionNode prefixNonTerminal)
		{
			var columnReference = new OracleColumnReference
			{
				ColumnNode = rootNode,
				Owner = owner
			};

			if (prefixNonTerminal != null)
			{
				var objectIdentifier = prefixNonTerminal.GetSingleDescendant(Terminals.ObjectIdentifier);
				var schemaIdentifier = prefixNonTerminal.GetSingleDescendant(Terminals.SchemaIdentifier);

				columnReference.OwnerNode = schemaIdentifier;
				columnReference.TableNode = objectIdentifier;
			}

			return columnReference;
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

	public interface IOracleTableReference
	{
		ICollection<OracleSelectListColumn> Columns { get; }
	}

	public interface IOracleSelectListColumn
	{
		
	}

	[DebuggerDisplay("OracleQueryBlock (Alias={Alias}; Type={Type}; RootNode={RootNode})")]
	public class OracleQueryBlock
	{
		public OracleQueryBlock()
		{
			TableReferences = new List<OracleTableReference>();
			Columns = new List<OracleSelectListColumn>();
		}

		public string Alias { get; set; }

		public QueryBlockType Type { get; set; }

		public StatementDescriptionNode RootNode { get; set; }

		public ICollection<OracleTableReference> TableReferences { get; set; }

		public ICollection<OracleSelectListColumn> Columns { get; set; }
	}

	[DebuggerDisplay("OracleTableReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Table={Type != SqlPad.Oracle.TableReferenceType.NestedQuery ? TableNode.Token.Value : \"<Nested subquery>\"}; Alias={AliasNode == null ? null : AliasNode.Token.Value}; Type={Type})")]
	public class OracleTableReference : IOracleTableReference
	{
		public OracleTableReference()
		{
			Nodes = new StatementDescriptionNode[0];
			QueryBlocks = new List<OracleQueryBlock>();
			Columns = new List<OracleSelectListColumn>();
		}

		public OracleObjectIdentifier FullyQualifiedName
		{
			get { return OracleObjectIdentifier.Create(OwnerNode, Type == TableReferenceType.NestedQuery ? null : TableNode, AliasNode); }
		}

		public StatementDescriptionNode OwnerNode { get; set; }

		public StatementDescriptionNode TableNode { get; set; }
		
		public StatementDescriptionNode AliasNode { get; set; }

		public ICollection<StatementDescriptionNode> Nodes { get; set; }
		
		public ICollection<OracleQueryBlock> QueryBlocks { get; set; }

		public ICollection<OracleSelectListColumn> Columns { get; set; }

		public TableReferenceType Type { get; set; }
	}

	[DebuggerDisplay("OracleColumnReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Table={TableNode == null ? null : TableNode.Token.Value}; Column={ColumnNode.Token.Value})")]
	public class OracleColumnReference
	{
		public OracleColumnReference()
		{
			QueryNodes = new List<StatementDescriptionNode>();
			QueryBlocks = new List<OracleQueryBlock>();
		}

		public OracleObjectIdentifier FullyQualifiedObjectName
		{
			get { return OracleObjectIdentifier.Create(OwnerNode, TableNode, null); }
		}

		public string Name { get { return ColumnNode.Token.Value.ToOracleIdentifier(); } }

		public string TableName { get { return TableNode == null ? null : TableNode.Token.Value.ToOracleIdentifier(); } }

		public bool HasTableReference { get { return TableNode != null; } }

		public bool ReferencesAllColumns { get { return ColumnNode.Token.Value == "*"; } }

		public StatementDescriptionNode OwnerNode { get; set; }

		public StatementDescriptionNode TableNode { get; set; }

		public StatementDescriptionNode ColumnNode { get; set; }

		public ICollection<StatementDescriptionNode> QueryNodes { get; set; }

		public OracleSelectListColumn Owner { get; set; }

		public ICollection<OracleQueryBlock> QueryBlocks { get; set; }
	}

	[DebuggerDisplay("OracleSelectListColumn (Alias={AliasNode == null ? null : AliasNode.Token.Value}; IsDirectColumnReference={IsDirectColumnReference})")]
	public class OracleSelectListColumn
	{
		public OracleSelectListColumn()
		{
			ColumnReferences = new List<OracleColumnReference>();
		}

		public bool IsDirectColumnReference { get; set; }
		
		public bool IsAsterisk { get; set; }

		public OracleColumn PhysicalColumn { get; set; }

		public bool ExplicitDefinition { get; set; }

		public string NormalizedName
		{
			get
			{
				if (AliasNode != null)
					return AliasNode.Token.Value.ToOracleIdentifier();

				return PhysicalColumn == null ? null : PhysicalColumn.Name;
			}
		}

		public StatementDescriptionNode AliasNode { get; set; }

		public StatementDescriptionNode RootNode { get; set; }
		
		public OracleQueryBlock Owner { get; set; }

		public ICollection<OracleColumnReference> ColumnReferences { get; set; }
		
		public OracleColumn ColumnDescription { get; set; }

		public OracleSelectListColumn AsImplicit()
		{
			return new OracleSelectListColumn
			       {
				       ExplicitDefinition = false,
				       AliasNode = AliasNode,
				       RootNode = RootNode,
				       IsDirectColumnReference = true,
					   PhysicalColumn = PhysicalColumn
			       };
		}
	}

	public enum TableReferenceType
	{
		PhysicalObject,
		CommonTableExpression,
		NestedQuery
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