﻿using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Documents;

namespace SqlPad
{
	/// <summary>
	/// Interaction logic for FunctionOverloadList.xaml
	/// </summary>
	public partial class FunctionOverloadList
	{

		public FunctionOverloadList()
		{
			InitializeComponent();
		}

		public ICollection<FunctionOverloadDescription> FunctionOverloads
		{
			set
			{
				foreach (var overloadDescription in value)
				{
					var textBlock = new TextBlock();

					if (!overloadDescription.IsParameterMetadataAvailable)
					{
						textBlock.Inlines.Add("Parameter metadata is not availale. ");
						ViewOverloads.Items.Add(textBlock);
						continue;
					}

					textBlock.Inlines.Add(overloadDescription.Name);

					textBlock.Inlines.Add("(");

					var i = 0;
					foreach (var parameter in overloadDescription.Parameters)
					{
						if (i > 0)
						{
							textBlock.Inlines.Add(", ");
						}

						Inline inline = new Run(parameter);
						if (i == overloadDescription.CurrentParameterIndex)
						{
							inline = new Bold(inline);
						}

						textBlock.Inlines.Add(inline);
						i++;
					}

					textBlock.Inlines.Add(")");

					if (!String.IsNullOrEmpty(overloadDescription.ReturnedDatatype))
					{
						textBlock.Inlines.Add(" RETURN: ");
						textBlock.Inlines.Add(overloadDescription.ReturnedDatatype);
					}

					ViewOverloads.Items.Add(textBlock);
				}
			}
		}
	}
}
