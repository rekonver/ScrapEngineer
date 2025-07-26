using UnityEngine;

[CreateAssetMenu(menuName = "Damper Configuration", fileName = "NewDamperConfig")]
public class DamperConfig : ScriptableObject
{
    [Header("Block Prefabs")]
    public GameObject EndBlockPrefab;

    [Header("Damper Settings")]
    [Tooltip("Default length of the damper")]
    public float DefaultLength = 3f;

    [Header("Spring Joint Settings")]
    public float SpringForce = 100f;
    public float Damper = 5f;
    public float MinDistanceMultiplier = 0.1f;
    public float MaxDistanceMultiplier = 1f;
    public float Tolerance = 0.1f;

    [Header("Visual Settings")]
    public Material DamperMaterial;
    public float CylinderDiameter = 0.2f;

    [Header("Stability Settings")]
    public JointProjectionMode ProjectionMode = JointProjectionMode.PositionAndRotation;
    public float ProjectionDistance = 0f;
    public float ProjectionAngle = 0f;
    public bool ConfiguredInWorldSpace = false;
    public bool SwapBodies = false;
    public float BreakForce = Mathf.Infinity;
    public float BreakTorque = Mathf.Infinity;
}