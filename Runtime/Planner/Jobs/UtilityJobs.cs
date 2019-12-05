using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.AI.Planner.Jobs
{
    [BurstCompile]
    struct QueueToListJob<T> : IJob
        where T : struct
    {
        public NativeQueue<T> InputQueue;
        [WriteOnly] public NativeList<T> OutputList;

        public void Execute()
        {
            while (InputQueue.TryDequeue(out T item))
            {
                OutputList.Add(item);
            }
        }
    }
}
