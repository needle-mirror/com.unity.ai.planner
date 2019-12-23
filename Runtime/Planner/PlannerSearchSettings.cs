using System;
using Unity.AI.Planner.Jobs;

namespace UnityEngine.AI.Planner
{
    [Serializable]
    class PlannerSearchSettings
    {
        [Tooltip("The number of search iterations to be completed by each update.")]
        public int SearchIterationsPerUpdate = 1;

        [Tooltip("The number of states to expand within each search iteration.")]
        public int StateExpansionBudgetPerIteration = 1;

        [Tooltip("Enables the delay of search update by a fixed number of frames.")]
        [SerializeField]
        public bool UseCustomSearchFrequency = true;

        [Tooltip("The number of frames to delay between each search update.")]
        public int FramesPerSearchUpdate;

        [Tooltip("Enables setting a maximum plan size.")]
        public bool CapPlanSize;

        [Tooltip("The maximum number of states in the plan.")]
        public int MaxStatesInPlan;

        [Tooltip("Enables setting a tolerance for the next immediate decision of the plan.")]
        public bool StopPlanningWhenToleranceAchieved;

        [Tooltip("The maximum tolerance required before the search process ceases.")]
        public float RootPolicyValueTolerance;

        [Tooltip("Specifies the job type to run for selecting states to expand. [Sequential or Parallel]")]
        public SelectionJobMode GraphSelectionJobMode = SelectionJobMode.Sequential;

        [Tooltip("Specifies the job type to run for updating state values in the plan. [Sequential or Parallel]")]
        public BackpropagationJobMode GraphBackpropagationJobMode = BackpropagationJobMode.Sequential;
    }
}
