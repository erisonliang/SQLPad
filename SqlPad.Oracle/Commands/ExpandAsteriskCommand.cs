﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace SqlPad.Oracle.Commands
{
	internal class ExpandAsteriskCommand : OracleCommandBase
	{
		private CommandSettingsModel _settingsModel;
		private SourcePosition _sourcePosition;
		public const string Title = "Expand";

		private ExpandAsteriskCommand(OracleCommandExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override bool CanExecute()
		{
			return CurrentNode != null && CurrentQueryBlock != null &&
			       CurrentNode.Id == OracleGrammarDescription.Terminals.Asterisk &&
				   ExistExpandableColumns();
		}

		private bool ExistExpandableColumns()
		{
			var expandedColumns = new List<ExpandedColumn>();
			FillColumnNames(expandedColumns, false);
			return expandedColumns.Count > 0;
		}

		private void ConfigureSettings()
		{
			ExecutionContext.EnsureSettingsProviderAvailable();

			_settingsModel = ExecutionContext.SettingsProvider.Settings;

			_settingsModel.TextInputVisibility = Visibility.Collapsed;
			_settingsModel.BooleanOptionsVisibility = Visibility.Visible;

			var expandedColumns = new List<ExpandedColumn>();
			_sourcePosition = FillColumnNames(expandedColumns, true);

			var initialValue = _settingsModel.UseDefaultSettings == null || _settingsModel.UseDefaultSettings();

			foreach (var expandedColumn in expandedColumns)
			{
				_settingsModel.AddBooleanOption(
					new BooleanOption
					{
						OptionIdentifier = expandedColumn.ColumnName,
						Description = expandedColumn.ColumnName,
						Value = !expandedColumn.IsRowId && initialValue,
						Tag = expandedColumn
					});
			}

			_settingsModel.Title = "Expand Asterisk";
			_settingsModel.Heading = _settingsModel.Title;
		}

		protected override void Execute()
		{
			ConfigureSettings();

			if (!ExecutionContext.SettingsProvider.GetSettings())
				return;

			var segmentToReplace = GetSegmentToReplace();
			if (!segmentToReplace.Equals(TextSegment.Empty))
			{
				ExecutionContext.SegmentsToReplace.Add(segmentToReplace);
			}
		}

		private TextSegment GetSegmentToReplace()
		{
			var columnNames = _settingsModel.BooleanOptions.Values
				.Where(v => v.Value)
				.Select(v => v.OptionIdentifier)
				.ToArray();

			if (columnNames.Length == 0)
				return TextSegment.Empty;

			var textSegment =
				new TextSegment
				{
					IndextStart = _sourcePosition.IndexStart,
					Length = _sourcePosition.Length,
					Text = String.Join(", ", columnNames)
				};

			return textSegment;
		}

		private SourcePosition FillColumnNames(List<ExpandedColumn> columnNames, bool includeRowId)
		{
			var segmentToReplace = SourcePosition.Empty;
			var asteriskReference = CurrentQueryBlock.Columns.FirstOrDefault(c => c.RootNode == CurrentNode);
			if (asteriskReference == null)
			{
				var columnReference = CurrentQueryBlock.Columns.SelectMany(c => c.ColumnReferences).FirstOrDefault(c => c.ColumnNode == CurrentNode);
				if (columnReference == null || columnReference.ObjectNodeObjectReferences.Count != 1)
					return segmentToReplace;
				
				segmentToReplace = columnReference.SelectListColumn.RootNode.SourcePosition;
				var objectReference = columnReference.ObjectNodeObjectReferences.First();

				columnNames.AddRange(objectReference.Columns
					.Where(c => !String.IsNullOrEmpty(c.Name))
					.Select(c => GetExpandedColumn(objectReference, c.Name, false)));

				if (includeRowId && objectReference.SearchResult.SchemaObject != null &&
				    objectReference.SearchResult.SchemaObject.Organization.In(OrganizationType.Heap, OrganizationType.Index))
				{
					columnNames.Insert(0, GetExpandedColumn(objectReference, OracleColumn.RowId, true));
				}
			}
			else
			{
				columnNames.AddRange(CurrentQueryBlock.Columns
					.Where(c => !c.IsAsterisk && !String.IsNullOrEmpty(c.NormalizedName))
					.Select(c => GetExpandedColumn(GetObjectReference(c), c.NormalizedName, false)));

				if (!includeRowId)
					return segmentToReplace;
				
				var rowIdColumns = CurrentQueryBlock.ObjectReferences
					.Where(o => o.SearchResult.SchemaObject != null && o.SearchResult.SchemaObject.Organization.In(OrganizationType.Heap, OrganizationType.Index))
					.Select(o => GetExpandedColumn(o, OracleColumn.RowId, true));

				columnNames.InsertRange(0, rowIdColumns);

				segmentToReplace = asteriskReference.RootNode.SourcePosition;
			}

			return segmentToReplace;
		}

		private static OracleObjectReference GetObjectReference(OracleSelectListColumn column)
		{
			var columnReference = column.ColumnReferences.FirstOrDefault();
			return columnReference != null && columnReference.ColumnNodeObjectReferences.Count == 1
				? columnReference.ColumnNodeObjectReferences.First()
				: null;
		}

		private static ExpandedColumn GetExpandedColumn(OracleObjectReference objectReference, string columnName, bool isRowId)
		{
			return new ExpandedColumn { ColumnName = GetColumnName(objectReference, columnName), IsRowId = isRowId };
		}

		private static string GetColumnName(OracleObjectReference objectReference, string columnName)
		{
			var simpleColumnName = columnName.ToSimpleIdentifier();
			if (objectReference == null)
				return simpleColumnName;

			var objectPrefix = objectReference.FullyQualifiedName.ToString();
			var usedObjectPrefix = String.IsNullOrEmpty(objectPrefix)
				? null
				: String.Format("{0}.", objectPrefix);

			return String.Format("{0}{1}", usedObjectPrefix, simpleColumnName);
		}

		private struct ExpandedColumn
		{
			public string ColumnName { get; set; }
			public bool IsRowId { get; set; }
		}
	}
}