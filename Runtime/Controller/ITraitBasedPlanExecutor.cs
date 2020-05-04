using System;
using Unity.AI.Planner;
using Unity.AI.Planner.Controller;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    interface ITraitBasedPlanExecutor : IPlanExecutor, IDisposable
    {
        /// <summary>
        /// Initializes the plan executor.
        /// </summary>
        /// <param name="actor">A MonoBehaviour used to start and stop coroutines.</param>
        /// <param name="planDefinition">The plan definition used to specify the domain and actions used in the planning process.</param>
        /// <param name="actionExecutionInfos">Action execution information for the actions contained in the plan definition.</param>
        void Initialize(MonoBehaviour actor, PlanDefinition planDefinition, IActionExecutionInfo[] actionExecutionInfos);

        ITraitBasedStateConverter StateConverter { get; }
    }
}
