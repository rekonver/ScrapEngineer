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
    [HideInInspector] public BlockConnections Connections = new BlockConnections();

    [Header("Connections")]
    public GameObject ParentConnection;
    public Chunk Chunk;

    public Transform CachedTransform => _cachedTransform != null ? _cachedTransform : (_cachedTransform = transform);
    public GameObject CachedGameObject => _cachedGameObject != null ? _cachedGameObject : (_cachedGameObject = gameObject);

    [Header("Auto-Connect Settings")]
    [SerializeField] private float checkRadius = 0.5f;
    [SerializeField] private LayerMask blockLayer;
    [SerializeField] private bool canBeDeleted = true;
    [SerializeField] private List<Transform> connectionPoints;

    private Transform _cachedTransform;
    private GameObject _cachedGameObject;
    private Collider[] _overlapBuffer = new Collider[64];

    private void Awake() => CacheReferences();
    private void CacheReferences()
    {
        _cachedTransform = transform;
        _cachedGameObject = gameObject;
    }

    private void Start()
    {
        if (ParentConnection != null)
        {
            var parentSystem = ParentConnection.GetComponent<ParentBlockScr>();
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
                    otherBlock.ParentConnection != ParentConnection)
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
                //if (b.ParentConnection != null)
                //    b.ParentConnection.GetComponent<ParentBlockScr>()?.QueueValidation();
            }
        }

        if (ParentConnection != null)
        {
            ParentConnection.GetComponent<ParentBlockScr>()?.UnregisterBlock(this);
            if (ParentConnection.TryGetComponent(out Block parentBlock))
                parentBlock.Connections.ConnectedObjects.Remove(CachedGameObject);
        }

        if (Chunk != null)
        {
            Chunk.RemoveBlock(this);
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