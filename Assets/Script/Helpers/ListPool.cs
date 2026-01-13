using System.Collections.Generic;

/// <summary>
/// Generic list pool to reduce GC allocations
/// </summary>
public static class ListPool<T>
{
    private static readonly Stack<List<T>> _pool = new Stack<List<T>>(8);
    private const int MAX_POOL_SIZE = 10;

    public static List<T> Get()
    {
        if (_pool.Count > 0)
        {
            List<T> list = _pool.Pop();
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

/// <summary>
/// Cached collections for common game operations
/// </summary>
public static class GameCollections
{
    // Reusable lists for game operations
    private static readonly List<int> _tempNumberList = new List<int>(20);
    private static readonly List<int> _tempHitsList = new List<int>(15);
    private static readonly HashSet<int> _tempNumberSet = new HashSet<int>();

    public static List<int> GetTempNumberList()
    {
        _tempNumberList.Clear();
        return _tempNumberList;
    }

    public static List<int> GetTempHitsList()
    {
        _tempHitsList.Clear();
        return _tempHitsList;
    }

    public static HashSet<int> GetTempNumberSet()
    {
        _tempNumberSet.Clear();
        return _tempNumberSet;
    }
}