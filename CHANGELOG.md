# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

<!-- 
Notes about changelog:
* It's okay to have a section at the top to capture all changes until the next release. This will pass package validation.
## [Unreleased] 
### Added
### Changed
### Deprecated
### Removed
### Fixed
### Security

* It's okay to only have the preview version without the .n at the end and to continue updating the release notes until final release.
[X.Y.Z-preview] - date
or
[X.Y.Z] - date
 -->
 
## [0.2.2-preview] - 2020-01-16
### Fixed
* Fix filepath concatenation when generating code. 
* Fix loss of existing player loop when initializing the plan executor. 
 
## [0.2.1-preview] - 2019-12-20
### Added
* IsIdle to `IDecisionController` to reflect when the controller is not planning and not executing actions.
* UpdateStateWithWorldQuery to `IDecisionController` to allow updating the planner outside of action complete, such as where the plan is complete.
* BoundedValue validity checks (e.g. NaN, infinity) for when custom heuristics go awry.
* 32-bit integer field support for traits.
* Play back the EntityCommandBuffer (for state changes) on a separate thread instead of the main thread.
* Documentation for custom traits.
* Planner setting for scheduling multiple iterations of search at once.
* Action jobs, state evaluation jobs, and graph expansion jobs are now Burst-compatible.

### Changed
* Code now generates to the Packages directory as source.
* Expose discount factor in the PlanDefinition settings.
* Planner search and execution settings stay open when switching to play mode.
* Clean up public API by hiding unnecessary interfaces, structs, and fields.
* No longer block on previously scheduled planner iteration in the frame and simply wait until the job chain is complete.

### Removed
* Moveable.cs -- trait no longer uses `CustomTrait`, but instead gets generated from its `TraitDefinition`.

### Fixed
* Custom heuristic was not always getting picked up during code generation, so now the fully qualified name is serialized.
* Compilation upon switching to play mode is more reliable now. 
* Null reference exceptions when adding a `TraitComponent` initially with no traits added.
* Exception when dragging a GameObject with a missing component to an action callback of the `DecisionController`.
* Comparison of custom traits during state equality. 
* Improved performance while the plan executor waits for plan execution criteria.

## [0.2.0-preview] - 2019-12-02
### Added
* Planner search and execution settings under more options in `DecisionController` inspector.
* `ICustomTraitReward` for custom reward modifiers based on specific traits.
* Navigation module w/ traits, custom reward modifier, and simple navigation script.
* Help button in the inspector now links to documentation.
* AI Planner user preferences (Edit -> Preferences...).
* `TraitGizmo` for drawing gizmos on GameObjects with a given trait.
* Bounded heuristics for better search.
* Parallel selection and backpropagation algorithms.

### Changed
* Improve UI with re-orderable lists, better operand validation, and plan visualizer inspector.
* Improve workflow through `DecisionController`, `TraitComponent`, UI, callbacks, and world queries.
* Improve code compilation.
* Rename all mentions of DomainObject to TraitBasedObject.

### Removed
* `AgentDefinition`, since `PlanDefinition` supersedes it.
* `DomainObjectProvider`, since `TraitComponent` supersedes it.
* `BaseAgent`, since it is no longer necessary to create an Agent class.
* `IOperationalAction`, since `DecisionController` now handles this implicitly.

### Fixed
* Update template to avoid stack overflow in state data comparison.
* Null reference exceptions on adding a `TraitComponent` to a GameObject.
* Trait field renames.
* Endless recompilation.

## [0.1.1-preview] - 2019-08-29
### Fixed
* Fix incorrect state information displayed in the plan visualizer.

## [0.1.0-preview] - 2019-08-28
### Added
* Add support for distributed initial state data on GameObjects in scene.
* Auto-building of domain assemblies now occurs before entering play mode.

### Changed
* Update package dependency for entities: 0.1.1-preview. 
* Move search computation into jobs.
* Reduce the quantity of entities generated.
* Improve handling of cycles in plans, reducing plan memory footprint.
* Move serialized domain specification data into assets.
* Improve inspectors for domain specification.
* Update code generation to use Scriban.
* Code generation now emits code into a separate assembly.
* Reduce managed memory allocations.

### Removed
* Remove trait aliases.

### Known Issues
* Prolonged usage of the planner can cause large allocations of native memory. 

## [0.0.1-preview.7] - 2019-07-16
### Changed
* Update Yamato/CI configuration.

## [0.0.1-preview.6] - 2019-07-16
### Fixed
* Allow assignment of domain definition to plan definition in inspector.

## [0.0.1-preview.5] - 2019-07-11
### Changed
* Update dependency for entities package to preview.30 - 0.0.12.
* Plan definitions are no longer editable from the inspector.

### Fixed
* Correct errors from attempting to read trait properties as properties instead of fields. 

## [0.0.1-preview.4] - 2019-04-01
### Removed
* Property-based code generated for traits.

### Changed
* Update README.md for the package.

## [0.0.1-preview.3] - 2019-03-29
### Fixed
* Correct null reference errors from reading trait mask fields from custom trait classes during domain code generation.
* Fix index out of bounds errors during state equality due to missing domain object matches.

### Changed
* Update to latest entities package (preview.29 - 0.0.12).
* Remove deprecated ECS method usages.

## [0.0.1-preview.2] - 2019-03-18
### Added
* The first (preview) release of the *AI Planner*.
