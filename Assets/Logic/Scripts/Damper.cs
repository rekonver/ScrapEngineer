using UnityEngine;

public class Damper : MonoBehaviour
{
    public Block StartConnection;
    public Block EndConnection;
    public ConfigurableJoint configurableJoint;
    private float CurrentLength;
    [SerializeField] int coreguvaniaLenght = 2;

    [SerializeField] private DamperConfig config;
    private GameObject visualRepresentation;
    Transform spawnDirection;

    public void Initialize(Block startBlock, Vector3 direction, Transform spawnConnectionPoint)
    {
        StartConnection = startBlock;
        CurrentLength = config.defaultLength;
        spawnDirection = spawnConnectionPoint;

        CreateEndBlock(direction);
        CreateParentGroup();
        SetupConnections();
        CreateConfigurableJoint(StartConnection.parentConnection, EndConnection.parentConnection);
        CreateVisualRepresentation();
    }

    private void CreateEndBlock(Vector3 direction)
    {
        Vector3 endPosition = StartConnection.transform.position + direction * (CurrentLength + 1) * 0.5f;
        GameObject endBlockObj = Instantiate(config.endBlockPrefab, endPosition, StartConnection.transform.rotation);
        EndConnection = endBlockObj.GetComponent<Block>();
    }

    private void CreateParentGroup()
    {
        var startParentScr = StartConnection.parentConnection.GetComponent<ParentBlockScr>();
        var parentConfig = startParentScr.groupSettings;
        GameObject damperParentEnd = Instantiate(parentConfig.groupPrefab,
                                            EndConnection.transform.position,
                                            Quaternion.identity);
        damperParentEnd.name = "DamperParent";
        EndConnection.transform.SetParent(damperParentEnd.transform, true);
        EndConnection.parentConnection = damperParentEnd;
    }

    private void SetupConnections()
    {
        StartConnection.dampers.Add(this);
        EndConnection.dampers.Add(this);
    }

    private void CreateConfigurableJoint(GameObject parentGroup, GameObject endParentGroup)
    {
        configurableJoint = parentGroup.AddComponent<ConfigurableJoint>();
        configurableJoint.autoConfigureConnectedAnchor = false;
        SetupDefaultJointParameters(parentGroup, endParentGroup);
        SetupJointLimits();
        SetupJointDrives();
        SetupJointStability();
    }

    public ConfigurableJoint CopyJointParameters(Transform parentGroup)
    {
        var targetJoint = parentGroup.gameObject.AddComponent<ConfigurableJoint>();
        targetJoint.autoConfigureConnectedAnchor = false;

        targetJoint.connectedBody = configurableJoint.connectedBody;
        var getEndParentGroup = configurableJoint.connectedBody.transform;

        SetJointAxes(targetJoint, StartConnection.transform, EndConnection.transform, parentGroup);
        SetJointAnchors(targetJoint, StartConnection.transform, EndConnection.transform, parentGroup, getEndParentGroup);
        ConfigureJointMotions(targetJoint);

        targetJoint.linearLimit = configurableJoint.linearLimit;
        targetJoint.linearLimitSpring = configurableJoint.linearLimitSpring;

        targetJoint.xDrive = configurableJoint.xDrive;
        targetJoint.yDrive = configurableJoint.yDrive;
        targetJoint.zDrive = configurableJoint.zDrive;

        targetJoint.projectionMode = configurableJoint.projectionMode;
        targetJoint.projectionDistance = configurableJoint.projectionDistance;
        targetJoint.projectionAngle = configurableJoint.projectionAngle;
        targetJoint.configuredInWorldSpace = configurableJoint.configuredInWorldSpace;
        targetJoint.swapBodies = configurableJoint.swapBodies;

        targetJoint.breakForce = configurableJoint.breakForce;
        targetJoint.breakTorque = configurableJoint.breakTorque;

        Destroy(configurableJoint);
        return targetJoint;
    }

    private void SetupDefaultJointParameters(GameObject parentGroup, GameObject endParentGroup)
    {
        configurableJoint.connectedBody = endParentGroup.GetComponent<Rigidbody>();
        SetJointAxes(configurableJoint, StartConnection.transform, EndConnection.transform, parentGroup.transform);
        SetJointAnchors(configurableJoint, StartConnection.transform, EndConnection.transform, parentGroup.transform, endParentGroup.transform, true);
        ConfigureJointMotions(configurableJoint);
    }

    private void SetJointAxes(ConfigurableJoint joint,
                            Transform startConnection, Transform endConnection,
                            Transform parentGroup)
    {
        Vector3 worldDirection = spawnDirection.forward;
        Vector3 localDirection = parentGroup.InverseTransformDirection(worldDirection).normalized;
        joint.axis = localDirection;
        joint.secondaryAxis = CalculatePerpendicularAxis(localDirection);
    }

    private void SetJointAnchors(ConfigurableJoint joint,
                            Transform startConnection, Transform endConnection,
                            Transform parentGroup, Transform endParentGroup,
                            bool isFirstSpawn = false)
    {
        Vector3 capsuleWorldPos = startConnection.position;
        Vector3 directionToTriangle = -spawnDirection.forward;

        joint.anchor = parentGroup.InverseTransformPoint(capsuleWorldPos);
        Vector3 desiredAnchorPos = capsuleWorldPos + directionToTriangle * ((CurrentLength + 1) * 0.5f - Vector3.Distance(capsuleWorldPos, endConnection.position));
        joint.connectedAnchor = endParentGroup.InverseTransformPoint(desiredAnchorPos);
    }

    private void ConfigureJointMotions(ConfigurableJoint joint)
    {
        joint.xMotion = ConfigurableJointMotion.Locked;
        joint.yMotion = ConfigurableJointMotion.Locked;
        joint.zMotion = ConfigurableJointMotion.Locked;
        joint.xMotion = ConfigurableJointMotion.Limited;
        joint.angularXMotion = ConfigurableJointMotion.Locked;
        joint.angularYMotion = ConfigurableJointMotion.Locked;
        joint.angularZMotion = ConfigurableJointMotion.Locked;
    }

    private Vector3 CalculatePerpendicularAxis(Vector3 direction)
    {
        Vector3 helperVector = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.8f ?
            Vector3.right : Vector3.up;
        return Vector3.Cross(direction, helperVector).normalized;
    }

    private void SetupJointLimits()
    {
        SoftJointLimit limit = new SoftJointLimit
        {
            limit = CurrentLength * config.maxDistanceMultiplier,
            bounciness = 0.1f,
            contactDistance = 0.01f
        };
        configurableJoint.linearLimit = limit;

        SoftJointLimitSpring limitSpring = new SoftJointLimitSpring
        {
            spring = config.springForce,
            damper = config.damper
        };
        configurableJoint.linearLimitSpring = limitSpring;
    }

    private void SetupJointDrives()
    {
        JointDrive drive = new JointDrive
        {
            positionSpring = config.springForce,
            positionDamper = config.damper,
            maximumForce = config.springForce * 2f
        };
        configurableJoint.xDrive = drive;
    }

    private void SetupJointStability()
    {
        configurableJoint.projectionMode = config.projectionMode;
        configurableJoint.projectionDistance = config.projectionDistance;
        configurableJoint.projectionAngle = config.projectionAngle;
        configurableJoint.configuredInWorldSpace = config.configuredInWorldSpace;
        configurableJoint.swapBodies = config.swapBodies;
        configurableJoint.breakForce = config.breakForce;
        configurableJoint.breakTorque = config.breakTorque;
    }

    private void CreateVisualRepresentation()
    {
        visualRepresentation = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visualRepresentation.transform.SetParent(EndConnection.parentConnection.transform);
        Destroy(visualRepresentation.GetComponent<Collider>());

        if (config.damperMaterial != null)
            visualRepresentation.GetComponent<Renderer>().material = config.damperMaterial;

        UpdateVisualRepresentation();
    }

    private void UpdateVisualRepresentation()
    {
        if (visualRepresentation == null) return;

        Vector3 startPos = StartConnection.transform.position;
        Vector3 endPos = EndConnection.transform.position;

        visualRepresentation.transform.position = (startPos + endPos) / 2f;
        visualRepresentation.transform.up = (endPos - startPos).normalized;

        float distance = Vector3.Distance(startPos, endPos);
        visualRepresentation.transform.localScale = new Vector3(
            config.cylinderDiameter,
            distance / 2f,
            config.cylinderDiameter
        );
    }

    void Update()
    {
        if (visualRepresentation != null)
            UpdateVisualRepresentation();
    }

    public void DestroyDamper()
    {
        if (StartConnection != null) StartConnection.dampers.Remove(this);
        if (EndConnection != null) EndConnection.dampers.Remove(this);

        if (configurableJoint != null) Destroy(configurableJoint);
        if (EndConnection != null && EndConnection.gameObject != null) Destroy(EndConnection.gameObject);
        if (visualRepresentation != null) Destroy(visualRepresentation);

        Destroy(gameObject);
    }
}