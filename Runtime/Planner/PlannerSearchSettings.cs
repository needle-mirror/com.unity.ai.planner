using System;
using Unity.AI.Planner.Jobs;

namespace UnityEngine.AI.Planner
{
    [Serializable]
    class PlannerSearchSettings
    {
        [Tooltip("The number of states to expand at each search iteration.")]
        [SerializeField]
        public int StateExpansionBudgetPerIteration = 1;

        [Tooltip("Enables the delay of search iterations by a fixed number of frames.")]
        [SerializeField]
        public bool UseCustomSearchFrequency = true;

        [Tooltip("The number of frames to delay between each search iteration.")]
        [SerializeField]
        public int FramesPerSearchIteration;

        [Tooltip("Enables setting a maximum plan size.")]
        [SerializeField]
        public bool CapPlanSize;

        [Tooltip("The maximum number of states in the plan.")]
        [SerializeField]
        public int MaxStatesInPlan;

        [Tooltip("Enables setting a tolerance for the next immediate decision of the plan.")]
        [SerializeField]
        public bool StopPlanningWhenToleranceAchieved;

        [Tooltip("The maximum tolerance required before the search process ceases.")]
        [SerializeField]
        public float RootPolicyValueTolerance;

        [Tooltip("Specifies the job type to run for selecting states to expand. [Sequential or Parallel]")]
        [SerializeField]
        public SelectionJobMode GraphSelectionJobMode = SelectionJobMode.Sequential;

        [Tooltip("Specifies the job type to run for updating state values in the plan. [Sequential or Parallel]")]
        [SerializeField]
        public BackpropagationJobMode GraphBackpropagationJobMode = BackpropagationJobMode.Sequential;
    }
}
