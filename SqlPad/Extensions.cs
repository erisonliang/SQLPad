﻿using System;
using System.Collections.Generic;
using System.Linq;
using ICSharpCode.AvalonEdit;

namespace SqlPad
{
	public static class Extensions
	{
		public static bool In<T>(this T o, params T[] elements)
		{
			return elements.Any(e => e.Equals(o));
		}

		public static IEnumerable<ICodeCompletionItem> OrderItems(this IEnumerable<ICodeCompletionItem> codeCompletionItems)
		{
			return codeCompletionItems.OrderBy(i => i.CategoryPriority).ThenBy(i => i.Priority).ThenBy(i => i.Name);
		}

		public static IEnumerable<T> TakeWhileInclusive<T>(this IEnumerable<T> source, Func<T, bool> predicate)
		{
			foreach (var item in source)
			{
				if (predicate(item))
				{
					yield return item;
					continue;
				}

				yield return item;
				break;
			}
		}

		public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> items)
		{
			foreach (var item in items)
				collection.Add(item);
		}

		public static bool TryRemoveBlockComment(this TextEditor editor)
		{
			const int commentHeadingLength = 2;
			var commentRemoved = false;

			if (editor.SelectionLength == 0)
			{
				if (editor.Text.Length - editor.CaretOffset >= commentHeadingLength && editor.CaretOffset >= commentHeadingLength &&
				    editor.Text.Substring(editor.CaretOffset - commentHeadingLength, 4) == "/**/")
				{
					commentRemoved = true;
					editor.Document.Remove(editor.CaretOffset - commentHeadingLength, 4);
				}

				if (editor.Text.Length - editor.CaretOffset >= 2 * commentHeadingLength &&
				    editor.Text.Substring(editor.CaretOffset, 4) == "/**/")
				{
					commentRemoved = true;
					editor.Document.Remove(editor.CaretOffset, 4);
				}
			}
			else if (editor.Text.Length - editor.SelectionStart >= commentHeadingLength && editor.SelectionStart + editor.SelectionLength > commentHeadingLength &&
			         editor.Text.Substring(editor.SelectionStart, commentHeadingLength) == "/*" &&
			         editor.Text.Substring(editor.SelectionStart + editor.SelectionLength - commentHeadingLength, commentHeadingLength) == "*/")
			{
				commentRemoved = true;
				editor.Document.Remove(editor.SelectionStart + editor.SelectionLength - commentHeadingLength, commentHeadingLength);
				editor.Document.Remove(editor.SelectionStart, commentHeadingLength);
			}

			return commentRemoved;
		}

		public static void ReplaceTextSegments(this TextEditor editor, ICollection<TextSegment> textSegments)
		{
			editor.Document.BeginUpdate();

			foreach (var textSegment in textSegments.OrderByDescending(s => s.IndextStart).ThenByDescending(s => s.Length))
			{
				editor.Document.Replace(textSegment.IndextStart, textSegment.Length, textSegment.Text);
			}

			editor.Document.EndUpdate();
		}

		public static void ScrollToCaret(this TextEditor editor)
		{
			editor.ScrollToLine(editor.Document.GetLineByOffset(editor.CaretOffset).LineNumber);
		}

		public static string ToToolTipText(this SemanticError error)
		{
			switch (error)
			{
				case SemanticError.None:
					return null;
				case SemanticError.AmbiguousReference:
					return "Ambiguous reference";
				case SemanticError.InvalidParameterCount:
					return "Invalid parameter count";
				case SemanticError.MissingParenthesis:
					return "Missing parenthesis";
			}

			throw new NotSupportedException(String.Format("Value '{0}' is not supported. ", error));
		}
	}
}