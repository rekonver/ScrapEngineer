using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ParentBlockScr : MonoBehaviour
{
    [Header("References")]
    public GroupSettings groupSettings;
    public HashSet<Block> managedBlocks = new HashSet<Block>();
    public List<Chunk> chunks = new List<Chunk>();
    public bool SuspendValidation { get; set; } = false;
    private bool validationQueued = false;

    private Transform _cachedTransform;
    private Rigidbody _cachedRigidbody;

    void Awake()
    {
        _cachedTransform = transform;
        _cachedRigidbody = GetComponent<Rigidbody>();
        _cachedRigidbody.isKinematic = false;
    }

    public void RegisterBlock(Block block)
    {
        if (managedBlocks.Add(block))
        {
            block.parentConnection = gameObject;
            AddBlockToChunk(block);
        }
    }

    //public void UnregisterBlock(Block block)
    //{
    //    if (managedBlocks.Remove(block))
    //    {
    //        QueueValidation();
    //    }
    //}
    public void UnregisterBlock(Block removed)
    {
        if (!managedBlocks.Remove(removed))
            return;

        var neighbors = GetRemainingNeighbors(removed);

        if (neighbors.Count == 0)
        {
            //QueueValidation();
            return;
        }

        var subGroups = CollectSubGroups(neighbors, removed);

        AddUnreachableBlocks(subGroups, removed);

        if (subGroups.Count <= 1)
        {
            //QueueValidation();
            return;
        }

        SplitGroupsCoroutineImmediate(subGroups);
    }

    private List<Block> GetRemainingNeighbors(Block removed)
    {
        return removed.Connections.ConnectedObjects
            .Where(o => o != null && o.TryGetComponent<Block>(out var b) && managedBlocks.Contains(b))
            .Select(o => o.GetComponent<Block>())
            .Distinct()
            .ToList();
    }

    private List<HashSet<Block>> CollectSubGroups(List<Block> neighbors, Block removed)
    {
        var visited = new HashSet<Block>();
        var subGroups = new List<HashSet<Block>>();

        foreach (var start in neighbors)
        {
            if (visited.Contains(start))
                continue;

            var group = new HashSet<Block>();
            var queue = new Queue<Block>();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                var curr = queue.Dequeue();
                group.Add(curr);

                foreach (var obj in curr.Connections.ConnectedObjects)
                {
                    if (obj == null || !obj.TryGetComponent<Block>(out var nb) || nb == removed)
                        continue;

                    if (!managedBlocks.Contains(nb) || visited.Contains(nb))
                        continue;

                    visited.Add(nb);
                    queue.Enqueue(nb);
                }
            }

            subGroups.Add(group);
        }

        return subGroups;
    }

    private void AddUnreachableBlocks(List<HashSet<Block>> subGroups, Block removed)
    {
        var allVisited = new HashSet<Block>(subGroups.SelectMany(g => g));
        var unreachable = managedBlocks.Except(allVisited).ToHashSet();
        if (unreachable.Count > 0)
            subGroups.Add(unreachable);
    }

    // Швидка версія SplitGroups без корутин
    private void SplitGroupsCoroutineImmediate(List<HashSet<Block>> subGroups)
    {
        var first = subGroups[0];
        subGroups.RemoveAt(0);

        foreach (var group in subGroups)
        {
            CreateNewParentForGroup(group);
            foreach (var b in group)
                managedBlocks.Remove(b);
        }
    }


    public void AddBlockToChunk(Block block)
    {
        // Try to add to existing chunk with space
        foreach (var chunk in chunks)
        {
            if (!chunk.IsFull)
            {
                chunk.AddBlock(block);
                return;
            }
        }

        // Create new chunk if no space
        CreateNewChunk().AddBlock(block);
    }

    private Chunk CreateNewChunk()
    {
        GameObject chunkObj = new GameObject($"Chunk_{chunks.Count}");
        chunkObj.transform.SetParent(_cachedTransform);

        Chunk chunk = chunkObj.AddComponent<Chunk>();
        chunk.parentSystem = this;
        chunks.Add(chunk);

        return chunk;
    }

    public void RemoveChunk(Chunk chunk)
    {
        chunks.Remove(chunk);
        Destroy(chunk.gameObject);
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

        // Then validate global connections
        var subGroups = FindSubGroups();
        if (subGroups.Count <= 1) return;

        StartCoroutine(SplitGroupsCoroutine(subGroups));
    }

    private IEnumerator SplitGroupsCoroutine(List<HashSet<Block>> subGroups)
    {
        // First group stays in current parent
        var firstGroup = subGroups[0];
        subGroups.RemoveAt(0);

        // Create new parents for other groups
        foreach (var group in subGroups)
        {
            CreateNewParentForGroup(group);
            yield return null;
        }
    }

    private void CreateNewParentForGroup(HashSet<Block> group)
    {
        if (group.Count == 0) return;

        Bounds groupBounds = CalculateGroupBounds(group);
        var newParentObj = Instantiate(groupSettings.groupPrefab, groupBounds.center, Quaternion.identity);
        var newParentSystem = newParentObj.GetComponent<ParentBlockScr>();

        // Transfer physics state
        var newRb = newParentObj.GetComponent<Rigidbody>();
        newRb.velocity = _cachedRigidbody.velocity;
        newRb.angularVelocity = _cachedRigidbody.angularVelocity;
        newRb.drag = _cachedRigidbody.drag;
        newRb.angularDrag = _cachedRigidbody.angularDrag;

        // Move blocks to new parent
        foreach (var block in group)
        {
            managedBlocks.Remove(block);
            block.parentConnection = newParentObj;
            newParentSystem.managedBlocks.Add(block);

            // Reassign to new parent's chunks
            newParentSystem.AddBlockToChunk(block);
        }
    }

    private Bounds CalculateGroupBounds(HashSet<Block> blocks)
    {
        Bounds bounds = new Bounds();
        bool hasBounds = false;

        foreach (var block in blocks)
        {
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

    private List<HashSet<Block>> FindSubGroups()
    {
        var blocksList = managedBlocks.ToList();
        int n = blocksList.Count;
        if (n == 0) return new List<HashSet<Block>>();

        var blockToIndex = new Dictionary<Block, int>(n);
        var indexToBlock = new Block[n];

        for (int i = 0; i < n; i++)
        {
            Block block = blocksList[i];
            blockToIndex[block] = i;
            indexToBlock[i] = block;
        }

        int[] parent = new int[n];
        int[] rank = new int[n];
        for (int i = 0; i < n; i++)
        {
            parent[i] = i;
            rank[i] = 0;
        }

        int Find(int x)
        {
            if (parent[x] != x)
                parent[x] = Find(parent[x]);
            return parent[x];
        }

        for (int i = 0; i < n; i++)
        {
            Block block = blocksList[i];
            foreach (var connObj in block.Connections.ConnectedObjects)
            {
                if (connObj != null &&
                    connObj.TryGetComponent(out Block neighbor) &&
                    blockToIndex.TryGetValue(neighbor, out int j))
                {
                    int rootI = Find(i);
                    int rootJ = Find(j);
                    if (rootI == rootJ) continue;

                    if (rank[rootI] < rank[rootJ])
                    {
                        parent[rootI] = rootJ;
                    }
                    else if (rank[rootI] > rank[rootJ])
                    {
                        parent[rootJ] = rootI;
                    }
                    else
                    {
                        parent[rootJ] = rootI;
                        rank[rootI]++;
                    }
                }
            }
        }

        var groups = new Dictionary<int, HashSet<Block>>();
        for (int i = 0; i < n; i++)
        {
            int root = Find(i);
            if (!groups.TryGetValue(root, out var group))
            {
                group = new HashSet<Block>();
                groups[root] = group;
            }
            group.Add(indexToBlock[i]);
        }

        return groups.Values.ToList();
    }
}