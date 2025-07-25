using Unity.Entities;
using Unity.Mathematics;

public struct BlockConnectionData : IComponentData
{
    public int BlockId;
    public float3 Position;
}
