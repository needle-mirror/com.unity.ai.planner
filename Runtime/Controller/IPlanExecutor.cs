using System;
using System.Collections.Generic;
using Unity.AI.Planner;
using Unity.AI.Planner.DomainLanguage.TraitBased;
using UnityEngine.AI.Planner.Controller;

namespace UnityEngine.AI.Planner.DomainLanguage.TraitBased
{
    interface IPlanExecutor
    {
        bool IsIdle { get; }

        /// <summary>
        /// The action key of the current action in the plan.
        /// </summary>
        IStateKey CurrentStateKey { get; }

        /// <summary>
        /// The action key of the current action in the plan.
        /// </summary>
        IActionKey CurrentActionKey { get; }

        /// <summary>
        /// The scheduler for the planning jobs.
        /// </summary>
        IPlannerScheduler PlannerScheduler { get; }

        /// <summary>
        /// The plan to be executed.
        /// </summary>
        IPlan Plan { get; }

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
        /// Progresses the state of the plan.
        /// </summary>
        void AdvancePlanWithPredictedState();

        /// <summary>
        /// Updates the planning systems with new object data.
        /// </summary>
        /// <param name="traitBasedObjects">The set of objects used to create a planning state.</param>
        void AdvancePlanWithNewState(IEnumerable<ITraitBasedObjectData> traitBasedObjects);

        /// <summary>
        /// Returns the state data for the current state of the plan.
        /// </summary>
        /// <param name="readWrite">Setting for whether or not the state data should be readwrite or readonly.</param>
        /// <returns>Returns the state data for the current state of the plan.</returns>
        IStateData GetCurrentStateData(bool readWrite = false);

        /// <summary>
        /// Returns the name of an action given the action key.
        /// </summary>
        /// <param name="actionKey">The key of the action.</param>
        /// <returns>Returns the name of an action given the action key.</returns>
        string GetActionName(IActionKey actionKey);

        /// <summary>
        /// Returns state data string for a given state key.
        /// </summary>
        /// <param name="stateKey">The key for the state.</param>
        /// <returns>Returns the state data string for a given state key.</returns>
        string GetStateString(IStateKey stateKey);

        /// <summary>
        /// Returns the longest path (in actions taken) within the plan graph starting from the current state.
        /// </summary>
        /// <returns>Returns the longest path (in actions taken) within the plan graph starting from the current state.</returns>
        int MaxPlanDepthFromCurrentState();
    }
}
