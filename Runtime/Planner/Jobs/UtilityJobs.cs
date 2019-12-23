using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
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

    [BurstCompile]
    struct DestroyStatesJob<TStateKey, TStateData, TStateDataContext> : IJob
        where TStateKey : struct, IEquatable<TStateKey>
        where TStateData : struct
        where TStateDataContext : struct, IStateDataContext<TStateKey, TStateData>
    {
        public NativeQueue<TStateKey> StatesToDestroy;
        public TStateDataContext StateDataContext;

        public void Execute()
        {
            while (StatesToDestroy.TryDequeue(out TStateKey state))
            {
                StateDataContext.DestroyState(state);
            }
        }
    }

    struct PlaybackSingleECBJob : IJob
    {
        public ExclusiveEntityTransaction ExclusiveEntityTransaction;
        public EntityCommandBuffer EntityCommandBuffer;

        public void Execute()
        {
            EntityCommandBuffer.Playback(ExclusiveEntityTransaction);
        }
    }
}
