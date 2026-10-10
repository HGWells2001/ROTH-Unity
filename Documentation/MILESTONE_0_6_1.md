# ROTH Unity 0.6.1

Hotfix for Unity 6 package resolution.

- Removed invalid `com.unity.modules.inputlegacy` dependency from `Packages/manifest.json`.
- Kept built-in `com.unity.modules.physics` required by `MeshCollider` and `CharacterController`.
- Runtime input code continues to use Unity's legacy `UnityEngine.Input` API, which does not require a Package Manager dependency entry.
- No original Realms of the Haunting assets are included.
