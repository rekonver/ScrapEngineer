using UnityEngine;

[CreateAssetMenu(menuName = "Damper Configuration", fileName = "NewDamperConfig")]
public class DamperConfig : ScriptableObject
{
    [Header("Block Prefabs")]
    public GameObject endBlockPrefab;

    [Header("Damper Settings")]
    [Tooltip("Default length of the damper")]
    public float defaultLength = 3f;

    [Header("Spring Joint Settings")]
    public float springForce = 100f;
    public float damper = 5f;
    public float minDistanceMultiplier = 0.1f;
    public float maxDistanceMultiplier = 1f;
    public float tolerance = 0.1f;

    [Header("Visual Settings")]
    public Material damperMaterial;
    public float cylinderDiameter = 0.2f;

    [Header("Stability Settings")]
    public JointProjectionMode projectionMode = JointProjectionMode.PositionAndRotation;
    public float projectionDistance = 0f;
    public float projectionAngle = 0f;
    public bool configuredInWorldSpace = false;
    public bool swapBodies = false;
    public float breakForce = Mathf.Infinity;
    public float breakTorque = Mathf.Infinity;
}