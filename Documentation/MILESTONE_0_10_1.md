# ROTH Unity Milestone 0.10.1

Hotfix for Unity 6000.6.0f1 package resolution.

## Fix

- Added built-in Unity IMGUI module dependency: `com.unity.modules.imgui: 1.0.0`.
- Keeps existing Audio and Physics module dependencies.
- No gameplay or data-format changes from 0.10.

`RothRuntimeHud` and other development overlays use `GUI`, `GUILayout`, and `GUIStyle`, which are provided by `UnityEngine.IMGUIModule` in Unity 6.

Original Realms of the Haunting assets are not included.
