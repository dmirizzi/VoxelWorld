using System;
using System.Collections.Generic;

// Priority queue where items with smaller priority are dequeued first
public class PriorityQueue<TValue, TPriority> where TPriority : IComparable
{
    public int Count => _containedValues.Count;

    public void Enqueue(TValue value, TPriority priority)
    {
        if(_list.Count == 0)
        {
            _list.AddFirst((value, priority));
        }
        else
        {           
            for(var node = _list.First; node != null; node = node.Next)
            {
                if(priority.CompareTo(node.Value.Priority) < 0)
                {
                    _list.AddBefore(node, (value, priority));
                    _containedValues.Add(value);
                    return;
                }
            }
            _list.AddLast((value, priority));
        }

        _containedValues.Add(value);
    }

    public bool DequeueNext(Func<TValue, bool> predicate, out TValue value)
    {
        if(_list.Count == 0)
        {
            value = default(TValue);
            return false;
        }

        for(var node = _list.First; node != null; node = node.Next)
        {            
            if(predicate(node.Value.Value))
            {
                value = node.Value.Value;
                _list.Remove(node);
                _containedValues.Remove(value);
                return true;
            }
        }

        value = default(TValue);
        return false;
    }

    public void EnqueueUnique(TValue value, TPriority priority)
    {
        if(!Contains(value))
        {
            Enqueue(value, priority);
        }
    }

    public bool Contains(TValue value) => _containedValues.Contains(value);

    public bool TryDequeue(out TValue value)
    {
        if(_list.Count > 0)
        {
            value = _list.First.Value.Value;
            _list.RemoveFirst();
            _containedValues.Remove(value);
            return true;
        }
        value = default(TValue);
        return false;
    }
    
    public void DropFirst()
    {
        _containedValues.Remove(_list.First.Value.Value);
        _list.RemoveFirst();
    }

    public bool Peek(out TValue value)
    {
        if(_list.Count > 0)
        {
            value = _list.First.Value.Value;
            return true;
        }
        value = default(TValue);
        return false;
    }

    public IReadOnlyCollection<(TValue Value, TPriority Priority)> GetList() => _list;

    public void Clear()
    {
        _list.Clear();
        _containedValues.Clear();
    }

    private LinkedList<(TValue Value, TPriority Priority)> _list = new LinkedList<(TValue, TPriority)>();

    private HashSet<TValue> _containedValues = new HashSet<TValue>();
}