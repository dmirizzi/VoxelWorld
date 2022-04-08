using System.Linq;
using UnityEngine;

public static class GameObjectExtensions
{
    public static T GetComponentByInterface<T>(this GameObject obj)
    {
        return (T)obj.GetComponents<object>().FirstOrDefault(c => c is T);
    }
}
