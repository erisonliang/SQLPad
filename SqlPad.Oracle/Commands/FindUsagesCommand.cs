﻿using System.Collections.Generic;
using System.Linq;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	public class FindUsagesCommand : DisplayCommandBase
	{
		private readonly StatementDescriptionNode _currentNode;
		private readonly OracleStatementSemanticModel _semanticModel;
		private readonly OracleQueryBlock _queryBlock;
		//private readonly int _currentPosition;

		public FindUsagesCommand(string statementText, int currentPosition, IDatabaseModel databaseModel)
		{
			_currentNode = new OracleSqlParser().Parse(statementText).GetTerminalAtPosition(currentPosition, t => t.Id.IsIdentifierOrAlias());
			if (_currentNode == null)
				return;

			_semanticModel = new OracleStatementSemanticModel(statementText, (OracleStatement)_currentNode.Statement, (OracleDatabaseModel)databaseModel);
			_queryBlock = _semanticModel.GetQueryBlock(_currentNode);
			//_currentPosition = currentPosition;
		}

		public override bool CanExecute(object parameter)
		{
			return _currentNode != null;
		}

		protected override void ExecuteInternal(ICollection<TextSegment> segments)
		{
			var nodes = Enumerable.Empty<StatementDescriptionNode>();

			switch (_currentNode.Id)
			{
				case Terminals.ObjectAlias:
				case Terminals.ObjectIdentifier:
					nodes = GetTableReferenceUsage();
					break;
				case Terminals.SchemaIdentifier:
					nodes = GetSchemaReferenceUsage();
					break;
				case Terminals.Identifier:
				case Terminals.ColumnAlias:
					nodes = GetColumnReferenceUsage();
					break;
			}

			//MessageBox.Show(_currentPosition + Environment.NewLine + String.Join(Environment.NewLine, nodes.OrderBy(n => n.SourcePosition.IndexStart).Select(n => n.SourcePosition.IndexStart + " - " + n.SourcePosition.Length)));

			foreach (var node in nodes)
			{
				segments.Add(new TextSegment
				                      {
					                      IndextStart = node.SourcePosition.IndexStart,
										  Length = node.SourcePosition.Length
				                      });
			}
		}

		private IEnumerable<StatementDescriptionNode> GetTableReferenceUsage()
		{
			var columnReferencedObject = _queryBlock.AllColumnReferences
				.SingleOrDefault(c => c.ObjectNode == _currentNode && c.ObjectNodeObjectReferences.Count == 1);

			var referencedObject = _queryBlock.ObjectReferences.SingleOrDefault(t => t.ObjectNode == _currentNode || t.AliasNode == _currentNode);
			var objectReference = columnReferencedObject != null
				? columnReferencedObject.ObjectNodeObjectReferences.Single()
				: referencedObject;

			var objectReferenceNodes = Enumerable.Repeat(objectReference.ObjectNode, 1);
			if (objectReference.AliasNode != null)
			{
				objectReferenceNodes = objectReferenceNodes.Concat(Enumerable.Repeat(objectReference.AliasNode, 1));
			}

			return _queryBlock.AllColumnReferences.Where(c => c.ObjectNode != null && c.ObjectNodeObjectReferences.Count == 1 && c.ObjectNodeObjectReferences.Single() == objectReference)
				.Select(c => c.ObjectNode)
				.Concat(objectReferenceNodes);
		}

		private IEnumerable<StatementDescriptionNode> GetColumnReferenceUsage()
		{
			IEnumerable<StatementDescriptionNode> nodes;
			var columnReference = _queryBlock.AllColumnReferences
				.FirstOrDefault(c => (c.ColumnNode == _currentNode || (c.SelectListColumn != null && c.SelectListColumn.AliasNode == _currentNode)) && c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeColumnReferences == 1);

			OracleSelectListColumn selectListColumn;
			if (columnReference != null)
			{
				var objectReference = columnReference.ColumnNodeObjectReferences.Single();
				var columnReferences = _queryBlock.AllColumnReferences.Where(c => c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeObjectReferences.Single() == objectReference && c.NormalizedName == columnReference.NormalizedName).ToArray();
				nodes = columnReferences.Select(c => c.ColumnNode);

				bool searchChildren;
				if (_currentNode.Id == Terminals.Identifier)
				{
					searchChildren = true;

					selectListColumn = columnReference.SelectListColumn;
					if (selectListColumn == null)
					{
						var selectionListColumnReference = columnReferences.FirstOrDefault(c => c.SelectListColumn != null && c.SelectListColumn.IsDirectColumnReference);
						if (selectionListColumnReference != null)
						{
							selectListColumn = selectionListColumnReference.SelectListColumn;
						}
					}
					else if (!selectListColumn.IsDirectColumnReference)
					{
						selectListColumn = null;
					}

					if (selectListColumn != null && selectListColumn.AliasNode != _currentNode)
					{
						nodes = nodes.Concat(new[] { selectListColumn.AliasNode });
					}
				}
				else
				{
					selectListColumn = _queryBlock.Columns.Single(c => c.AliasNode == _currentNode);
					var nodeList = new List<StatementDescriptionNode> { selectListColumn.AliasNode };
					searchChildren = selectListColumn.IsDirectColumnReference;

					if (selectListColumn.IsDirectColumnReference && selectListColumn.RootNode.TerminalCount > 1)
						nodeList.Add(selectListColumn.ColumnReferences.Single().ColumnNode);

					nodes = searchChildren ? nodes.Concat(nodeList) : nodeList;
				}

				if (searchChildren)
					nodes = nodes.Concat(GetChildQueryBlockColumnReferences(objectReference, columnReference));
			}
			else
			{
				nodes = new[] { _currentNode };
				selectListColumn = _queryBlock.Columns.SingleOrDefault(c => c.AliasNode == _currentNode);
			}

			nodes = nodes.Concat(GetParentQueryBlockReferences(selectListColumn));

			return nodes;
		}

		private IEnumerable<StatementDescriptionNode> GetChildQueryBlockColumnReferences(OracleObjectReference objectReference, OracleColumnReference columnReference)
		{
			var nodes = Enumerable.Empty<StatementDescriptionNode>();
			if (objectReference.QueryBlocks.Count != 1)
				return nodes;

			var childQueryBlock = objectReference.QueryBlocks.Single();
			var childColumn = childQueryBlock.Columns
				.SingleOrDefault(c => c.NormalizedName == columnReference.NormalizedName && c.ColumnReferences.All(cr => cr.ColumnNodeColumnReferences == 1));
			
			if (childColumn == null)
				return nodes;

			nodes = new List<StatementDescriptionNode>{ childColumn.AliasNode };
			
			if (childColumn.IsDirectColumnReference)
			{
				var childSelectColumnReferences = childQueryBlock.Columns.SelectMany(c => c.ColumnReferences)
					.Where(c => c.ColumnNodeObjectReferences.Count == 1 && c.SelectListColumn.NormalizedName == columnReference.NormalizedName && c.ColumnNode != childColumn.AliasNode)
					.Select(c => c.ColumnNode);

				nodes = nodes.Concat(childSelectColumnReferences);

				var childColumnReference = childColumn.ColumnReferences.Single();
				nodes = nodes.Concat(childQueryBlock.ColumnReferences.Where(c => c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeObjectReferences.Single() == childColumnReference.ColumnNodeObjectReferences.Single() && c.NormalizedName == childColumnReference.NormalizedName).Select(c => c.ColumnNode));

				if (childColumnReference.ColumnNodeObjectReferences.Count == 1)
				{
					nodes = nodes.Concat(GetChildQueryBlockColumnReferences(childColumnReference.ColumnNodeObjectReferences.Single(), childColumnReference));
				}
			}

			return nodes;
		}

		private IEnumerable<StatementDescriptionNode> GetParentQueryBlockReferences(OracleSelectListColumn selectListColumn)
		{
			var nodes = Enumerable.Empty<StatementDescriptionNode>();
			if (selectListColumn == null || selectListColumn.AliasNode == null)
				return nodes;

			var parentQueryBlocks = _semanticModel.QueryBlocks.Where(qb => qb.ObjectReferences.SelectMany(o => o.QueryBlocks).Contains(selectListColumn.Owner));
			foreach (var parentQueryBlock in parentQueryBlocks)
			{
				var parentReferences = parentQueryBlock.AllColumnReferences
					.Where(c => c.ColumnNodeColumnReferences == 1 && c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeObjectReferences.Single().QueryBlocks.Count == 1
								&& c.ColumnNodeObjectReferences.Single().QueryBlocks.Single() == selectListColumn.Owner && c.NormalizedName == selectListColumn.NormalizedName)
					.ToArray();

				if (parentReferences.Length == 0)
					continue;

				nodes = parentReferences.Select(c => c.ColumnNode);

				var parentColumnReferences = parentReferences.Where(c => c.SelectListColumn != null && c.SelectListColumn.IsDirectColumnReference).ToArray();

				if (parentColumnReferences.Length == 1)
				{
					nodes = nodes
						.Concat(parentColumnReferences.Where(c => c.ColumnNode != c.SelectListColumn.AliasNode).Select(c => c.SelectListColumn.AliasNode))
						.Concat(GetParentQueryBlockReferences(parentColumnReferences[0].SelectListColumn));
				}
			}

			return nodes;
		}

		private IEnumerable<StatementDescriptionNode> GetSchemaReferenceUsage()
		{
			return _currentNode.Statement.AllTerminals.Where(t => t.Id == Terminals.SchemaIdentifier && t.Token.Value.ToQuotedIdentifier() == _currentNode.Token.Value.ToQuotedIdentifier());
		}
	}
}
