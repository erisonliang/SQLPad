﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace SqlPad
{
	public class ObservableDictionary<TKey, TValue> : IDictionary<TKey, TValue>, INotifyCollectionChanged, INotifyPropertyChanged
	{
		private const string IndexerName = "Item[]";

		protected IDictionary<TKey, TValue> Dictionary { get; private set; }

		public ObservableDictionary()
		{
			Dictionary = new Dictionary<TKey, TValue>();
		}

		public ObservableDictionary(IDictionary<TKey, TValue> dictionary)
		{
			Dictionary = new Dictionary<TKey, TValue>(dictionary);
		}

		public ObservableDictionary(IEqualityComparer<TKey> comparer)
		{
			Dictionary = new Dictionary<TKey, TValue>(comparer);
		}

		public ObservableDictionary(int capacity)
		{
			Dictionary = new Dictionary<TKey, TValue>(capacity);
		}

		public ObservableDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer)
		{
			Dictionary = new Dictionary<TKey, TValue>(dictionary, comparer);
		}

		public ObservableDictionary(int capacity, IEqualityComparer<TKey> comparer)
		{
			Dictionary = new Dictionary<TKey, TValue>(capacity, comparer);
		}

		public void Add(TKey key, TValue value)
		{
			Insert(key, value, true);
		}

		public bool ContainsKey(TKey key)
		{
			return Dictionary.ContainsKey(key);
		}

		public ICollection<TKey> Keys => Dictionary.Keys;

		public bool Remove(TKey key)
		{
			if (key == null) throw new ArgumentNullException(nameof(key));

			TValue value;
			Dictionary.TryGetValue(key, out value);
			var removed = Dictionary.Remove(key);
			if (removed)
			{
				OnCollectionChanged();
			}

			return removed;
		}

		public bool TryGetValue(TKey key, out TValue value)
		{
			return Dictionary.TryGetValue(key, out value);
		}

		public ICollection<TValue> Values => Dictionary.Values;

		public TValue this[TKey key]
		{
			get
			{
				TValue value;
				return TryGetValue(key, out value) ? value : default(TValue);
			}

			set { Insert(key, value, false); }
		}

		public void Add(KeyValuePair<TKey, TValue> item)
		{
			Insert(item.Key, item.Value, true);
		}

		public void Clear()
		{
			if (Dictionary.Count > 0)
			{
				Dictionary.Clear();
				OnCollectionChanged();
			}
		}

		public bool Contains(KeyValuePair<TKey, TValue> item)
		{
			return Dictionary.Contains(item);
		}

		public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
		{
			Dictionary.CopyTo(array, arrayIndex);
		}

		public int Count => Dictionary.Count;

		public bool IsReadOnly => Dictionary.IsReadOnly;

		public bool Remove(KeyValuePair<TKey, TValue> item)
		{
			return Remove(item.Key);
		}

		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
		{
			return Dictionary.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable)Dictionary).GetEnumerator();
		}

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		public event PropertyChangedEventHandler PropertyChanged;

		public void AddRange(IDictionary<TKey, TValue> items)
		{
			if (items == null) throw new ArgumentNullException(nameof(items));

			if (items.Count == 0)
			{
				return;
			}

			if (Dictionary.Count > 0)
			{
				if (items.Keys.Any(k => Dictionary.ContainsKey(k)))
				{
					throw new ArgumentException("An item with the same key has already been added.");
				}

				foreach (var item in items)
				{
					Dictionary.Add(item);
				}
			}
			else
			{
				Dictionary = new Dictionary<TKey, TValue>(items);
			}

			OnCollectionChanged(NotifyCollectionChangedAction.Add, items.ToArray());
		}

		private void Insert(TKey key, TValue value, bool add)
		{
			if (key == null)
			{
				throw new ArgumentNullException(nameof(key));
			}

			TValue item;
			if (Dictionary.TryGetValue(key, out item))
			{
				if (add)
				{
					throw new ArgumentException("An item with the same key has already been added.");
				}

				if (Equals(item, value))
				{
					return;
				}

				Dictionary[key] = value;

				OnCollectionChanged(NotifyCollectionChangedAction.Replace, new KeyValuePair<TKey, TValue>(key, value), new KeyValuePair<TKey, TValue>(key, item));
				OnPropertyChanged(key.ToString());
			}
			else
			{
				Dictionary[key] = value;

				OnCollectionChanged(NotifyCollectionChangedAction.Add, new KeyValuePair<TKey, TValue>(key, value));
				OnPropertyChanged(key.ToString());
			}
		}

		private void OnPropertyChanged()
		{
			OnPropertyChanged(nameof(Count));
			OnPropertyChanged(IndexerName);
			OnPropertyChanged(nameof(Keys));
			OnPropertyChanged(nameof(Values));
		}

		protected virtual void OnPropertyChanged(string propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private void OnCollectionChanged()
		{
			OnPropertyChanged();
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}

		private void OnCollectionChanged(NotifyCollectionChangedAction action, KeyValuePair<TKey, TValue> changedItem)
		{
			OnPropertyChanged();
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(action, changedItem, 0));
		}

		private void OnCollectionChanged(NotifyCollectionChangedAction action, KeyValuePair<TKey, TValue> newItem, KeyValuePair<TKey, TValue> oldItem)
		{
			OnPropertyChanged();
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(action, newItem, oldItem, 0));
		}

		private void OnCollectionChanged(NotifyCollectionChangedAction action, IList newItems)
		{
			OnPropertyChanged();
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(action, newItems, 0));
		}
	}
}