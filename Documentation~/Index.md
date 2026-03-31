# About Scene Reference

A wrapper that provides the means to safely serialize `SceneAsset` References. Adds aditional controls for the serialized `SceneAsset`. This helps to have all-in-one tool to manage basic settings of your scene.

## Content

- [Requirements](#requirements)
- [Installation](#installation)
- [Using Scene Reference](#using-scene-references)
- [Features added by this package](#features-added-by-this-package)
- [Samples](#samples)

## Requirements

- Unity 2022.3 LTS or higher

## Installation

Install by git URL

```
https://github.com/der-hugo/SceneReference?path=com.derhugo.unity.scenereference
```

## Using Scene Reference

To your according assembly references add `derHugo.Unity.SceneReference` and then use

```csharp
using derHugo.Unity.SceneReference;
```

Then you can simply use the type `SceneReference` for exposing a `SceneAsset` field to the Inspector.

```csharp
[SerializeField] private SceneReference _example;
```

## Functionality

Internally we serialize a `UnityEngine.Object` reference to the `SceneAsset` which only exists at editor time.
Any time the object is serialized, we store the `path` provided by this Asset (assuming it was valid).

This means that, come build time, the string path of the scene asset is always already stored,
which if the scene was added to the build settings means it can be loaded.

There is an implicit conversion to `string` returning the `path` so a `SceneReference`
can directly passed as a parameter to `SceneManager.LoadScene`

## Visual Enhancement

It is up to the user to ensure the scene exists in the build settings so it is loadable at runtime.

To help with this, a custom `PropertyDrawer` displays the scene build settings state.

[![SceneReference Inspector][1]][1]

[1]:./images/scenereference_inspector.png


## Known issues:

- When reverting back to a prefab which has the asset stored as null, Unity will show the property as modified despite
  having just reverted. This only happens on the first time, and reverting again fix it. Under the hood the state is still always valid and serialized correctly regardless.

## Samples

- [Scene View Sample](sample-scenereference.md) - *Showcases usage of `Scenereference`*
