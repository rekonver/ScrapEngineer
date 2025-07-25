using UnityEngine;
using System.Collections;

public class GridBlockSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject blockPrefab;
    public GameObject groupPrefab;
    public Vector3 startOffset = Vector3.zero;
    public float spacing = 1.1f;
    public int sizeX = 100;
    public int sizeY = 100;
    public int sizeZ = 100;

    private ParentBlockScr parentSystem;
    private GameObject parentObj;
    private Rigidbody rbParent;

    private void Start()
    {
        if (blockPrefab == null || groupPrefab == null)
        {
            Debug.LogError("BlockPrefab or GroupPrefab not set!");
            enabled = false;
            return;
        }

        parentObj = Instantiate(groupPrefab, startOffset, Quaternion.identity);
        parentSystem = parentObj.GetComponent<ParentBlockScr>();
        rbParent = parentObj.GetComponent<Rigidbody>();
        rbParent.isKinematic = true;

        parentSystem.SuspendValidation = true;
        StartCoroutine(SpawnBlocksCoroutine());
    }

    private IEnumerator SpawnBlocksCoroutine()
    {
        int totalBlocks = sizeX * sizeY * sizeZ;
        int blocksPerFrame = Mathf.Max(100, totalBlocks / 100);

        int spawned = 0;
        for (int x = 0; x < sizeX; x++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    Vector3 position = parentObj.transform.position +
                                      new Vector3(x * spacing, y * spacing, z * spacing);

                    var blockObj = Instantiate(blockPrefab, position, Quaternion.identity);

                    if (blockObj.TryGetComponent<Rigidbody>(out var rb))
                        Destroy(rb);

                    if (blockObj.TryGetComponent(out Block block))
                    {
                        block.parentConnection = parentObj;
                        parentSystem.managedBlocks.Add(block);
                        parentSystem.AddBlockToChunk(block);
                    }

                    spawned++;
                    if (spawned % blocksPerFrame == 0)
                        yield return null;
                }
            }
        }

        StartCoroutine(CheckConnectionsParallel());
        Debug.Log($"[GridSpawner] Spawned {spawned} blocks");
        rbParent.isKinematic = false;
    }

    private IEnumerator CheckConnectionsParallel()
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForEndOfFrame();

        int blocksPerFrame = Mathf.Max(50, parentSystem.managedBlocks.Count / 50);
        int processed = 0;

        foreach (var block in parentSystem.managedBlocks)
        {
            block.CheckConnections();
            processed++;

            if (processed % blocksPerFrame == 0)
                yield return null;
        }

        parentSystem.SuspendValidation = false;
        parentSystem.QueueValidation();
    }
}