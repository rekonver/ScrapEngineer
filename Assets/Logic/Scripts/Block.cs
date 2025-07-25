using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class BlockConnections
{
    public List<Bearing> Bearings { get; } = new List<Bearing>();
    public List<Damper> Dampers { get; } = new List<Damper>();
    public HashSet<GameObject> ConnectedObjects { get; } = new HashSet<GameObject>();
}

public class Block : MonoBehaviour
{
    [Header("Connections")]
    public BlockConnections Connections = new BlockConnections();

    [Header("Connections")]
    public List<Transform> connectionPoints;
    public GameObject parentConnection;

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
        Vector3[] positions = new Vector3[pointCount];
        for (int i = 0; i < pointCount; i++)
            positions[i] = connectionPoints[i].position;

        for (int p = 0; p < positions.Length; p++)
        {
            int count = Physics.OverlapSphereNonAlloc(
                positions[p],
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

    /*
    private void OnDrawGizmos()
    {
        if (connectionPoints == null || connectionPoints.Count == 0)
            return;

        Gizmos.color = Color.yellow;

        foreach (var point in connectionPoints)
        {
            if (point != null)
                Gizmos.DrawWireSphere(point.position, checkRadius);
        }

        Gizmos.color = Color.green;

        if (Connections != null && Connections.ConnectedObjects != null)
        {
            foreach (var obj in Connections.ConnectedObjects)
            {
                if (obj != null)
                    Gizmos.DrawLine(transform.position, obj.transform.position);
            }
        }
    }
    */
}