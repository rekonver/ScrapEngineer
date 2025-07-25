using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct FindConnectionsJob : IJob
{
    [ReadOnly] public NativeArray<int> BlockInstanceIDs;
    [ReadOnly] public NativeParallelMultiHashMap<int, int> ConnectionsMap;
    public NativeArray<int> ComponentLabels;

    public void Execute()
    {
        int n = BlockInstanceIDs.Length;
        for (int i = 0; i < n; i++)
        {
            int currentID = BlockInstanceIDs[i];
            int currentLabel = ComponentLabels[i];
            int minLabel = currentLabel;

            if (ConnectionsMap.TryGetFirstValue(currentID, out int connectionID, out var it))
            {
                do
                {
                    int connectionIndex = FindIndex(BlockInstanceIDs, connectionID);
                    if (connectionIndex != -1)
                    {
                        int connectionLabel = ComponentLabels[connectionIndex];
                        minLabel = math.min(minLabel, connectionLabel);
                    }
                }
                while (ConnectionsMap.TryGetNextValue(out connectionID, ref it));
            }

            if (minLabel < currentLabel)
            {
                ComponentLabels[i] = minLabel;
                PropagateLabel(minLabel, currentLabel);
            }
        }
    }

    private void PropagateLabel(int newLabel, int oldLabel)
    {
        for (int i = 0; i < ComponentLabels.Length; i++)
        {
            if (ComponentLabels[i] == oldLabel)
            {
                ComponentLabels[i] = newLabel;
            }
        }
    }

    private int FindIndex(NativeArray<int> array, int value)
    {
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i] == value) return i;
        }
        return -1;
    }
}