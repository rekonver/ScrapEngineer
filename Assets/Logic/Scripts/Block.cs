using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class BlockConnections
{
    public List<Bearing> Bearings = new List<Bearing>();
    public List<Damper> Dampers = new List<Damper>();
    public HashSet<GameObject> ConnectedObjects = new HashSet<GameObject>();
}

public class Block : MonoBehaviour
{
    [Header("Connections")]
    public BlockConnections Connections = new BlockConnections();

    [Header("Connections")]
    public List<Transform> connectionPoints;
    public GameObject parentConnection;
    public Chunk chunk;

    [Header("Auto-Connect Settings")]
    public float checkRadius = 0.5f;
    public LayerMask blockLayer;
    public bool canBeDeleted = true;

    private Transform _cachedTransform;
    private GameObject _cachedGameObject;
    private Collider[] _overlapBuffer = new Collider[64];

    public Transform CachedTransform => _cachedTransform != null ? _cachedTransform : (_cachedTransform = transform);
    public GameObject CachedGameObject => _cachedGameObject != null ? _cachedGameObject : (_cachedGameObject = gameObject);

    private void Awake() => CacheReferences();
    private void CacheReferences()
    {
        _cachedTransform = transform;
        _cachedGameObject = gameObject;
    }

    private void Start()
    {
        if (parentConnection != null)
        {
            var parentSystem = parentConnection.GetComponent<ParentBlockScr>();
            parentSystem?.RegisterBlock(this);
        }
        CheckConnections();
    }

    public void CheckConnections()
    {
        int pointCount = connectionPoints.Count;
        for (int p = 0; p < pointCount; p++)
        {
            Vector3 point = connectionPoints[p].position;
            int count = Physics.OverlapSphereNonAlloc(
                point,
                checkRadius,
                _overlapBuffer,
                blockLayer,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < count; i++)
            {
                var col = _overlapBuffer[i];
                if (col.gameObject == CachedGameObject) continue;

                if (!col.TryGetComponent(out Block otherBlock) ||
                    otherBlock.parentConnection != parentConnection)
                    continue;

                Connections.ConnectedObjects.Add(otherBlock.CachedGameObject);
                otherBlock.Connections.ConnectedObjects.Add(CachedGameObject);
            }
        }
    }

    public void DeleteBlock()
    {
        if (!canBeDeleted) return;

        foreach (var bearing in Connections.Bearings.ToList())
            bearing.DestroyBearing();

        foreach (var damper in Connections.Dampers.ToArray())
            damper.DestroyDamper();

        var connectedList = new List<GameObject>(Connections.ConnectedObjects);
        int count = connectedList.Count;

        for (int i = 0; i < count; i++)
        {
            var obj = connectedList[i];
            if (obj == null) continue;

            if (obj.TryGetComponent(out Block b))
            {
                b.Connections.ConnectedObjects.Remove(CachedGameObject);
                if (b.parentConnection != null)
                    b.parentConnection.GetComponent<ParentBlockScr>()?.QueueValidation();
            }
        }

        if (parentConnection != null)
        {
            parentConnection.GetComponent<ParentBlockScr>()?.UnregisterBlock(this);
            if (parentConnection.TryGetComponent(out Block parentBlock))
                parentBlock.Connections.ConnectedObjects.Remove(CachedGameObject);
        }

        if (chunk != null)
        {
            chunk.RemoveBlock(this);
        }

        Destroy(CachedGameObject);
    }

    public Transform GetClosestConnectionPoint(Vector3 hitPoint)
    {
        Transform closest = null;
        float minDist = float.MaxValue;
        int count = connectionPoints.Count;

        for (int i = 0; i < count; i++)
        {
            var p = connectionPoints[i];
            float d = (p.position - hitPoint).sqrMagnitude;
            if (d < minDist)
            {
                minDist = d;
                closest = p;
            }
        }
        return closest;
    }
}