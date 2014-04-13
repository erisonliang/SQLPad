﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	public class AddAliasCommand : OracleCommandBase
	{
		public AddAliasCommand(OracleStatementSemanticModel semanticModel, StatementDescriptionNode currentTerminal)
			: base(semanticModel, currentTerminal)
		{

		}

		public override bool CanExecute(object parameter)
		{
			if (CurrentTerminal.Id != Terminals.ObjectIdentifier)
				return false;
			
			var tables = SemanticModel.QueryBlocks.SelectMany(b => b.TableReferences).Where(t => t.TableNode == CurrentTerminal).ToArray();
			return tables.Length == 1 && tables[0].AliasNode == null;
		}

		protected override void ExecuteInternal(ICollection<TextSegment> segmentsToReplace)
		{
			MessageBox.Show("not implemented yet");
		}

		public override event EventHandler CanExecuteChanged = delegate { };
	}
}
