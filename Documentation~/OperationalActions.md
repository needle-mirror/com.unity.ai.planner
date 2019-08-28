## Operational Actions

While the Agent Definition contains specifications for plans an agent makes, operational actions govern the execution of the plan's actions within the game/simulation. These are what control your character, trigger animations, or otherwise interact with your game world. 

All operational action scripts must implement the [`IOperationalAction<TAgent, TStateData, TAction>`](xref:Unity.AI.Planner.Agent.IOperationalAction`1) interface, which requires methods for beginning, continuing, ending, and monitoring the status of the actions. The types for this interface are typically StateData, ActionKey, and YourAgentClass. StateData is generated for you when you build your assembly. ActionKey is provided by default with the package code. Finally, YourAgentClass is the class you've implemented which inherits from BaseAgent.

Each required method of the interface is defined over the following arguments:
* State Data - A struct containing the data for a given state. 
* Action - A struct representing the action to be taken as well as the parameters of the action. 
* Agent - The agent which will perform the action. This is your agent class. 

Tip: Do not forget to assign your operational actions to the corresponding action assets you authored earlier. 

### Operational Action Status

```csharp
OperationalActionStatus Status(TStateData stateData, TActionKey action, TAgent agent)
```

Operational actions are responsible for reporting the status of the action to the [Controller](xref:Unity.AI.Planner.Agent.Controller`1). This method is called each frame until the action is completed or determined no longer valid. The possible values of the OperationalActionStatus are:
* InProgress 
* NoLongerValid
* Complete


### Begin Execution

```csharp
void BeginExecution(TStateData stateData, TAction action, TAgent agent)
```

BeginExecution is called once, at the start of each action.


### Continue Execution

```csharp
void ContinueExecution(TStateData stateData, TAction action, TAgent agent)
```

ContinueExecution is called each frame until the action is determined to be Complete or NoLongerValid.

## End Execution

```csharp
void EndExecution(TStateData stateData, TAction action, TAgent agent)
```

EndExecution is called once, after the action is reported Complete or NoLongerValid. 


