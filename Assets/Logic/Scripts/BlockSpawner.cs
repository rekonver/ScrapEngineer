using UnityEngine;

public class BlockSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera raycastCamera;
    [SerializeField] private GroupSettings groupSettings;

    [Header("Block Settings")]
    [SerializeField] private BlockSpawnConfig blockConfig;

    [Header("Bearing Settings")]
    [SerializeField] private GameObject bearingPrefab;

    [Header("Bearing Settings")]
    [SerializeField] private GameObject damperPrefab;

    private BlockRaycaster raycaster;
    private BlockConnector blockConnector;

    void Awake()
    {
        raycaster = new BlockRaycaster(raycastCamera, blockConfig.blockLayer);
        blockConnector = new BlockConnector(blockConfig, groupSettings);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) SpawnBlock(blockConfig.blockPrefab);
        if (Input.GetKeyDown(KeyCode.E)) SpawnBlock(bearingPrefab, BlockType.Bearing);
        if (Input.GetMouseButtonDown(1)) DeleteBlock();
        if (Input.GetKeyDown(KeyCode.R)) SpawnBlock(damperPrefab, BlockType.Damper);
    }

    private void SpawnBlock(GameObject spawnedPrefab, BlockType blockType = BlockType.Undefined)
    {
        if (!raycaster.Raycast(out Block hitBlock, out RaycastHit hit)) return;
        Transform connectionPoint = hitBlock.GetClosestConnectionPoint(hit.point);
        if (connectionPoint == null) return;
        blockConnector.SpawnConnectedBlock(spawnedPrefab, hitBlock, connectionPoint, blockType);
    }

    private void DeleteBlock()
    {
        if (!raycaster.Raycast(out Block hitBlock, out _)) return;
        hitBlock.DeleteBlock();
    }
}

[System.Serializable]
public class BlockSpawnConfig
{
    public GameObject blockPrefab;
    public float spawnOffset = 0.25f;
    public LayerMask blockLayer;
}

public class BlockRaycaster
{
    private readonly Camera camera;
    private readonly LayerMask layerMask;

    public BlockRaycaster(Camera cam, LayerMask mask)
    {
        camera = cam;
        layerMask = mask;
    }

    public bool Raycast(out Block block, out RaycastHit hit)
    {
        block = null;
        hit = new RaycastHit();

        if (camera == null) return false;
        Ray ray = new Ray(camera.transform.position, camera.transform.forward);

        if (!Physics.Raycast(ray, out hit, 100f, layerMask))
            return false;

        block = hit.collider.GetComponent<Block>();
        return block != null;
    }
}

public class BlockConnector
{
    private readonly BlockSpawnConfig config;
    private readonly GroupSettings groupSettings;
    public BlockConnector(BlockSpawnConfig config, GroupSettings groupSettings)
    {
        this.config = config;
        this.groupSettings = groupSettings;
    }

    public void SpawnConnectedBlock(GameObject PrefabToSpawn, Block parentBlock, Transform connectionPoint, BlockType blockType = BlockType.Undefined)
    {
        Vector3 spawnPosition = connectionPoint.position + connectionPoint.forward * config.spawnOffset;
        Quaternion spawnRotation = connectionPoint.rotation;
        Transform parentTransform = parentBlock.ParentConnection?.transform;

        GameObject newBlockObj = Object.Instantiate(
            PrefabToSpawn,
            spawnPosition,
            spawnRotation,
            parentTransform
        );

        if (newBlockObj.TryGetComponent(out Block newBlock))
        {
            ConnectBlocks(parentBlock, newBlock);
            ValidateParentConnection(parentBlock);

            var bearing = parentBlock.GetComponent<Bearing>();
            if (bearing != null)
            {
                var newParentToBlock = CreateNewParent(parentBlock, spawnPosition);
                newBlockObj.transform.SetParent(newParentToBlock.transform, worldPositionStays: true);
                newBlock.ParentConnection = newParentToBlock;
                bearing.AddEndPoint(newBlock);
            }
        }

        if (blockType == BlockType.Bearing)
        {
            var bearing = newBlockObj.GetComponent<Bearing>();
            bearing.AddStartPoint(parentBlock);
            newBlockObj.transform.rotation = connectionPoint.rotation * bearing.customRotation;
            newBlockObj.transform.SetParent(parentBlock.transform, true);
        }
        if (blockType == BlockType.Damper)
        {
            newBlockObj.transform.SetParent(parentBlock.transform, worldPositionStays: true);
            Damper damper = newBlockObj.GetComponent<Damper>();
            damper.Initialize(parentBlock, connectionPoint.forward.normalized, connectionPoint);
        }
    }

    private GameObject CreateNewParent(Block hitBlock, Vector3 spawnPos)
    {
        var hitParentTransform = hitBlock.ParentConnection.transform;
        return Object.Instantiate(
            groupSettings.groupPrefab,
            spawnPos,
            hitParentTransform.rotation
        );
    }

    private void ConnectBlocks(Block parent, Block child)
    {
        child.ParentConnection = parent.ParentConnection;
        parent.Connections.ConnectedObjects.Add(child.gameObject);
        child.Connections.ConnectedObjects.Add(parent.gameObject);
    }

    private void ValidateParentConnection(Block block)
    {
        if (block.ParentConnection != null)
            block.ParentConnection.GetComponent<ParentBlockScr>()?.QueueValidation();
    }
}