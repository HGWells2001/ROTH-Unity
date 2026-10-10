# ROTH Unity 0.6.2

Hotfix for Unity 6000.6 package dependencies.

- Adds the built-in `com.unity.modules.audio` module required by `AudioClip` and `AudioSource`.
- Keeps `com.unity.modules.physics` for `MeshCollider`, colliders and `CharacterController`.
- Does not declare the invalid `com.unity.modules.inputlegacy` package. The project still uses Unity's legacy `UnityEngine.Input` API without a Package Manager dependency.
- No original Realms of the Haunting assets are included.
