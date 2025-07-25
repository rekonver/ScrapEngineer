using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ParentBlockScr : MonoBehaviour
{
    [Header("References")]
    public GroupSettings groupSettings;
    public HashSet<Block> managedBlocks = new HashSet<Block>();
    public bool SuspendValidation { get; set; } = false;
    private bool validationQueued = false;

    private Transform _cachedTransform;
    private BlockGroup _cachedBlockGroup;
    private Rigidbody _cachedRigidbody;

    void Awake()
    {
        _cachedTransform = transform;
        _cachedBlockGroup = GetComponent<BlockGroup>();
        _cachedRigidbody = GetComponent<Rigidbody>();
        _cachedRigidbody.isKinematic = false;
        RegisterChildBlocks();
    }

    private void RegisterChildBlocks()
    {
        var childBlocks = GetComponentsInChildren<Block>();
        foreach (var block in childBlocks)
            if (block.parentConnection == gameObject)
                RegisterBlock(block);
    }

    private class DisjointSet
    {
        private Dictionary<Block, Block> parent = new Dictionary<Block, Block>();
        private Dictionary<Block, int> rank = new Dictionary<Block, int>();
        private Dictionary<Block, HashSet<Block>> sets = new Dictionary<Block, HashSet<Block>>();

        public DisjointSet(IEnumerable<Block> blocks)
        {
            foreach (var block in blocks)
            {
                parent[block] = block;
                rank[block] = 0;
                sets[block] = new HashSet<Block> { block };
            }
        }

        public Block Find(Block x)
        {
            Block root = x;
            while (parent[root] != root)
                root = parent[root];

            Block current = x;
            while (parent[current] != root)
            {
                Block next = parent[current];
                parent[current] = root;
                current = next;
            }
            return root;
        }

        public void Union(Block a, Block b)
        {
            var rootA = Find(a);
            var rootB = Find(b);
            if (rootA == rootB) return;

            if (rank[rootA] < rank[rootB])
            {
                parent[rootA] = rootB;
                sets[rootB].UnionWith(sets[rootA]);
                sets.Remove(rootA);
            }
            else
            {
                parent[rootB] = rootA;
                sets[rootA].UnionWith(sets[rootB]);
                sets.Remove(rootB);
                if (rank[rootA] == rank[rootB])
                    rank[rootA]++;
            }
        }

        public List<HashSet<Block>> GetSets() => sets.Values.ToList();
    }

    public void RegisterBlock(Block block)
    {
        if (managedBlocks.Add(block))
        {
            block.parentConnection = gameObject;
            QueueValidation();
        }
    }

    public void UnregisterBlock(Block block)
    {
        if (managedBlocks.Remove(block))
        {
            CleanConnections(block);
            QueueValidation();
        }
    }

    private void CleanConnections(Block removed)
    {
        var removedId = removed.CachedGameObject.GetInstanceID();
        foreach (var b in managedBlocks)
            b.connectedObjects.RemoveWhere(go => go != null && go.GetInstanceID() == removedId);
    }

    public void QueueValidation()
    {
        if (validationQueued || SuspendValidation) return;
        validationQueued = true;
        StartCoroutine(DelayedValidation());
    }

    private IEnumerator DelayedValidation()
    {
        yield return new WaitForEndOfFrame();
        ValidateGroup();
        validationQueued = false;
    }

    public void ValidateGroup()
    {
        if (SuspendValidation) return;

        if (managedBlocks.Count == 0)
        {
            DestroyImmediate(gameObject);
            return;
        }

        var subGroups = FindSubGroups();
        if (subGroups.Count <= 1) return;

        StartCoroutine(SplitGroupsCoroutine(subGroups));
    }

    private IEnumerator SplitGroupsCoroutine(List<HashSet<Block>> subGroups)
    {
        DetachAllChildren();
        int groupsPerFrame = Mathf.Max(1, Mathf.Min(10, subGroups.Count / 50));
        var replacementMaps = new List<Dictionary<Block, Block>>(subGroups.Count);

        for (int i = 0; i < subGroups.Count; i++)
        {
            var blockMap = CreateOptimizedGroup(subGroups[i]);
            replacementMaps.Add(blockMap);
            if (i % groupsPerFrame == 0) yield return null;
        }
        DestroyImmediate(gameObject);
    }

    private void DetachAllChildren()
    {
        int childCount = _cachedTransform.childCount;
        if (childCount == 0) return;

        List<Transform> children = new List<Transform>(childCount);
        for (int i = 0; i < childCount; i++)
            children.Add(_cachedTransform.GetChild(i));

        foreach (var child in children)
            child.SetParent(null, true);
    }

    private Dictionary<Block, Block> CreateOptimizedGroup(HashSet<Block> subgroup)
    {
        var oldToNewMap = new Dictionary<Block, Block>();
        if (groupSettings == null || groupSettings.groupPrefab == null || subgroup.Count == 0)
            return oldToNewMap;

        Bounds groupBounds = CalculateSubgroupBounds(subgroup);
        RigidbodyState originalPhysics = CaptureRigidbodyState();
        GameObject newGroupObj = CreateGroupContainer(groupBounds, originalPhysics);
        ParentBlockScr newGroupScr = newGroupObj.GetComponent<ParentBlockScr>();

        MigrateBlocksToNewGroup(subgroup, newGroupObj.transform, newGroupScr, oldToNewMap);

        if (newGroupObj.TryGetComponent(out BlockGroup newBlockGroup))
            newBlockGroup.UpdateGroupBlocks(newGroupScr.managedBlocks);

        return oldToNewMap;
    }

    private Bounds CalculateSubgroupBounds(HashSet<Block> subgroup)
    {
        Bounds bounds = new Bounds();
        bool hasBounds = false;

        foreach (var block in subgroup)
        {
            if (block == null) continue;

            if (!hasBounds)
            {
                bounds = new Bounds(block.transform.position, Vector3.zero);
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(block.transform.position);
            }
        }
        return bounds;
    }

    private RigidbodyState CaptureRigidbodyState() => new RigidbodyState
    {
        velocity = _cachedRigidbody.velocity,
        angularVelocity = _cachedRigidbody.angularVelocity,
        drag = _cachedRigidbody.drag,
        angularDrag = _cachedRigidbody.angularDrag
    };

    private GameObject CreateGroupContainer(Bounds bounds, RigidbodyState physicsState)
    {
        GameObject newObj = Instantiate(
            groupSettings.groupPrefab,
            bounds.center,
            _cachedTransform.rotation
        );

        newObj.name = $"BlockGroup_{bounds.size.magnitude:F1}units";
        Rigidbody newRb = newObj.GetComponent<Rigidbody>() ?? newObj.AddComponent<Rigidbody>();

        newRb.velocity = physicsState.velocity;
        newRb.angularVelocity = physicsState.angularVelocity;
        newRb.drag = physicsState.drag;
        newRb.angularDrag = physicsState.angularDrag;

        return newObj;
    }

    private void MigrateBlocksToNewGroup(
        HashSet<Block> subgroup,
        Transform newParent,
        ParentBlockScr newGroupScr,
        Dictionary<Block, Block> blockMap)
    {
        foreach (var oldBlock in subgroup)
        {
            if (oldBlock == null) continue;

            oldBlock.transform.SetParent(newParent, true);
            oldBlock.parentConnection = newParent.gameObject;
            oldBlock.connectedObjects.RemoveWhere(go => go == null);
            newGroupScr.managedBlocks.Add(oldBlock);
            blockMap.Add(oldBlock, oldBlock);
            CheckAdvancedBlock(oldBlock, newGroupScr);
            managedBlocks.Remove(oldBlock);
        }
    }

    private void CheckAdvancedBlock(Block block, ParentBlockScr newGroupScr)
    {
        var newGroupObj = newGroupScr.gameObject;
        var newGroupRb = newGroupObj.GetComponent<Rigidbody>();

        if (block.bearings.Count > 0)
        {
            foreach (var b in block.bearings)
            {
                if (block == b.EndConnection)
                    b.joint.connectedBody = newGroupRb;
                else if (block == b.StartConnection)
                {
                    var duplicated = b.DuplicateJoint(newGroupObj, b.StartConnection, b.EndConnection);
                    if (duplicated != null) b.joint = duplicated;
                }
            }
        }
        if (block.dampers.Count > 0)
        {
            foreach (var d in block.dampers)
            {
                if (block == d.EndConnection)
                    d.configurableJoint.connectedBody = newGroupRb;
                else if (block == d.StartConnection)
                    d.configurableJoint = d.CopyJointParameters(newGroupObj.transform);
            }
        }
    }

    private struct RigidbodyState
    {
        public Vector3 velocity;
        public Vector3 angularVelocity;
        public float drag;
        public float angularDrag;
    }

    private List<HashSet<Block>> FindSubGroups() =>
        managedBlocks.Count < 100 ? FindSubGroupsSimple() : FindSubGroupsOptimized();

    private List<HashSet<Block>> FindSubGroupsOptimized()
    {
        var uf = new DisjointSet(managedBlocks);
        var blocksList = managedBlocks.ToList();
        int count = blocksList.Count;

        for (int i = 0; i < count; i++)
        {
            var block = blocksList[i];
            if (block == null) continue;

            var connectedObjects = new List<GameObject>(block.connectedObjects);
            int connCount = connectedObjects.Count;

            for (int j = 0; j < connCount; j++)
            {
                var connectedObj = connectedObjects[j];
                if (connectedObj == null) continue;

                if (connectedObj.TryGetComponent(out Block neighbor) &&
                    managedBlocks.Contains(neighbor))
                    uf.Union(block, neighbor);
            }
        }
        return uf.GetSets();
    }

    private List<HashSet<Block>> FindSubGroupsSimple()
    {
        var visited = new HashSet<Block>();
        var groups = new List<HashSet<Block>>();

        foreach (var block in managedBlocks)
        {
            if (block == null || visited.Contains(block)) continue;

            var group = new HashSet<Block>();
            var stack = new Stack<Block>();
            stack.Push(block);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (visited.Contains(current)) continue;

                visited.Add(current);
                group.Add(current);

                var connectedObjects = new List<GameObject>(current.connectedObjects);
                int count = connectedObjects.Count;

                for (int i = 0; i < count; i++)
                {
                    var connectedObj = connectedObjects[i];
                    if (connectedObj != null &&
                        connectedObj.TryGetComponent(out Block neighbor) &&
                        !visited.Contains(neighbor) &&
                        managedBlocks.Contains(neighbor))
                    {
                        stack.Push(neighbor);
                    }
                }
            }
            groups.Add(group);
        }
        return groups;
    }
}