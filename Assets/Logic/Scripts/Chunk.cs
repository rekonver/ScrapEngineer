using UnityEngine;
using System.Collections.Generic;

public class Chunk : MonoBehaviour
{
    public const int CHUNK_CAPACITY = 100;

    public HashSet<Block> blocks = new HashSet<Block>();
    public ParentBlockScr parentSystem;

    public bool IsFull => blocks.Count >= CHUNK_CAPACITY;
    public bool IsEmpty => blocks.Count == 0;

    public void AddBlock(Block block)
    {
        if (IsFull) return;

        blocks.Add(block);
        block.chunk = this;
        block.transform.SetParent(transform, true);
    }

    public void RemoveBlock(Block block)
    {
        blocks.Remove(block);
        block.chunk = null;

        if (IsEmpty)
        {
            parentSystem.RemoveChunk(this);
            Destroy(gameObject);
        }
    }

    public void CheckChunkIntegrity()
    {
        if (blocks.Count < 2) return;

        var subGroups = FindSubGroups();
        if (subGroups.Count <= 1) return;

        parentSystem.SplitChunk(this, subGroups);
    }

    private List<HashSet<Block>> FindSubGroups()
    {
        var visited = new HashSet<Block>();
        var groups = new List<HashSet<Block>>();

        foreach (var block in blocks)
        {
            if (visited.Contains(block)) continue;

            var group = new HashSet<Block>();
            var stack = new Stack<Block>();
            stack.Push(block);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (visited.Contains(current)) continue;

                visited.Add(current);
                group.Add(current);

                foreach (var connObj in current.Connections.ConnectedObjects)
                {
                    if (connObj != null &&
                        connObj.TryGetComponent(out Block neighbor) &&
                        neighbor.chunk == this &&
                        !visited.Contains(neighbor))
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