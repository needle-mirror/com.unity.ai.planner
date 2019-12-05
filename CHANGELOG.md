# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [0.2.0-preview.4] - 2019-12-06
### Fixed
* Update template to avoid stack overflow in state data comparison.

## [0.2.0-preview.1] - 2019-12-06
### Fixed
* Null reference on adding a TraitComponent to a GameObject

## [0.2.0-preview] - 2019-12-02
###Added
* Planner search and execution settings under more options in `DecisionController` inspector
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
