using UnityEngine;
using System.Collections.Generic;

public class BlockGroup : MonoBehaviour
{
    public void UpdateGroupBlocks(IEnumerable<Block> blocks)
    {
        Transform cachedTransform = transform;
        int childCount = cachedTransform.childCount;

        if (childCount > 0)
        {
            List<Transform> children = new List<Transform>(childCount);
            for (int i = 0; i < childCount; i++)
                children.Add(cachedTransform.GetChild(i));

            foreach (var child in children)
                child.SetParent(null);
        }

        foreach (var blk in blocks)
            blk.CachedTransform.SetParent(cachedTransform);
    }
}