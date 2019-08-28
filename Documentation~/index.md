# About AI Planner

Use the AI Planner package to create agents that generate and execute plans. For example, use AI Planner to create an NPC, generate storylines, or validate game/simulation mechanics. The AI Planner package also includes authoring tools and a plan visualizer.


# Installing AI Planner

To install this package, follow the instructions in the [Package Manager documentation](https://docs.unity3d.com/Packages/com.unity.package-manager-ui@latest/index.html). 


# Using AI Planner
To create an AI agent with the AI Planner, see the following guides:
* [Defining the domain](DomainDefinition.md)
* [Defining actions](ActionDefinition.md)
* [Defining termination conditions](TerminationDefinition.md)
* [Configuring the scene](ConfigureScene.md)
* [Creating an AI agent](AgentDefinition.md)
* [Executing plans through operational actions](OperationalActions.md)

During execution, it is also useful to view an agent's plan through the [plan visualizer](PlanVisualizer.md).

For a complete sample project, see: [Otto](https://github.com/Unity-Technologies/otto) 


## Talks
### Unite LA 2018 - _AI for Behavior: Advanced Research for Intelligent Decision Making_
[![Unite LA 2018](images/UniteLA.png)](https://www.youtube.com/watch?v=ZdN8dDa0ff4)

### Unite Austin 2017 - _Unity Labs Behavioral AI Research_
[![Unite Austin 2017](images/UniteAustin.png)](https://www.youtube.com/watch?v=78nhJNPS0vA)


# Technical details
## Requirements

This version of AI Planner is compatible with the following versions of the Unity Editor:
* 2019.2  


## Package contents

The following table indicates the runtime folders that will be of interest to you as a developer:

|Location|Description|
|---|---|
|`Runtime/Agent`|Contains classes and interfaces for the agent and operational actions.|
|`Runtime/Planner`|Contains the planning system.|
|`Runtime/Serialization`|Contains serialized data definitions.|
|`Runtime/TraitBasedLanguage`|Contains state and action representations used by the planner.|
|`Runtime/Utility`|Contains utility classes for the package.|
|`Runtime/World`|Contains utility classes for monitoring game objects.|

## Known issues
* Prolonged usage of the planner can cause large allocations of native memory. 

## Document revision history
 
|Date|Reason|
|---|---|
|Aug 28, 2019|Document updated. Matches preview package version 0.1.0.|
|Mar 18, 2019|Document created. Matches preview package version 0.0.1.|