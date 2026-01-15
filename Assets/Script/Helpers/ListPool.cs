using System.Collections.Generic;

/// <summary>
/// Generic list pool to reduce GC allocations
/// </summary>
public static class ListPool<T>
{
    private static readonly Stack<List<T>> _pool = new Stack<List<T>>();
    private const int MAX_POOL_SIZE = 20;

    public static List<T> Get()
    {
        if (_pool.Count > 0)
        {
            var list = _pool.Pop();
            list.Clear();
            return list;
        }
        return new List<T>();
    }

    public static void Release(List<T> list)
    {
        if (list == null) return;
        list.Clear();
        if (_pool.Count < MAX_POOL_SIZE)
            _pool.Push(list);
    }
}
