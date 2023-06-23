# mc-get

A console application to download and install mods and modpacks for Minecraft

## Modding Platform Support

At the moment only [Modrinth](https://modrinth.com/) is supported. [Curseforge](https://www.curseforge.com/) archives are working in theory, but due to their api licensing I can't publish it at the moment.

## Usage

    Usage: 
      mc-get (flags) <archivepath>
      mc-get (flags) install (<slug> | <id> | <name>):<mcversion>:<modloader>

    Flags:
      -h / --help         :  displays this help page
      -r                  :  deletes modpack and restores old state
      -s                  :  performs a silent install. No user input needed
      -f / --fix-missing  :  retries to download failed mods
      -m <path>           :  specifies minecraft installation path
    
    Examples:
      mc-get install sodium:1.19.3:fabric
      mc-get install fabulously-optimized      
      mc-get -s install fabulously-optimized
      mc-get Fabulously.Optimized-4.10.5.mrpack

      mc-get -r

## OS Support

 - Windows: Working
 - Linux: Working
 - MacOS: Not Tested

## Building

Requires the [.Net 6 Sdk](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)

Run following command to generate an executable:

    dotnet build

For more informations about building .Net applications visit [this article](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build)