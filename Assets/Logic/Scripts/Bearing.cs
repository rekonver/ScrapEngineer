using UnityEngine;
using UnityEngine.UIElements.Experimental;

public class Bearing : MonoBehaviour
{
    [SerializeField] private Block blockInfo;
    public Block StartConnection;
    public Block EndConnection;
    public HingeJoint Joint;
    public Quaternion customRotation = Quaternion.identity;
    private GameObject parentBlock;

    public void AddStartPoint(Block block)
    {
        StartConnection = block;
        CallBlockToAddBearing(block);
        parentBlock = block.ParentConnection;
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
        var endParentGroup = endBlock.ParentConnection;
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
        Joint = bearingJoint;
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
        if (Joint == null)
        {
            Debug.LogWarning("Немає оригінального joint для копіювання!");
            return null;
        }

        var newJoint = newParentGroup.AddComponent<HingeJoint>();
        newJoint.connectedBody = Joint.connectedBody;
        newJoint.autoConfigureConnectedAnchor = false;

        SetJointAnchors(newJoint, endBlock.transform.position, newParentGroup.transform, endBlock.ParentConnection.transform);
        SetRotationAxis(newJoint, startBlock.transform, endBlock.transform, newParentGroup.transform);

        newJoint.useLimits = Joint.useLimits;
        newJoint.limits = Joint.limits;
        newJoint.useSpring = Joint.useSpring;
        newJoint.spring = Joint.spring;
        newJoint.useMotor = Joint.useMotor;
        newJoint.motor = Joint.motor;

        Destroy(Joint);
        return newJoint;
    }

    public void DestroyBearing()
    {
        if (StartConnection != null)
        {
            StartConnection.Connections.Bearings.Remove(this);
            StartConnection.DeleteBlock();
        }

        if (EndConnection != null)
        {
            EndConnection.Connections.Bearings.Remove(this);
            EndConnection.DeleteBlock();
        }

        if (Joint != null) Destroy(Joint);
        Destroy(gameObject);
    }
}