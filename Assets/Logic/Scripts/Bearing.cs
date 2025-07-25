using UnityEngine;
using UnityEngine.UIElements.Experimental;

public class Bearing : MonoBehaviour
{
    [SerializeField] Block blockInfo;
    public Block StartConnection;
    public Block EndConnection;
    public HingeJoint joint;
    public Quaternion customRotation = Quaternion.identity;
    private GameObject parentBlock;

    public void AddStartPoint(Block block)
    {
        StartConnection = block;
        CallBlockToAddBearing(block);
        parentBlock = block.parentConnection;
    }

    public void AddEndPoint(Block block)
    {
        EndConnection = block;
        CallBlockToAddBearing(block);
        CreateHingleJoint(parentBlock, block);
        GetComponent<Collider>().enabled = false;
    }

    private void CallBlockToAddBearing(Block block) => block.Connections.Bearings.Add(this);

    private void CreateHingleJoint(GameObject parentGroup, Block endBlock = null)
    {
        var bearingJoint = parentGroup.AddComponent<HingeJoint>();
        if (endBlock != null)
            SetStartSettingsBearing(bearingJoint, endBlock, parentGroup);
    }

    private void SetStartSettingsBearing(HingeJoint bearingJoint, Block endBlock, GameObject parentGroup)
    {
        var endParentGroup = endBlock.parentConnection;
        var endParentRb = endParentGroup.GetComponent<Rigidbody>();

        if (endParentRb == null)
        {
            Debug.LogError("У endBlock.parentConnection немає Rigidbody!");
            return;
        }

        bearingJoint.connectedBody = endParentRb;
        bearingJoint.autoConfigureConnectedAnchor = false;

        SetJointAnchors(bearingJoint, endBlock.transform.position, parentGroup.transform, endParentRb.transform);
        SetRotationAxis(bearingJoint, StartConnection.transform, EndConnection.transform, parentGroup.transform);
        joint = bearingJoint;
    }

    private void SetJointAnchors(HingeJoint joint, Vector3 worldAnchorPosition, Transform parentTransform, Transform connectedTransform)
    {
        joint.anchor = parentTransform.InverseTransformPoint(worldAnchorPosition);
        joint.connectedAnchor = connectedTransform.InverseTransformPoint(worldAnchorPosition);
    }

    private void SetRotationAxis(HingeJoint joint, Transform startTransform, Transform endTransform, Transform hingeTransform)
    {
        Vector3 direction = (startTransform.position - endTransform.position).normalized;
        Vector3 localAxis = hingeTransform.InverseTransformDirection(direction);
        joint.axis = localAxis;
    }

    public HingeJoint DuplicateJoint(GameObject newParentGroup, Block startBlock, Block endBlock)
    {
        if (joint == null)
        {
            Debug.LogWarning("Немає оригінального joint для копіювання!");
            return null;
        }

        var newJoint = newParentGroup.AddComponent<HingeJoint>();
        newJoint.connectedBody = joint.connectedBody;
        newJoint.autoConfigureConnectedAnchor = false;

        SetJointAnchors(newJoint, endBlock.transform.position, newParentGroup.transform, endBlock.parentConnection.transform);
        SetRotationAxis(newJoint, startBlock.transform, endBlock.transform, newParentGroup.transform);

        newJoint.useLimits = joint.useLimits;
        newJoint.limits = joint.limits;
        newJoint.useSpring = joint.useSpring;
        newJoint.spring = joint.spring;
        newJoint.useMotor = joint.useMotor;
        newJoint.motor = joint.motor;

        Destroy(joint);
        return newJoint;
    }

    public void DestroyBearing()
    {
        if (StartConnection != null)
            StartConnection.Connections.Bearings.Remove(this);

        if (EndConnection != null)
            EndConnection.Connections.Bearings.Remove(this);

        if (joint != null) Destroy(joint);
        Destroy(gameObject);
    }
}