# Changelog
## [1.1.0] - 2017-06-20
### Added
- Added deterministic mode. Use CreateEventWriter<T>(forEachCount). Must prefix writes with stream.BeginForEachIndex(index) just like NativeStream (there is no EndForEachIndex though).
- Added a persistent allocator option which is useful for when you are using worlds with different update rates to avoid leaks.

### Changed
- Updated stress test sample. New options to tweak the stress test. Will now only run if you load the scene.
- Writer now needs to be passed by ref to other methods. There is a safety check on here in case you forget.

### Fixed
- Multi world eventsystems actually releasing deferred streams properly now.
- Disabled domain reloading issue with multi event systems.

## [1.0.1] - 2020-06-16
### Changed
- Changed generic restriction to unmanaged instead of struct.
- Updated dependencies

## [1.0.0] - 2020-05-28
- Initial release