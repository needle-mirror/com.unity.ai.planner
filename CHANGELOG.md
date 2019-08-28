# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).


## [0.1.1-preview] - 2019-08-29
### Fixed
* Fix incorrect state information displayed in the plan visualizer.

## [0.1.0-preview] - 2019-08-28
### Added
* Add support for distributed initial state data on GameObjects in scene.
* Auto-building of domain assemblies now occurs before entering play mode.

### Changed
* Update package dependency for entities - 0.1.1-preview. 
* Move search computation into jobs.
* Reduce the quantity of entities generated.
* Improve handling of cycles in plans, reducing plan memory footprint.
* Move serialized domain specification data into assets.
* Improve inspectors for domain specification.
* Update code generation to use Scriban.
* Code generation now emits code into a separate assembly.
* Reduce managed memory allocations.

### Known Issues
* Prolonged usage of the planner can cause large allocations of native memory. 

### Removed
* Remove trait aliases.

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

## [0.0.1-preview.7] - 2019-07-16
### Changed
* Update Yamato/CI configuration.

## [0.0.1-preview.6] - 2019-07-16
### Fixed
* Allow assignment of domain definition to plan definition in inspector.

## [0.0.1-preview.5] - 2019-07-11
### Changed
* Updated dependency for entities package to preview.30 - 0.0.12.
* Plan definitions are no longer editable from the inspector.

### Fixed
* Corrected errors from attempting to read trait properties as properties instead of fields. 

## [0.0.1-preview.4] - 2019-04-01
### Removed
* Property-based code is no longer generated for traits.

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
