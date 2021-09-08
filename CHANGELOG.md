# Changelog
All notable changes to this project will be documented in this file.

This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.4.0] - 2021-09-08
### Added
- Network data filtering
- Fuzzy reconnect delay
- Max duration support for calls
- Added Disconnect API
- Added Dial API

## [2.3.0] - 2019-10-22
### Added
- Agent in blade.connect
- Responding to blade.ping
- Support for external authorizations event passing
- Added Url to RecordAction
- Ringback added for call.connect
- Added ringtone as new media option for play and prompt
- Added play and prompt volume control and API's
- Added play pause and resume API's
- Added StopResult to all Stop API's

## [2.2.0] - 2019-09-09
### Deprecated
- Deprecated DetectMachine, DetectMachineAsync, DetectHuman, and DetectHumanAsync
### Added
- Added AMD, AMDAsync, DetectAnsweringMachine, and DetectAnsweringMachineAsync as replacements
- Added Send Digits API (DTMF)

## [2.1.2] - 2019-08-19
### Fixed
- Fixed websocket buffers to 64kb max to satisfy .NET Framework limits
### Added
- Constructors for Relay.Messaging.SendSource that accept both a body string and media URLs
- OnIncomingMessage and OnMessageStateChange events to Consumer

## [2.1.1] - 2019-08-02
### Fixed
- Fixed Call.WaitFor to not throw exception
### Removed
- MessageData from low level results

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

