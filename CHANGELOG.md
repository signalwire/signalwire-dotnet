# Changelog
All notable changes to this project will be documented in this file.

This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.1.0] - 2019-07-29
### Added
- Added Task API
- Added Tap API
- Added Detect API
- Added Fax API
- Added Messaging API

## [2.0.0] - 2019-07-16
### Fixed
- Releasing 2.0.0

## [2.0.0-rc2] - 2019-07-15
### Fixed
- Moved SignalWire.Relay.Calling.Event to SignalWire.Relay.Event
- Adjusted Call.WaitFor to wait properly for state requested, Ended, or	client disconnected

## [2.0.0-rc1] - 2019-07-11
### Added
- Major rewrite on front facing API for the new 2.x Consumer model

## [1.4.2] - 2019-06-17
### Fixed
- Fixed host no longer requiring the path suffix
### Added
- Added a helper function FindProtocols to Cache

## [1.4.1] - 2019-05-08
### Fixed
- Fixed config detection throwing bad exception in TwilioClient.Init

## [1.4.0] - 2019-05-03
### Added
- Calling Record API

## [1.3.1]
- Fixed cleanup of some internal temporary callback assignments

## [1.3.0]
- Adding PlayMedia and PlayAndCollectMedia, and convenience helpers

## [1.2.1]
- Internal fix for ignoring certificate verification

## [1.2.0]
- Initial merge of SignalWire.Relay support

## [1.1.1]
- Updated to support full signalwire space
- Support ENV var for signalwire space
- Deprecated SetDomain in favor of using signalwire space

