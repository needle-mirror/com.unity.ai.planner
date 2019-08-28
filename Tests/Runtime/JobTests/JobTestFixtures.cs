using System;
using KeyDomain;
using Unity.AI.Planner.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;


namespace Unity.AI.Planner.Tests
{
    struct TestStateDataContext : IStateDataContext<int, int>
    {
        public int CreateStateData() => default;

        public int GetStateData(int stateKey) => stateKey;

        public int GetStateDataKey(int stateData) => stateData;

        public int CopyStateData(int stateData) => stateData;

        public int RegisterState(int stateData) => stateData;

        public void DestroyState(int stateKey) { }

        public bool Equals(int x, int y) => x == y;

        public int GetHashCode(int stateData) => stateData;
    }

    struct TestStateManager : IStateManager<int, int, TestStateDataContext>
    {
        public TestStateDataContext GetStateDataContext() => new TestStateDataContext();

        public int GetStateData(int stateKey, bool readWrite) => GetStateData(stateKey);

        public int CreateStateData() => default;

        public int CopyStateData(int stateData) => stateData;

        public int CopyState(int stateKey) => stateKey;

        public int GetStateDataKey(int stateData) => stateData;

        public int GetStateData(int stateKey) => stateKey;

        public void DestroyState(int stateKey) { }

        public bool Equals(int x, int y) => x == y;

        public int GetHashCode(int stateData) => stateData;
    }

    class ActionScheduler : IActionScheduler<int, int, TestStateDataContext, TestStateManager, int, ActionResult>
    {
        // Input
        public NativeList<int> UnexpandedStates { get; set; }
        public TestStateManager StateManager { get; set; }

        // Output
        public NativeQueue<(int, int, ActionResult, int)> CreatedStateInfo { get; set; }

        struct Add : IJobParallelForDefer
        {
            public TestStateDataContext StateDataContext { get; set; }

            [field:ReadOnly] public NativeArray<int> UnexpandedStates { get; set; }
            [field:NativeDisableContainerSafetyRestriction] public NativeQueue<(int, int, ActionResult, int)>.ParallelWriter CreatedStateInfo { get; set; }

            public int ValueToAdd;

            public void Execute(int index)
            {
                // Read data from input
                var stateKey = UnexpandedStates[index];
                var stateData = StateDataContext.GetStateData(stateKey);

                // Make modifications to copy of state
                var newStateData = StateDataContext.CopyStateData(stateData);
                newStateData += ValueToAdd;
                var newStateKey = StateDataContext.RegisterState(newStateData);

                var reward = ValueToAdd;

                // Register action. Output transition info (state, action, result, resulting state key).
                CreatedStateInfo.Enqueue((stateKey, ValueToAdd, new ActionResult{ Probability = 1f, TransitionUtilityValue = reward }, newStateKey));
            }
        }

        public JobHandle Schedule(JobHandle inputDeps)
        {
            var createdStateInfoConcurrent = CreatedStateInfo.AsParallelWriter();

            var addOneHandle = new Add()
            {
                StateDataContext = StateManager.GetStateDataContext(),
                UnexpandedStates = UnexpandedStates.AsDeferredJobArray(),
                CreatedStateInfo = createdStateInfoConcurrent,
                ValueToAdd = 1,
            }.Schedule(UnexpandedStates, 0, inputDeps);

            var addTwoHandle = new Add()
            {
                StateDataContext = StateManager.GetStateDataContext(),
                UnexpandedStates = UnexpandedStates.AsDeferredJobArray(),
                CreatedStateInfo = createdStateInfoConcurrent,
                ValueToAdd = 2,
            }.Schedule(UnexpandedStates, 0, inputDeps);

            var addThreeHandle = new Add()
            {
                StateDataContext = StateManager.GetStateDataContext(),
                UnexpandedStates = UnexpandedStates.AsDeferredJobArray(),
                CreatedStateInfo = createdStateInfoConcurrent,
                ValueToAdd = 3,
            }.Schedule(UnexpandedStates, 0, inputDeps);

            return JobHandle.CombineDependencies(addOneHandle, addTwoHandle, addThreeHandle);
        }

        public void Run()
        {
            var createdStateInfoConcurrent = CreatedStateInfo.AsParallelWriter();

            var addOneHandle = new Add()
            {
                StateDataContext = StateManager.GetStateDataContext(),
                UnexpandedStates = UnexpandedStates.AsDeferredJobArray(),
                CreatedStateInfo = createdStateInfoConcurrent,
                ValueToAdd = 1,
            }.Schedule(UnexpandedStates, UnexpandedStates.Length);
            addOneHandle.Complete();

            var addTwoHandle = new Add()
            {
                StateDataContext = StateManager.GetStateDataContext(),
                UnexpandedStates = UnexpandedStates.AsDeferredJobArray(),
                CreatedStateInfo = createdStateInfoConcurrent,
                ValueToAdd = 2,
            }.Schedule(UnexpandedStates, UnexpandedStates.Length);
            addTwoHandle.Complete();

            var addThreeHandle = new Add()
            {
                StateDataContext = StateManager.GetStateDataContext(),
                UnexpandedStates = UnexpandedStates.AsDeferredJobArray(),
                CreatedStateInfo = createdStateInfoConcurrent,
                ValueToAdd = 3,
            }.Schedule(UnexpandedStates, UnexpandedStates.Length);
            addThreeHandle.Complete();
        }
    }

    struct CountToHeuristic : IHeuristic<int>
    {
        public int Goal;

        public float Evaluate(int stateData) => stateData >= Goal ? 0 : Goal - stateData;
    }

    struct CountToTerminationEvaluator : ITerminationEvaluator<int>
    {
        public int Goal;

        public bool IsTerminal(int stateData) => stateData >= Goal;
    }

    struct DefaultHeuristic : IHeuristic<int>
    {
        public float Evaluate(int stateData) => 0f;
    }

    struct DefaultTerminalStateEvaluator : ITerminationEvaluator<int>
    {
        public bool IsTerminal(int stateData) => false;
    }

}
