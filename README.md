<div style="display: flex; align-items: center; cursor: pointer;">
  <img src="Documentation~/images/Scene.png" width="120" style="margin-right: 10px;">
  <h1 style="margin: 0; padding: 0;">Scene Reference</h1>
</div>

*Originally based on https://github.com/JohannesMP/unity-scene-reference*


Serializable scene reference type for Unity

Supports build-settings scenes and, when `com.unity.addressables` is installed, also persists addressable scene metadata.
The runtime API returns wrapper operations that work across both `SceneManager` and Addressables loading.


[![SceneReference Inspector][1]][1]

[1]:./Documentation~/images/scenereference_inspector.png

For further information please refer to the [User Faced Documentation](Documentation~/index.md).

## Requirements

- Unity 2022.3 LTS or higher

## Loading

```csharp
var loadOperation = _scene.LoadAsync(LoadSceneMode.Additive, activateOnLoad: false);

while (!loadOperation.IsDone)
{
    Debug.Log(loadOperation.Progress);
    yield return null;
}

if (loadOperation.IsReadyForActivation)
{
    yield return loadOperation.ActivateAsync();
}
```

For regular scenes the runtime key is the scene path. For Addressables scenes the runtime key is GUID-first,
with the address kept as inspector-facing information.
