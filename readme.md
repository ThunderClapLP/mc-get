# mc-get

A console application to download and install mods and modpacks for Minecraft or a Minecraft server

> [!NOTE]
> This readme is for the development version, it may differ from the latest release. You can view the readme for the latest release [here](../v0.3.1/readme.md).

## Modding Platform Support

Supports downloading from the two major modding platforms [Modrinth](https://modrinth.com/) and [CurseForge](https://www.curseforge.com/)!\
🚧 CurseForge is WIP and not supported in any release yet. In order to test it in main branch, you need to set the urls in [CurseForge.cs](MCGet/Platforms/CurseForge.cs) and add api key handling yourself 🚧

> [!NOTE]
> Only supports installing mods in the .minecraft directory at the moment (But it is planned to add the ability to fully manage multiple modpacks and installations!). In the meantime, have a look at this [list of awesome projects](https://github.com/modrinth/awesome) and see if something there fits your needs.

## Usage

    Usage:
        mc-get (flags) <archivepath>
        mc-get (flags) <command> (parameters)

    Flags:
        -h / --help         :  displays this help page
        -s                  :  performs a silent install. No user input needed
        -f / --fix-missing  :  retries to download failed mods
        -mr / --modrinth    :  download from modrinth
        -cf / --curseforge  :  download from curseforge
        -m <path>           :  specifies minecraft installation path
        -mc <version>       :  specifies the minecraft version
        --server            :  installs mod / modpack as server
        -v / --version      :  displays the current version

    Commands:
        install (<slug> | <id> | <name>):<mod(pack)version>:<modloader>
            installs a mod / modpack

        search <query>
            searches for modrinth projects

        restore
            deletes modpack and restores old state

    Examples:
        mc-get install sodium:0.6.6:fabric
        mc-get -mc 1.19.3 install fabulously-optimized
        mc-get install fabulously-optimized
        mc-get -s install fabulously-optimized
        mc-get Fabulously.Optimized-4.10.5.mrpack
        mc-get restore

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