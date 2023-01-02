using System;

class JobPriority : IComparable
{
    public int JobTypePriority { get; set; }

    public float DistanceToPlayer { get; set; }

    public int CompareTo(object obj)
    {
        var rhs = obj as JobPriority;
        int res = JobTypePriority.CompareTo(rhs.JobTypePriority);
        if(res == 0)  DistanceToPlayer.CompareTo(rhs.DistanceToPlayer);
        return res;
    }
}