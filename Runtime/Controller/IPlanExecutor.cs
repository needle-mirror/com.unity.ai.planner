using System;
using System.Collections.Generic;
using Unity.AI.Planner;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using UnityEngine.AI.Planner.Controller;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    interface IPlanExecutor
    {
        /// <summary>
        /// Initializes the plan executor.
        /// </summary>
        /// <param name="name">The name of the object executing the plan.</param>
        /// <param name="planDefinition">The plan definition used to specify the domain and actions used in the planning process.</param>
        /// <param name="initialTraitBasedObjects">The set of initial objects used to create a planning state.</param>
        /// <param name="settings">Settings governing the execution of the plan.</param>
        void Initialize(string name, PlanDefinition planDefinition, IEnumerable<ITraitBasedObjectData> initialTraitBasedObjects, PlanExecutionSettings settings);

        /// <summary>
        /// Destroys the plan executor.
        /// </summary>
        void Destroy();

        /// <summary>
        /// Checks the criteria for performing the next action in the plan.
        /// </summary>
        /// <returns>Returns true if the criteria have been met to perform the next plan action. Returns false otherwise.</returns>
        bool ReadyToAct();

        /// <summary>
        /// Triggers the executor to begin the next action of the plan.
        /// </summary>
        /// <param name="controller">The component on the game object for which the action will be performed.</param>
        void Act(DecisionController controller);

        /// <summary>
        /// Updates the state of the executor and the root state of the plan.
        /// </summary>
        /// <param name="traitBasedObjects">The set of objects used to create a planning state.</param>
        void UpdateState(IEnumerable<ITraitBasedObjectData> traitBasedObjects);

        /// <summary>
        /// Progresses the state of the plan.
        /// </summary>
        void AdvancePlanWithPredictedState();

        /// <summary>
        /// Updates the planning systems with new object data.
        /// </summary>
        /// <param name="traitBasedObjects">The set of objects used to create a planning state.</param>
        void AdvancePlanWithNewState(IEnumerable<ITraitBasedObjectData> traitBasedObjects);

        /// <summary>
        /// The action key of the current action in the plan.
        /// </summary>
        IActionKey CurrentActionKey { get; }

        /// <summary>
        /// Returns the state data for the current state of the plan.
        /// </summary>
        /// <param name="readWrite">Setting for whether or not the state data should be readwrite or readonly.</param>
        /// <returns>Returns the state data for the current state of the plan.</returns>
        IStateData GetCurrentState(bool readWrite = false);

        /// <summary>
        /// The scheduler for the planning jobs.
        /// </summary>
        IPlannerScheduler PlannerScheduler { get; }
    }
}
