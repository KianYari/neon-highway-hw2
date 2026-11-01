using System.Collections.Generic;
using UnityEngine;

// Marks an instance as belonging to a pool so it knows where to go back to.
public class PooledObject : MonoBehaviour
{
    [System.NonSerialized] public GameObject prefab;
    [System.NonSerialized] public bool inPool;
}

// One pool per prefab. Nothing in the game uses Instantiate/Destroy while playing,
// everything is taken from here and returned here.
[DefaultExecutionOrder(-200)]
public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance { get; private set; }

    readonly Dictionary<GameObject, Stack<GameObject>> free = new Dictionary<GameObject, Stack<GameObject>>();

    void Awake()
    {
        Instance = this;
    }

    public void Prewarm(GameObject prefab, int count)
    {
        Stack<GameObject> stack = GetStack(prefab);
        for (int i = 0; i < count; i++)
        {
            GameObject obj = Create(prefab);
            obj.SetActive(false);
            obj.GetComponent<PooledObject>().inPool = true;
            stack.Push(obj);
        }
    }

    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        Stack<GameObject> stack = GetStack(prefab);
        GameObject obj = null;
        while (stack.Count > 0 && obj == null) obj = stack.Pop();
        if (obj == null) obj = Create(prefab);

        obj.GetComponent<PooledObject>().inPool = false;
        obj.transform.SetParent(null);
        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);
        return obj;
    }

    public T Get<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
    {
        return Get(prefab.gameObject, position, rotation).GetComponent<T>();
    }

    public void Release(GameObject obj)
    {
        PooledObject pooled = obj.GetComponent<PooledObject>();
        if (pooled == null || pooled.inPool) return;

        pooled.inPool = true;
        obj.SetActive(false);
        obj.transform.SetParent(transform, false);
        GetStack(pooled.prefab).Push(obj);
    }

    Stack<GameObject> GetStack(GameObject prefab)
    {
        if (!free.TryGetValue(prefab, out Stack<GameObject> stack))
        {
            stack = new Stack<GameObject>();
            free[prefab] = stack;
        }
        return stack;
    }

    GameObject Create(GameObject prefab)
    {
        GameObject obj = Instantiate(prefab, transform);
        obj.name = prefab.name;
        PooledObject pooled = obj.GetComponent<PooledObject>();
        if (pooled == null) pooled = obj.AddComponent<PooledObject>();
        pooled.prefab = prefab;
        return obj;
    }
}
