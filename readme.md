# mc-get

A console application to download and install mods and modpacks for Minecraft or a Minecraft server

> [!NOTE]
> This readme is for the development version, it may differ from the latest release. You can view the readme for the latest release [here](../v0.3.1/readme.md).

> [!NOTE]
> Breaking change from 0.3.x: mc-get no longer installs modpacks in the .minecraft folder directly! It now supports managing multiple concurrent Minecraft installations.

## Modding Platform Support

Supports downloading from the two major modding platforms [Modrinth](https://modrinth.com/) and [CurseForge](https://www.curseforge.com/)!\
🚧 CurseForge is WIP and not supported in any release yet. In order to test it in main branch, you need to set the urls in [CurseForge.cs](MCGet/Platforms/CurseForge.cs) and add api key handling yourself 🚧


## Usage

    Usage:
      mc-get (flags) <archivepath>
      mc-get (flags) <command> (parameters)
    
    Flags:
      -h, --help                :  displays this help page
      -s, --silent              :  performs a silent install. No user input needed
      -p, --platform <platform> :  installs from specified platform
                                   either modrinth (mr) or curseforge (cf)
      -m, --mc-path <path>      :  specifies minecraft installation path
      --path <path>             :  specifies the target installation path
                                   can also be used as a filter in other commands
      --mc-version <version>    :  specifies the minecraft version
      --server                  :  installs mod / modpack as server
      --version                 :  displays the current version
    
    Commands:
      install <slug | id | name>:<mod(pack)version>:<modloader>
        installs a mod / modpack
    
      search <query>
        searches for modrinth/curseforge projects
    
      list installs
        lists all installed modpacks
      list mods <search>
        lists all custom mods in installation
        that fit the search term (either slug or id)
    
      remove installation <search>
        removes an installation that fits the search term (either slug or id)
        --path can also be used as a filter
      remove mod <installation> <mod>
        removes a mod from an installation
        both <installation> and <mod> are search terms (either slug or id)
        --path can also be used as a filter

    Examples:
      mc-get install sodium:0.6.6:fabric
      mc-get --mc-version 1.19.3 install fabulously-optimized
      mc-get install fabulously-optimized
      mc-get -s install fabulously-optimized
      mc-get Fabulously.Optimized-4.10.5.mrpack
      mc-get list mods
      mc-get list mods fabulously-optimized
      mc-get remove installation 123
      mc-get remove installation fabulously-optimized
      mc-get remove mod fabulously-optimized sodium

## OS Compatibility

 - Windows: Working (last tested: v0.3.1)
 - Linux: Working (last tested: v0.3.0)
 - MacOS: Working (last tested: v0.2.2)

## Supported Modloaders

 - Forge
 - NeoForge
 - Fabric
 - Quilt

## Building

Requires the [.Net 8 Sdk](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
> [!NOTE]
> Can be built with .net 6, but some features may not be available

Run following command to generate an executable:

    dotnet build

For more informations about building .Net applications visit [this article](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build)