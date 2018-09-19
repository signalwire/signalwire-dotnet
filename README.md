# signalwire-dotnet

Prerequisites: Git and .NET Core SDK

- Install Git for your system, for Windows install msysgit if not already installed.

- Install .NET Core for your system, see https://www.microsoft.com/net/download for more information.


To build from source, the bootstrap.sh script must be run before anything else.

For *nix, run the following from your shell:

```./bootstrap.sh```

For Windows, run the git-bash and run the same above script


After bootstrapping, you do not need to run the bootstrap.sh again unless there are changes to the LaML source library.


Building can then be achieved by running the following cross platform 'dotnet' toolchain commands:

```dotnet build```

To create a complete set of files required including dependancies you can run the following:

```dotnet publish```

To create a nuget package, with a complete set of files required including dependancies you can run the following:

```dotnet pack```




