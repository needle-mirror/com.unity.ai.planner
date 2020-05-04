# Configuring the scene

Once you've defined your domain, you can configure your scene by adding traits via `TraitComponent` to individual GameObjects and setting up your AI agent with a `DecisionController`.

## Trait Component

Every plan begins with an initial or _root_ state, describing the circumstances under which an agent plans its future course of action. To initialize this root state, GameObjects that you may want to include in your planning state need to be marked appropriately with traits.

You can add a `TraitComponent` to a GameObject to specify traits for that object.

![Image](images/TraitComponent.png)

A `TraitComponent` has a:
* Name - The name of the object (defaults to GameObject name)
* Traits - A list of selected traits that are possessed by the object
* Trait properties - Optional initial values for properties (otherwise default values are used)

## Decision Controller

After setting up GameObjects in the scene with traits you can add a `DecisionController` to your AI agent GameObject.

![Image](images/DecisionController.png)

In order to make and execute plans, a `DecisionController` requires a [PlanDefinition](PlanDefinition.md), which defines the planning problem to be solved. Once assigned, the `DecisionController` will display:
* The assigned `PlanDefinition`
* Settings for initialization and automatic updating (see [DecisionController](xref:UnityEngine.AI.Planner.Controller.DecisionController))
* Callback assignments for enacting the actions included in the `PlanDefinition`
* A world query for including trait-based objects in the planning state

Advanced options for controlling the planner search and execution are also available by clicking the cog icon on the `DecisionController` (shown below). For tips on improving planner performance, see [Improving Performance](PlannerPerformanceTips.md).

![Image](images/AdvancedSettings.png)



### Action Callbacks

Actions authored as `ActionDefinitions` represent models of the actions an agent may perform, as used by the planning process to simulate possible futures. Such definitions specify at a high level the constraints of using the action (preconditions) as well as the expected outcomes (effects). As such, they omit many of the practical considerations of performing an action in a game. For example, an action requiring an agent to move to another location may need to set a destination on a NavMeshAgent and begin following a given path. Similarly, you may wish to trigger various animations that visually exhibit an action being performed. 

In order for an object with a `DecisionController` to interact with your scene through enacting its plan(s), you must provide action callbacks for each action contained in the controller's plan definition. At each instance when the controller decides to act, the corresponding action callback will be invoked. Notably, this only occurs at the beginning of each action. It will be left to the user to set up the appropriate code to perform the action in the scene, e.g. triggering animations, changing trait data on objects, etc. Furthermore, consider that while an `ActionDefinition` specifies what is *expected* to occur, in many applications, the true outcomes may be substantially different, particularly if the action is blocked, delayed, or otherwise interacted with by a player or by another game system. **The effects from actions defined in `ActionDefinitions` are not automatically applied to the scene. It is up to the user to ensure these effects occur.** 

Action callbacks can take either of two forms:

- Coroutine - The controller will initiate the coroutine and wait until its completion before updating its current state and proceeding with plan execution. This is the recommended choice for actions that execute over multiple frames.
- Non-coroutine - The controller will invoke the callback, then immediately update its current state and proceed with plan execution. 

Furthermore, you can specify arguments for the callbacks to access data from the action's parameters. For example, in the image below, we have set up the callback for the action Navigate. First, the object holding the appropriate script is assigned. Then, you can select which method to invoke from the available methods on the game object. If the method has any `GameObject` or trait parameters, you must assign the appropriate fields from the parameters of the corresponding `ActionDefinition`. Here, the `NavigateTo` method accepts a target `GameObject` to navigate toward; for this object, we specify the `Destination` parameter from the action definition. **Note: Trait arguments are value types and should be treated as *readonly* data. If you desire to modify trait data on an object, you must assign the modified trait on the relevant object's `TraitComponent`.** 

![Example Action Callback](images/NavigateCallback.png)

As plans contain predictions of various futures that may not occur as predicted, the controller  tracks the progress of the execution of its current plan. After every action, the `DecisionController` updates its current state, which designates where in the plan it is currently.  You can specify how this update should occur, either:

- Use World State - Queries the world for current objects and trait data. This is the recommended setting, particularly if your application involves potential changes or interactions not predicted by the modeled action effects. 
- Use Next Plan State - Uses the predicted state from the plan, which disregards any unexpected changes to the live scene data. While it is the more performant option, this setting is only recommended if the modeled action's predictions are accurate or if inaccuracies should be ignored. 



### Querying the World

For most applications, agents will query the world for state changes. This process pulls in data from all of the `GameObjects` with `TraitComponents`, forming a snapshot of the current state of the world. By default, all objects will be included in the query. However, this is not always desirable, as the number of objects can be very large, which can severely reduce performance. A better practice is to limit which objects are included in the world query to just those the agent will  potentially use in its plans. 

![Example World Query](images/WorldQuery.png)

Pictured above is an example of a world query composed of clauses specifying which objects should be included. **Note: the interpreted order is a disjunction (OR) over conjunctions (AND).** In this example, the interpretation is (`in radius: 5` AND `With Traits: Dirt` ) OR (`With Traits: Robot` AND `Without Traits: Hidden` ) OR (`From GameObject: Sofa`), meaning the query will pick up dirt objects within 5 meters, robots that are not hidden, and the sofa. 

