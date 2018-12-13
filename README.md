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

Or under Windows you may open the solution file with VS2017 and immediately build the entire solution after bootstrap.sh has been run.



Runtime Notes:

Use nuget to add the reference to ```signalwire-dotnet``` project, found here: https://www.nuget.org/packages/SignalWire-DotNet/

Calling ```TwilioClient.Init``` with your projectid and token for the username and password and then call:

```TwilioClient.SetDomain("<yourdomain>");```

Where ```<yourdomain>``` is where your dashboard can be found on SignalWire, IE: ```http://<yourdomain>.signalwire.com```

Example:
```
TwilioClient.Init("<projectid>", "<token>");
TwilioClient.SetDomain("<domain>");
```
