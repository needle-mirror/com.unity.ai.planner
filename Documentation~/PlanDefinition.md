# Creating a plan definition

A plan definition is where your [domain definition](DomainDefinition.md) (i.e. traits and enumerations) comes together with your [actions](ActionDefinition.md) and [state terminations](TerminationDefinition.md). You assign a `PlanDefinition` to your `DecisionController` when you [set up your scene](ConfigureScene.md).

## Plan Definition
A plan definition holds the set of actions and a list of termination criteria that are used by the planner to generate a plan. Create a "Plan Definition" asset via the asset creation menu (Create -> AI -> Planner -> Plan Definition) or the Create menu from the project window.

![Image](images/CreatePlanDefinition.png)

Once the asset has been created you can assign or create new actions, assign or create new state termination criteria, and specify a heuristic. When you create actions, any built-in actions will show up under their own submenu (e.g. Navigation).

![Image](images/PlanDefinition.png)