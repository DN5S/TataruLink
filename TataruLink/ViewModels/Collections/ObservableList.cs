using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace TataruLink.ViewModels.Collections;

/// <summary>
/// Lightweight observable list optimized for ImGui.
/// Uses a simple dirty flag instead of complex change events.
/// </summary>
public class ObservableList<T> : IList<T>, IReadOnlyList<T>
{
    private readonly List<T> items = [];
    private readonly Lock syncRoot = new();
    private bool isDirty;

    public ObservableList()
    {
    }

    public ObservableList(IEnumerable<T> collection)
    {
        items.AddRange(collection);
        isDirty = true;
    }

    /// <summary>
    /// Gets whether the list has been modified since last reset.
    /// </summary>
    public bool IsDirty
    {
        get
        {
            lock (syncRoot)
                return isDirty;
        }
    }

    /// <summary>
    /// Resets the dirty flag. Call after processing changes.
    /// </summary>
    public void ResetDirtyFlag()
    {
        lock (syncRoot)
            isDirty = false;
    }

    /// <summary>
    /// Gets a snapshot of the current items. Thread-safe.
    /// </summary>
    public List<T> GetSnapshot()
    {
        lock (syncRoot)
            return [..items];
    }

    public void Add(T item)
    {
        lock (syncRoot)
        {
            items.Add(item);
            isDirty = true;
        }
    }

    public void AddRange(IEnumerable<T> collection)
    {
        lock (syncRoot)
        {
            items.AddRange(collection);
            isDirty = true;
        }
    }

    public bool Remove(T item)
    {
        lock (syncRoot)
        {
            var result = items.Remove(item);
            if (result)
                isDirty = true;
            return result;
        }
    }

    public void RemoveAt(int index)
    {
        lock (syncRoot)
        {
            items.RemoveAt(index);
            isDirty = true;
        }
    }

    public void Clear()
    {
        lock (syncRoot)
        {
            items.Clear();
            isDirty = true;
        }
    }

    public bool Contains(T item)
    {
        lock (syncRoot)
            return items.Contains(item);
    }

    public int IndexOf(T item)
    {
        lock (syncRoot)
            return items.IndexOf(item);
    }

    public void Insert(int index, T item)
    {
        lock (syncRoot)
        {
            items.Insert(index, item);
            isDirty = true;
        }
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        lock (syncRoot)
            items.CopyTo(array, arrayIndex);
    }

    public int Count
    {
        get
        {
            lock (syncRoot)
                return items.Count;
        }
    }

    public bool IsReadOnly => false;

    public T this[int index]
    {
        get
        {
            lock (syncRoot)
                return items[index];
        }
        set
        {
            lock (syncRoot)
            {
                items[index] = value;
                isDirty = true;
            }
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        // Return enumerator on snapshot to avoid lock during iteration
        return GetSnapshot().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Replaces all items with new collection. Efficient bulk update.
    /// </summary>
    public void ReplaceAll(IEnumerable<T> newItems)
    {
        lock (syncRoot)
        {
            items.Clear();
            items.AddRange(newItems);
            isDirty = true;
        }
    }

    /// <summary>
    /// Filters items and replaces collection. Efficient for search operations.
    /// </summary>
    public void Filter(Func<T, bool> predicate)
    {
        lock (syncRoot)
        {
            var filtered = items.Where(predicate).ToList();
            items.Clear();
            items.AddRange(filtered);
            isDirty = true;
        }
    }
}
