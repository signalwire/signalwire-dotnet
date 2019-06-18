# SignalWire C#/.Net SDK

[![Build Status](https://ci.signalwire.com/api/badges/signalwire/signalwire-dotnet/status.svg)](https://ci.signalwire.com/signalwire/signalwire-dotnet) [![NuGet version (SignalWire-DotNet)](https://img.shields.io/nuget/v/SignalWire-DotNet.svg?color=brightgreen)](https://www.nuget.org/packages/SignalWire-DotNet/)

This library provides access to SignalWire APIs which allow you to do things like placing or receiving audio calls in .NET platform languages. For a full reference of capabilities, check out the [documentation](https://docs.signalwire.com/topics/relay-sdk-dotnet).

## Getting Started

All of the documentation material can be found at the official [Relay SDK for C#/.Net Documentation](https://docs.signalwire.com/topics/relay-sdk-dotnet) site.

---

## Contributing

Relay SDK for C#/.Net is open source and maintained by the SignalWire team, but we are very grateful for everyone who has contributed and assisted so far.

If you'd like to contribute, feel free to visit our [Slack channel](https://signalwire.community/) and get set up.

## Developers

The SDK can be found at the [signalwire-dotnet](https://github.com/signalwire/signalwire-dotnet) repo. To set up the dev environment, follow these steps:

1. Prerequisites: Git and .NET Core SDK
 * Install Git for your system, for Windows install msysgit if not already installed.
 * Install .NET Core for your system, see https://www.microsoft.com/net/download for more information.

2. To build from source, the `bootstrap.sh` script must be run before anything else.
 * For *nix, run the following from your shell: `./bootstrap.sh`
 * For Windows, run the git-bash and run the same above script

3. After bootstrapping, you do not need to run `bootstrap.sh` again unless there are changes to the LaML source library.

4. Building can then be performed by running the following cross-platform 'dotnet' toolchain commands: `dotnet build`

5. To create a complete set of files including dependencies, you can run the following: `dotnet publish`

6. To create a nuget package with a complete set of files including dependencies, you can run the following: `dotnet pack`

7. Under Windows you may open the solution file with VS2017 and immediately build the entire solution after `bootstrap.sh` has been run.

## Versioning

Relay SDK for C#/.Net follows Semantic Versioning 2.0 as defined at <http://semver.org>.

## License

Copyright (c) 2019 [SignalWire](http://signalwire.com). It is free software, and may be redistributed under the terms specified in the [MIT-LICENSE](https://github.com/signalwire/signalwire-dotnet/blob/master/LICENSE) file.

