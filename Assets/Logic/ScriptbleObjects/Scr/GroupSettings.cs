using UnityEngine;

[CreateAssetMenu(fileName = "GroupSettings", menuName = "Custom/Group Settings", order = 1)]
public class GroupSettings : ScriptableObject
{
    [Header("Group Settings")]
    public GameObject groupPrefab;
}
