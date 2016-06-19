﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml.Linq;

namespace SqlPad.Oracle.ExecutionPlan
{
	[DebuggerDisplay("ExecutionPlanItem (Id={Id}; Operation={Operation}; Depth={Depth}; IsLeaf={IsLeaf}; ExecutionOrder={ExecutionOrder})")]
	public class ExecutionPlanItem : ModelBase
	{
		private readonly List<ExecutionPlanItem> _childItems = new List<ExecutionPlanItem>();

		public int ExecutionOrder { get; set; }
		
		public decimal? CostRatio { get; set; }

		public int Id { get; set; }

		public int? ParentId { get; set; }

		public int Depth { get; set; }

		public bool IsLeaf => _childItems.Count == 0;

		public string Operation { get; set; }

		public string Options { get; set; }

		public string Optimizer { get; set; }

		public string ObjectOwner { get; set; }

		public string ObjectName { get; set; }

		public string ObjectAlias { get; set; }

		public string ObjectType { get; set; }

		public long? Cost { get; set; }
		
		public long? Cardinality { get; set; }

		public long? Bytes { get; set; }

		public string PartitionStart { get; set; }

		public string PartitionStop { get; set; }

		public string Distribution { get; set; }

		public long? CpuCost { get; set; }

		public long? IoCost { get; set; }

		public long? TempSpace { get; set; }

		public string AccessPredicates { get; set; }

		public string FilterPredicates { get; set; }

		public TimeSpan? Time { get; set; }

		public string QueryBlockName { get; set; }

		public XElement Other { get; set; }

		public ExecutionPlanItem Parent { get; private set; }

		public bool IsInactive { get; set; }

		public IReadOnlyList<ExecutionPlanItem> ChildItems => _childItems.AsReadOnly();

		public void AddChildItem(ExecutionPlanItem childItem)
		{
			_childItems.Add(childItem);
			childItem.Parent = this;
		}

		public bool IsChildFrom(ExecutionPlanItem parent)
		{
			var item = this;
			do
			{
				item = item.Parent;
			} while (item != null && item != parent);

			return item != null;
		}
	}
}
