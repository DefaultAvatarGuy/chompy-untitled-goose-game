# Source layout

This repository is source for developers. Download the player ZIP from Releases to install.

Source/TinyRexPlaytime.cs, ControlsCore.cs and EmbeddedArms.cs are the complete working runtime source; Source/Chompy-Arms.json is editable arm geometry. Installer scripts and the supported file-hash manifest are included at repository root for review.

Source/Original-Geometry contains the original procedural T-Rex generator and patch builder, recovered from the original v0.1 work. They use development folders such as work/ and game/. Source/build_arms.py is the later arm/triangle-patch builder and also assumes intermediate rig data from that development tree. The embedded arm geometry is already present for rebuilding the runtime.

No Payload/, Model/, Roar/, Recovery/ binary payloads, sound recordings, proprietary game references, or loader binaries are tracked here. Install.ps1 requires these folders from the separate player release and cannot install from this source-only repository. BUILD.txt specifies the game's private assembly references needed to compile the C# plugin. Fetch BepInEx 5.4.23.5 from its official upstream for the development reference. Do not upload your game DLLs.

The fixed plugin ID and folder name retain their historic TinyRex/brandcreative identifiers for compatibility. Public project authorship is DefaultAvatarGuy. The release keeps internal runtime version 0.6.1 to preserve the verified binary; v1.0.0 is the public package version.
