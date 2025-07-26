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

    void Update()
    {
        if (visualRepresentation != null)
            UpdateVisualRepresentation();
    }

    public void Initialize(Block startBlock, Vector3 direction, Transform spawnConnectionPoint)
    {
        StartConnection = startBlock;
        CurrentLength = config.DefaultLength;
        spawnDirection = spawnConnectionPoint;

        CreateEndBlock(direction);
        CreateParentGroup();
        SetupConnections();
        CreateConfigurableJoint(StartConnection.ParentConnection, EndConnection.ParentConnection);
        CreateVisualRepresentation();
    }

    private void CreateEndBlock(Vector3 direction)
    {
        Vector3 endPosition = StartConnection.transform.position + direction * (CurrentLength + 1) * 0.5f;
        GameObject endBlockObj = Instantiate(config.EndBlockPrefab, endPosition, StartConnection.transform.rotation);
        EndConnection = endBlockObj.GetComponent<Block>();
    }

    private void CreateParentGroup()
    {
        var startParentScr = StartConnection.ParentConnection.GetComponent<ParentBlockScr>();
        var parentConfig = startParentScr.GroupSettings;
        GameObject damperParentEnd = Instantiate(parentConfig.groupPrefab,
                                            EndConnection.transform.position,
                                            Quaternion.identity);
        damperParentEnd.name = "DamperParent";
        EndConnection.transform.SetParent(damperParentEnd.transform, true);
        EndConnection.ParentConnection = damperParentEnd;
    }

    private void SetupConnections()
    {
        StartConnection.Connections.Dampers.Add(this);
        EndConnection.Connections.Dampers.Add(this);
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

    private void SetJointAnchors(
        ConfigurableJoint joint,
        Transform startPointTransform, Transform endPointTransform,
        Transform parentBodyTransform, Transform connectedBodyTransform,
        bool isFirstSpawn = false)
    {
        Vector3 worldAnchorPosition = startPointTransform.position;
        Vector3 inverseDirection = -spawnDirection.forward;

        joint.anchor = parentBodyTransform.InverseTransformPoint(worldAnchorPosition);

        float halfTotalLength = (CurrentLength + 1f) * 0.5f;
        float currentDistance = Vector3.Distance(worldAnchorPosition, endPointTransform.position);
        Vector3 adjustedWorldConnectedAnchor = worldAnchorPosition
            + inverseDirection * (halfTotalLength - currentDistance);

        joint.connectedAnchor = connectedBodyTransform.InverseTransformPoint(adjustedWorldConnectedAnchor);
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
            limit = CurrentLength * config.MaxDistanceMultiplier,
            bounciness = 0.1f,
            contactDistance = 0.01f
        };
        configurableJoint.linearLimit = limit;

        SoftJointLimitSpring limitSpring = new SoftJointLimitSpring
        {
            spring = config.SpringForce,
            damper = config.Damper
        };
        configurableJoint.linearLimitSpring = limitSpring;
    }

    private void SetupJointDrives()
    {
        JointDrive drive = new JointDrive
        {
            positionSpring = config.SpringForce,
            positionDamper = config.Damper,
            maximumForce = config.SpringForce * 2f
        };
        configurableJoint.xDrive = drive;
    }

    private void SetupJointStability()
    {
        configurableJoint.projectionMode = config.ProjectionMode;
        configurableJoint.projectionDistance = config.ProjectionDistance;
        configurableJoint.projectionAngle = config.ProjectionAngle;
        configurableJoint.configuredInWorldSpace = config.ConfiguredInWorldSpace;
        configurableJoint.swapBodies = config.SwapBodies;
        configurableJoint.breakForce = config.BreakForce;
        configurableJoint.breakTorque = config.BreakTorque;
    }

    private void CreateVisualRepresentation()
    {
        visualRepresentation = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visualRepresentation.transform.SetParent(EndConnection.ParentConnection.transform);
        Destroy(visualRepresentation.GetComponent<Collider>());

        if (config.DamperMaterial != null)
            visualRepresentation.GetComponent<Renderer>().material = config.DamperMaterial;

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
            config.CylinderDiameter,
            distance / 2f,
            config.CylinderDiameter
        );
    }

    public void DestroyDamper()
    {
        if (StartConnection != null) StartConnection.Connections.Dampers.Remove(this);
        if (EndConnection != null) EndConnection.Connections.Dampers.Remove(this);

        if (configurableJoint != null) Destroy(configurableJoint);
        if (EndConnection != null && EndConnection.gameObject != null) Destroy(EndConnection.gameObject);
        if (visualRepresentation != null) Destroy(visualRepresentation);

        Destroy(gameObject);
    }
}