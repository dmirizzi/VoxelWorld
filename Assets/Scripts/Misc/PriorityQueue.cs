using System;
using System.Collections.Generic;

public class PriorityQueue<TValue, TPriority> where TPriority : IComparable
{
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
                    return;
                }
            }
            _list.AddLast((value, priority));
        }
    }

    public bool TryDequeue(out TValue value)
    {
        if(_list.Count > 0)
        {
            value = _list.First.Value.Value;
            _list.RemoveFirst();
            return true;
        }
        value = default(TValue);
        return false;
    }

    private LinkedList<(TValue Value, TPriority Priority)> _list = new LinkedList<(TValue, TPriority)>();
}