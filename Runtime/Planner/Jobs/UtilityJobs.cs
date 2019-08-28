using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Unity.AI.Planner.Jobs
{
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
