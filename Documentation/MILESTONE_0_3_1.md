# Milestone 0.3.1

Hotfix for Unity 6000.6.0f1 modular built-in packages.

## Fix

`RothMapMeshBuilder` uses `MeshCollider`, which is provided by `UnityEngine.PhysicsModule`.
The project manifest now explicitly enables the built-in Physics module:

```json
"com.unity.modules.physics": "1.0.0"
```

This removes CS1069 errors for `UnityEngine.MeshCollider` on projects where the Physics built-in package was not already enabled.
