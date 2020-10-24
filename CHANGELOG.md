# Changelog
## [1.1.8] - 2020-10-25
### Added
- Added EnsureHashMapCapacity override for NativeMultiHashMaps
- Added stacktrace info to the consumer and producer safety to help pinpoint a missing AddHandlerFor

### Changed
- ReadLarge now has an optional Allocator if you want to use something other than the Temp allocator

## [1.1.7] - 2020-08-14
### Changed
- Updated requirements to Unity 2020.1 and Entities 0.14
- Tweaked testing

### Fixed
- Warnings as the result of the new burst 1.4 preview

## [1.1.6] - 2020-08-13
### Added
- Added AllocateLarge and ReadLarge extensions to NativeEventStream

## [1.1.5] - 2020-08-09
### Fixed
- Added support back for Unity 19.4 and 20.1

## [1.1.4] - 2020-08-06
### Added
- Added Schedule for IJobEvent for when you don't want parallel scheduling
- Added new extension, ToNativeList

## [1.1.3] - 2020-07-11
### Added
- New job, IJobEventReaderForEach. This is a parallel job that allows reading of a foreach index per thread

### Changed
- IJobEventStream renamed to IJobEventReader
- Made ScheduleSimultaneous internal

## [1.1.2] - 2020-07-10
### Added
- New extension, GetEventCount

### Changed
- UsePersistentAllocator is now an instance property

## [1.1.1] - 2020-07-09
### Fixed
- Allocation issue causing writes to be lost on rare occasions

## [1.1.0] - 2020-07-07
### Added
- Added deterministic mode. Use CreateEventWriter<T>(forEachCount). Must prefix writes with stream.BeginForEachIndex(index) just like NativeStream (there is no EndForEachIndex though).
- Added a persistent allocator option which is useful for when you are using worlds with different update rates to avoid leaks

### Changed
- NativeThreadStream renamed NativeEventStream
- Updated stress test sample. New options to tweak the stress test. Will now only run if you load the scene
- Writer now needs to be passed by ref to other methods. There is a safety check on here in case you forget

### Fixed
- Multi world eventsystems actually releasing deferred streams properly now
- Disabled domain reloading issue with multi event systems

## [1.0.1] - 2020-06-16
### Changed
- Changed generic restriction to unmanaged instead of struct
- Updated dependencies

## [1.0.0] - 2020-05-28
- Initial release