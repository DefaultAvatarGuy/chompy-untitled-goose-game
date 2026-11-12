# Install and remove Chompy

## Supported game

Windows x64, itch.io DRM-free Untitled Goose Game 1.1.4. The installer verifies the specific supported game files. Other storefronts/builds, macOS, Linux, consoles, and mod managers are not verified. Own a legitimate copy of the game. No game download is included.

## Install — once

1. Close the game. Keep a clean copy of your original game download; optionally copy your entire unmodified game folder as a quick restore point.
2. Download the PLAYER ZIP from the release's download/files section. GitHub's automatically generated “Source code” ZIP is for developers and will not install the mod.
3. Right-click the downloaded ZIP and choose Extract All. Do not run the installer from inside the ZIP.
4. Move the complete **Chompy-Playtime** folder into the game folder, beside **Untitled.exe**. Leave the files inside Chompy-Playtime. Do not copy the entire Release Kit into the game folder.
5. Open Chompy-Playtime and double-click **Install.cmd**. Wait for the success message. No separate BepInEx, sound installer, character picker, or .NET 8 runtime installation is needed to play.
6. Launch **Untitled.exe** normally. Enter gameplay and allow a second for the runtime features to attach. Use the game's normal honk and wing-flap controls.

To update an existing Chompy install, close the game, replace the old Chompy-Playtime installer folder with this complete folder, and run Install.cmd again. Keep the original asset backups in Untitled_Data and StreamingAssets. Do not run older installers afterward.

If Windows blocks the download, inspect the file's Properties and use Unblock if available, then extract it again. Do not disable Windows security or use an administrator account as a routine fix. An “unknown game revision/other modification” error means the installer stopped: use a clean supported game copy rather than forcing it over another mod.

## Simple options — close the game first

| File | Result |
| --- | --- |
| Restore-Honk.cmd | Original goose honk; current Chompy and runtime features stay |
| Back-to-Classic.cmd | Original simple Chompy with honk; disables runtime arms/fire/fart/size features |
| Install.cmd | Re-enable the full package |
| Restore-v0.5.cmd | Development recovery only: older Chompy before the visible-arm fix |

Back-to-Classic does **not** return the character to the goose. It keeps the original Chompy model. Restore-Honk does **not** mute the separate fart sound.

## Full uninstall — return to the goose

The simplest complete reset is to close the game and use your clean, pre-mod game-folder copy or extract your original purchased game download into a NEW empty folder. Launch that clean copy. Keep any unrelated mods and saves backed up. The Chompy installer does not change save files.

For an in-place removal on an installation where you retained the verified original backups:

1. Close the game. Copy the original backup of **Untitled_Data/sharedassets2.assets** over **Untitled_Data/sharedassets2.assets**. The backup is beside it with a suffix such as `.chompy-playtime-original`, `.tiny-rex-original`, or `.character-club-original`. Use a known clean backup; if unsure, use the clean-copy method above.
2. Copy the original backup of **Untitled_Data/StreamingAssets/Master Bank.bank** over **Master Bank.bank** in that same folder. Its suffix may be `.chompy-playtime-original`, `.chompy-roar-original`, or `.character-club-original`. Keep spaces in the bank filename.
3. Delete the installed **BepInEx/plugins/TinyRexPlaytime** folder, then delete the **Chompy-Playtime** installer folder. This removes Chompy's plugin and fart recording.
4. If Chompy was your ONLY BepInEx mod and you did not have a loader before installing it, you may also remove **BepInEx**, **winhttp.dll**, **doorstop_config.ini**, and **.doorstop_version** from beside Untitled.exe. Keep shared loader files if another mod uses them. The loader's **changelog.txt** can also be removed if it came from this package.
5. Optional: remove **Chompy-Arms-Diagnostics.txt**. Keep original backups until you have launched and verified the restored game.

There is no dedicated one-click full uninstaller in this release. The supplied recovery buttons have the narrower functions described above.

## Troubleshooting / reports

- No Chompy at all: check the folder placement and the install success message.
- Chompy appears but no runtime features: check **BepInEx/LogOutput.log** beside the game and allow a second after entering gameplay.
- Arms problem: run **Open-Arm-Report.cmd** inside Chompy-Playtime. Include the report with your description.
- Unknown file/version: keep the error text. Do not delete unknown files to bypass the check.
- Report your game version/store, install or update steps, exact error, and relevant log. Remove personal Windows usernames/paths from logs before posting publicly. Do not upload full game files or save data containing personal information.
