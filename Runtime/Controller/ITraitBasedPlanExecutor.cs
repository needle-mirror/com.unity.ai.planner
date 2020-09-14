using System;
using Unity.AI.Planner.Controller;
using UnityEngine;

namespace Unity.AI.Planner.Traits
{
    interface ITraitBasedPlanExecutor : IPlanExecutor, IDisposable
    {
        /// <summary>
        /// Initializes the plan executor.
        /// </summary>
        /// <param name="actor">A MonoBehaviour used to start and stop coroutines.</param>
        /// <param name="problemDefinition">The problem definition used to specify the domain and actions used in the planning process.</param>
        /// <param name="actionExecutionInfos">Action execution information for the actions contained in the problem definition.</param>
        void Initialize(MonoBehaviour actor, ProblemDefinition problemDefinition, IActionExecutionInfo[] actionExecutionInfos);

        ITraitBasedStateConverter StateConverter { get; }
    }
}
