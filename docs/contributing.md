# Celeste Randomizer: Contributing Guide

## Building

### Requirements

* Visual Studio 14 or later
* Everest
* .NET 7.0 SDK

### Basic git

* Fork this repository
* `git clone https://github.com/<your_account>/CelesteRandomizer`
* `git remote add upstream https://github.com/Rhelmot/CelesteRandomizer`
* `git fetch remotes/upstream/master`
* `git checkout -b feature/<feature_name> remotes/upstream/master`
* Update files and stage changes
* `git commit -m "Commit Name"`
* `git push origin feature/<feature_name>`
* Create a pull request on Github with a meaningful title and a short description.

### Setting Up the Environment

Ensure your Celeste build is using FNA. Before you change it, make a backup of your orig folder. Delete the orig folder. Then you can open Steam and go to your library. Right click on Celeste and select 'Properties'. Under 'Betas' ensure that the 'Beta Participation' is set to 'opengl - opengl'.

From there, you will need to reinstall Everest if you were previously on XNA. If it asks to remove residual builds, I would recommend doing so. You may need to add 'CelesteRandomzier/' to blacklist.txt to avoid conflicts. If you have not done so before, you will need to install legacyRef from Everest mod options menu.

You will need to make two folders in the base directory. One named "dist" which will be where builds will be produced. The other name "packages" which will have a handful of references necessary for building.

From the legacyRef folder (SteamLibrary/steamapps/common/Celeste/legacyRef), grab the following files:

* Celeste.exe
* FNA.dll
* MMHOOK_Celeste.dll
* Mono.Cecil.dll
* Mono.Cecil.Mdb.dll
* Mono.Cecil.Pdb.dll
* Mono.Cecil.Rocks.dll
* MonoMod.RuntimeDetour.dll
* MonoMod.Util.dll
* Steamworks.Net.dll
* YamlDotNet.dll

These files will need to be inserted into the "packages" folder you created earlier.

Now to build, you can simply run the bundle.sh in your favorite terminal.

## Coding and Configs

For an understanding on how Celeste mods work, please refer to the [Everest Wiki](https://github.com/EverestAPI/Resources/wiki/Making-Code-Mods).

For editing configs, you can view [this file](https://github.com/rhelmot/CelesteRandomizer/blob/master/docs/metadata.md) to get a full understanding of how the configs are formatted.

For more specific questions, please join the [Celeste Discord](https://discord.gg/celeste) and ask in #code_modding for coding questions and #randomizer for config questions.
