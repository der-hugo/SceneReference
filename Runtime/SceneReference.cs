using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

#if SUPPORT_ADDRESABBLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;
using UnityEditor.VersionControl;
#if SUPPORT_ADDRESABBLES
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif

#endif

namespace derHugo.Unity.SceneReference
{
    /// <summary>
    /// <para>A wrapper that provides the means to safely serialize Scene Asset References.</para>
    /// <para>
    /// Internally we serialize an Object to the SceneAsset which only exists at editor time.
    /// Any time the object is serialized, we store the path provided by this Asset (assuming it was valid).
    /// </para>
    /// <para>This means that, come build time, the string path of the scene asset is always already stored, which if the scene was added to the build settings means it can be loaded.</para>
    /// <para>It is up to the user to ensure the scene exists in the build settings so it is loadable at runtime. To help with this, a custom PropertyDrawer displays the scene build settings state.</para>
    /// <para>
    /// Known issues:
    /// <list type="bullet">
    /// <item>
    /// When reverting back to a prefab which has the asset stored as null, Unity will show the property as modified despite having just reverted. This only happens on the fist time, and reverting again fix it. Under the hood the state is still always valid and serialized correctly regardless.
    /// </item>
    /// </list>
    /// </para>
    /// <para>Original Source: https://github.com/JohannesMP/unity-scene-reference</para>
    /// </summary>
    [Serializable]
    public class SceneReference : IEquatable<SceneReference>, IEquatable<Scene>
#if UNITY_EDITOR
                                  , ISerializationCallbackReceiver
#endif
    {
#if UNITY_EDITOR
        /// <summary>
        /// What we use in editor to select the scene
        /// </summary>
        [SerializeField] private SceneAsset _sceneAsset;
#endif

        /// <summary>
        /// This should only ever be set during serialization/deserialization!
        /// </summary>
        [SerializeField] private string _path = "Assets/Scenes/SampleScene.unity";

        /// <summary>
        /// This should only ever be set during serialization/deserialization!
        /// </summary>
        [SerializeField] private string _name = "SampleScene";

#if SUPPORT_ADDRESABBLES
        [SerializeField] private bool _isAddressable;
        [SerializeField] private string _addressableAddress;
        [SerializeField] private string _addressableAssetGuid;
#endif

#if SUPPORT_ADDRESABBLES
        private SceneReferenceLoadOperation _lastLoadOperation;
#endif

        public SceneReferenceLoadOperation LoadAsync(LoadSceneMode loadSceneMode = LoadSceneMode.Single, bool activateOnLoad = true, int priority = 100)
        {
            return LoadAsync(new LoadSceneParameters(loadSceneMode), activateOnLoad, priority);
        }

        public SceneReferenceLoadOperation LoadAsync(LoadSceneParameters loadSceneParameters, bool activateOnLoad = true, int priority = 100)
        {
            var validState = IsValidScene;
            if (validState != ValidState.Valid)
            {
                var invalidOperation = SceneReferenceLoadOperation.CreateInvalid(
                    this,
                    new InvalidOperationException(
                        $"SceneReference '{Name}' is not loadable. Validation failed with '{validState}'. Path='{Path}', RuntimeKey='{RuntimeKey}'."
                    )
                );

#if SUPPORT_ADDRESABBLES
                _lastLoadOperation = invalidOperation;
#endif

                return invalidOperation;
            }

            SceneReferenceLoadOperation operation;

#if SUPPORT_ADDRESABBLES
            if (IsAddressable)
            {
                operation = SceneReferenceLoadOperation.CreateAddressable(
                    this,
                    Addressables.LoadSceneAsync(AddressableRuntimeKey, loadSceneParameters, activateOnLoad, priority),
                    activateOnLoad,
                    priority
                );
                _lastLoadOperation = operation;
                return operation;
            }
#endif

            var sceneOperation = SceneManager.LoadSceneAsync(Path, loadSceneParameters);
            if (sceneOperation != null)
            {
                sceneOperation.priority = priority;
                sceneOperation.allowSceneActivation = activateOnLoad;
            }

            operation = SceneReferenceLoadOperation.CreateSceneManager(this, sceneOperation, activateOnLoad, priority);

#if SUPPORT_ADDRESABBLES
            _lastLoadOperation = operation;
#endif

            return operation;
        }

        public SceneReferenceLoadOperation Load(LoadSceneMode loadSceneMode = LoadSceneMode.Single, bool activateOnLoad = true, int priority = 100)
        {
            return LoadAsync(loadSceneMode, activateOnLoad, priority);
        }

        public SceneReferenceLoadOperation Load(LoadSceneParameters loadSceneParameters, bool activateOnLoad = true, int priority = 100)
        {
            return LoadAsync(loadSceneParameters, activateOnLoad, priority);
        }

        public SceneReferenceUnloadOperation UnloadAsync(UnloadSceneOptions unloadOptions = UnloadSceneOptions.None, bool autoReleaseHandle = true)
        {
#if SUPPORT_ADDRESABBLES
            if (_lastLoadOperation != null && _lastLoadOperation.IsValid)
            {
                return _lastLoadOperation.Unload(unloadOptions, autoReleaseHandle);
            }

            if (IsAddressable)
            {
                return SceneReferenceUnloadOperation.CreateInvalid(this);
            }
#endif

            var scene = SceneManager.GetSceneByPath(Path);
            return SceneReferenceUnloadOperation.CreateSceneManager(
                this,
                scene.IsValid() ? SceneManager.UnloadSceneAsync(scene, unloadOptions) : null
            );
        }

        public SceneReferenceUnloadOperation Unload(UnloadSceneOptions unloadOptions = UnloadSceneOptions.None, bool autoReleaseHandle = true)
        {
            return UnloadAsync(unloadOptions, autoReleaseHandle);
        }

        public SceneReferenceLoadOperation LastLoadOperation
        {
            get
            {
#if SUPPORT_ADDRESABBLES
                return _lastLoadOperation;
#else
                return null;
#endif
            }
        }

        /// <summary>
        /// In edit mode return the <see cref="_sceneAsset"/>.<see cref="UnityEngine.Object.name"/>
        /// <para>In a build returns the serialized <see cref="_name"/></para>
        /// </summary>
        public string Name
        {
            get
            {
#if UNITY_EDITOR
                return _sceneAsset ? _sceneAsset.name : string.Empty;
#else
                return _name;
#endif
            }
        }

#if SUPPORT_ADDRESABBLES
        /// <summary>
        /// Whether this scene is also registered as an Addressables scene.
        /// </summary>
        public bool IsAddressable
        {
            get
            {
                var addressableSceneData = GetCurrentAddressableSceneData();
                return addressableSceneData.IsAddressable && !string.IsNullOrWhiteSpace(addressableSceneData.AssetGuid);
            }
        }

        /// <summary>
        /// The Addressables address of this scene if one was assigned.
        /// </summary>
        public string AddressableAddress
        {
            get
            {
                var addressableSceneData = GetCurrentAddressableSceneData();
                return addressableSceneData.IsAddressable ? addressableSceneData.Address : string.Empty;
            }
        }

        /// <summary>
        /// The asset guid used by Addressables as the preferred stable runtime key.
        /// </summary>
        public string AddressableAssetGuid
        {
            get
            {
                var addressableSceneData = GetCurrentAddressableSceneData();
                return addressableSceneData.IsAddressable ? addressableSceneData.AssetGuid : string.Empty;
            }
        }

        /// <summary>
        /// The key to pass to Addressables when loading this scene.
        /// </summary>
        public string AddressableRuntimeKey
        {
            get
            {
                var addressableSceneData = GetCurrentAddressableSceneData();
                return addressableSceneData.RuntimeKey;
            }
        }
#endif

        /// <summary>
        /// In edit mode checks if the <see cref="Path"/> is valid
        /// <para>In play mode additionally checks whether the scene is added to the build settings and enabled</para>
        /// </summary>
        public ValidState IsValidScene
        {
            get
            {
#if SUPPORT_ADDRESABBLES
                if (IsAddressable)
                {
                    if (string.IsNullOrWhiteSpace(AddressableRuntimeKey))
                    {
                        return ValidState.InvalidAddressableKey;
                    }

                    if (Application.isPlaying && TryValidateRuntimeAddressableKey(out var hasValidRuntimeKey) && !hasValidRuntimeKey)
                    {
                        return ValidState.InvalidAddressableKey;
                    }

                    return ValidState.Valid;
                }
#endif

                if (string.IsNullOrWhiteSpace(Path))
                {
                    return ValidState.InvalidUnassigned;
                }

                if (Application.isPlaying)
                {
                    if (SceneUtility.GetBuildIndexByScenePath(Path) < 0)
                    {
                        return ValidState.InvalidNotInBuildSettings;
                    }
                }

                return ValidState.Valid;
            }
        }

        public enum ValidState
        {
            Valid,
            InvalidUnassigned,
            InvalidNotInBuildSettings,
#if SUPPORT_ADDRESABBLES
            InvalidAddressableKey
#endif
        }

        /// <summary>
        /// Is this scene currently loaded?
        /// <para>
        /// This is valid for both regular and Addressables scenes once the scene is actually activated into the
        /// <see cref="SceneManager"/>. For delayed activation workflows use the returned
        /// <see cref="SceneReferenceLoadOperation"/> to inspect <see cref="SceneReferenceLoadOperation.IsReadyForActivation"/>.
        /// </para>
        /// </summary>
        public bool IsLoaded => SceneManager.GetSceneByPath(Path).isLoaded;

        /// <summary>
        /// In editor returns the <see cref="_sceneAsset"/>'s asset path
        /// <para>In a build we rely on the stored <see cref="_path"/> value which we assume was serialized correctly at build time. See <see cref="OnBeforeSerialize"/> and <see cref="OnAfterDeserialize"/></para>
        /// </summary>
        public string Path
        {
            get
            {
#if UNITY_EDITOR
                // In editor we always use the asset's path
                return GetScenePathFromAsset();
#else
                return _path;
#endif
            }
        }

        /// <summary>
        /// Returns the runtime loading key for this scene.
        /// <para>
        /// For regular scenes this is the serialized <see cref="Path"/>.
        /// For Addressables scenes this is the serialized asset guid if available, otherwise the address.
        /// </para>
        /// </summary>
        public string RuntimeKey
        {
            get
            {
#if SUPPORT_ADDRESABBLES
                if (IsAddressable)
                {
                    return AddressableRuntimeKey;
                }
#endif
                return Path;
            }
        }

        /// <summary>
        /// Checks SceneReference wrapped Scene if it is equal to another Scene
        /// </summary>
        /// <param name="other">other scene to check it against</param>
        /// <returns>TRUE or FALSE depending if the scenes are equal or not</returns>
        public bool Equals(Scene other)
        {
            return Path == other.path;
        }

        /// <summary>
        /// Checks if SceneReferences are equal
        /// </summary>
        /// <param name="other">other SceneReference to check it against</param>
        /// <returns>TRUE or FALSE depending if the SceneReferences are equal or not</returns>
        public bool Equals(SceneReference other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Path == other.Path;
        }

        /// <summary>
        /// Implicit conversion to string using the scene's <see cref="Path"/> in order to directly use this in e.g. <see cref="SceneManager.LoadScene(string,UnityEngine.SceneManagement.LoadSceneMode)"/>
        /// </summary>
        /// <param name="sceneReference">Desired SceneReference to get the string of</param>
        /// <returns>Scene path</returns>
        public static implicit operator string(SceneReference sceneReference)
        {
            return sceneReference.Path;
        }

        /// <summary>
        /// Operator that checks if SceneReference and Scene are equal
        /// </summary>
        /// <param name="sceneReference">Provided SceneReference</param>
        /// <param name="scene">Provided Scene</param>
        /// <returns>TRUE or FALSE depending if they're equal</returns>
        public static bool operator ==(SceneReference sceneReference, Scene scene)
        {
            if (ReferenceEquals(sceneReference, null))
            {
                return false;
            }

            return sceneReference.Equals(scene);
        }

        /// <summary>
        /// Operator that checks if SceneReference and Scene are not equal
        /// </summary>
        /// <param name="sceneReference">Provided SceneReference</param>
        /// <param name="scene">Provided Scene</param>
        /// <returns>TRUE or FALSE depending if they're not equal</returns>
        public static bool operator !=(SceneReference sceneReference, Scene scene)
        {
            if (ReferenceEquals(sceneReference, null))
            {
                return true;
            }

            return !sceneReference.Equals(scene);
        }

        /// <summary>
        /// Operator that checks if SceneReference and Scene are equal
        /// </summary>
        /// <param name="sceneReference">Provided SceneReference</param>
        /// <param name="scene">Provided Scene</param>
        /// <returns>TRUE or FALSE depending if they're equal</returns>
        public static bool operator ==(Scene scene, SceneReference sceneReference)
        {
            if (ReferenceEquals(sceneReference, null))
            {
                return false;
            }

            return sceneReference.Equals(scene);
        }

        /// <summary>
        /// Operator that checks if SceneReference and Scene are not equal
        /// </summary>
        /// <param name="sceneReference">Provided SceneReference</param>
        /// <param name="scene">Provided Scene</param>
        /// <returns>TRUE or FALSE depending if they're not equal</returns>
        public static bool operator !=(Scene scene, SceneReference sceneReference)
        {
            if (ReferenceEquals(sceneReference, null))
            {
                return true;
            }

            return !sceneReference.Equals(scene);
        }

        /// <summary>
        /// Operator that checks if two SceneReferences are equal
        /// </summary>
        /// <param name="a">Provided SceneReference a</param>
        /// <param name="b">Provided SceneReference b</param>
        /// <returns>TRUE or FALSE depending if they're equal</returns>
        public static bool operator ==(SceneReference a, SceneReference b)
        {
            if (ReferenceEquals(a, null))
            {
                return false;
            }

            return a.Equals(b);
        }

        /// <summary>
        /// Operator that checks if two SceneReferences are not equal
        /// </summary>
        /// <param name="a">Provided SceneReference a</param>
        /// <param name="b">Provided SceneReference b</param>
        /// <returns>TRUE or FALSE depending if they're not equal</returns>
        public static bool operator !=(SceneReference a, SceneReference b)
        {
            if (ReferenceEquals(a, null))
            {
                return true;
            }

            return !a.Equals(b);
        }

        /// <summary>
        /// Checks if SceneReference is equal to specified object
        /// </summary>
        /// <param name="other">other object to check the SceneReference against</param>
        /// <returns>TRUE or FALSE depending if the object is equal to the SceneReference</returns>
        public override bool Equals(object other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (ReferenceEquals(other, null))
            {
                return false;
            }

            switch (other)
            {
                case SceneReference sceneReference:
                    return Equals(sceneReference);

                case Scene scene:
                    return Equals(scene);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return Path != null ? Path.GetHashCode() : 0;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Get scene build ids
        /// </summary>
        public EditorBuildSettingsScene GetEditorBuildSettingsScene()
        {
            return EditorBuildSettings.scenes.FirstOrDefault(s => s.path == GetScenePathFromAsset());
        }

        public Scene GetSceneStruct()
        {
            return SceneManager.GetSceneByPath(GetScenePathFromAsset());
        }

        public void OnBeforeSerialize()
        {
            HandleBeforeSerialize();
        }

        public void OnAfterDeserialize()
        {
            // We cannot touch AssetDatabase during serialization, so we delay it
            // remove first -> makes sure there is definitely only one callback at a time
            EditorApplication.update -= HandleAfterDeserialize;
            EditorApplication.update += HandleAfterDeserialize;
        }

        private void HandleBeforeSerialize()
        {
            if (_sceneAsset)
            {
                // Asset takes precedence and overwrites Path
                _path = GetScenePathFromAsset();
                _name = _sceneAsset.name;
            }
            else
            {
                // Do not attempt path-to-asset recovery here. Unity may invoke serialization during domain backup,
                // and AssetDatabase.LoadAssetAtPath is not allowed in that phase.
                if (string.IsNullOrWhiteSpace(_path))
                {
                    _name = string.Empty;
#if SUPPORT_ADDRESABBLES
                    ClearAddressableState();
#endif
                }
            }
        }

        private void HandleAfterDeserialize()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            EditorApplication.update -= HandleAfterDeserialize;

            if (_sceneAsset)
            {
#if SUPPORT_ADDRESABBLES
                UpdateAddressableState(GetScenePathFromAsset());
#endif
                // Asset is valid, don't do anything - Path will always be set based on it when it matters
                return;
            }

            if (string.IsNullOrWhiteSpace(_path))
            {
#if SUPPORT_ADDRESABBLES
                ClearAddressableState();
#endif
                // Asset is invalid and we don't have a path to try and recover from
                return;
            }

            _sceneAsset = GetSceneAssetFromPath();

            if (!_sceneAsset)
            {
                // No asset found, path was invalid. Make sure we don't carry over the old invalid path
                _path = string.Empty;
                _name = string.Empty;
#if SUPPORT_ADDRESABBLES
                ClearAddressableState();
#endif
            }
            else
            {
#if SUPPORT_ADDRESABBLES
                UpdateAddressableState(_path);
#endif
            }

            if (!Application.isPlaying)
            {
                // we either recovered the asset from the given path or reset the path and name to empty
                // in both cases we need to save
                EditorSceneManager.MarkAllScenesDirty();
            }
        }

        private SceneAsset GetSceneAssetFromPath()
        {
            return string.IsNullOrWhiteSpace(_path) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(_path);
        }

        private string GetScenePathFromAsset()
        {
            return _sceneAsset ? AssetDatabase.GetAssetPath(_sceneAsset) : string.Empty;
        }

#if SUPPORT_ADDRESABBLES
        private bool TryValidateRuntimeAddressableKey(out bool hasValidRuntimeKey)
        {
            hasValidRuntimeKey = false;

            var runtimeKey = AddressableRuntimeKey;
            if (string.IsNullOrWhiteSpace(runtimeKey))
            {
                return true;
            }

            var sawAnyLocator = false;

            foreach (var locator in Addressables.ResourceLocators)
            {
                sawAnyLocator = true;

                if (!locator.Locate(runtimeKey, typeof(SceneInstance), out IList<IResourceLocation> locations))
                {
                    continue;
                }

                if (locations != null && locations.Count > 0)
                {
                    hasValidRuntimeKey = true;
                    return true;
                }
            }

            return sawAnyLocator;
        }

        private AddressableSceneData GetCurrentAddressableSceneData()
        {
#if UNITY_EDITOR
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                var scenePath = _sceneAsset ? GetScenePathFromAsset() : _path;
                return string.IsNullOrWhiteSpace(scenePath)
                    ? AddressableSceneData.None
                    : AddressablesUtils.GetSceneData(scenePath);
            }
#endif

            return new AddressableSceneData(_isAddressable, _addressableAssetGuid, _addressableAddress);
        }

        private struct AddressableSceneData
        {
            public static readonly AddressableSceneData None = new(false, string.Empty, string.Empty);

            public AddressableSceneData(bool isAddressable, string assetGuid, string address)
            {
                IsAddressable = isAddressable;
                AssetGuid = assetGuid ?? string.Empty;
                Address = address ?? string.Empty;
            }

            public bool IsAddressable { get; }
            public string AssetGuid { get; }
            public string Address { get; }
            public string RuntimeKey => !string.IsNullOrWhiteSpace(AssetGuid) ? AssetGuid : Address;
        }

#if UNITY_EDITOR
        private void UpdateAddressableState(string scenePath)
        {
            var addressableSceneData = AddressablesUtils.GetSceneData(scenePath);
            _isAddressable = addressableSceneData.IsAddressable;
            _addressableAddress = addressableSceneData.Address;
            _addressableAssetGuid = addressableSceneData.AssetGuid;
        }
#endif

        private void ClearAddressableState()
        {
            _isAddressable = false;
            _addressableAddress = string.Empty;
            _addressableAssetGuid = string.Empty;
        }

#if UNITY_EDITOR
        private static class AddressablesUtils
        {
            public static AddressableSceneData GetSceneData(string assetPath)
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    return AddressableSceneData.None;
                }

                if (!AddressableAssetSettingsDefaultObject.SettingsExists)
                {
                    return AddressableSceneData.None;
                }

                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings == null)
                {
                    return AddressableSceneData.None;
                }

                var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrWhiteSpace(assetGuid))
                {
                    return AddressableSceneData.None;
                }

                var entry = settings.FindAssetEntry(assetGuid);
                if (entry == null)
                {
                    return AddressableSceneData.None;
                }

                if (!entry.IsScene)
                {
                    return AddressableSceneData.None;
                }

                return new AddressableSceneData(
                    true,
                    entry.guid,
                    entry.address
                );
            }
        }
#endif
#endif

        /// <summary>
        /// <see cref="CustomPropertyDrawer"/> for a <see cref="SceneReference"/> in the Inspector.
        /// <para>If scene is valid, provides basic buttons to interact with the scene's role in Build Settings.</para>
        /// </summary>
        [CustomPropertyDrawer(typeof(SceneReference))]
        private class SceneReferenceDrawer : PropertyDrawer
        {
            private const string SCENE_ASSET_PROPERTY_NAME = nameof(_sceneAsset);
            private const string SCENE_PATH_PROPERTY_NAME = nameof(_path);
            private const string SCENE_NAME_PROPERTY_NAME = nameof(_name);
#if SUPPORT_ADDRESABBLES
            private const string SCENE_IS_ADDRESSABLE_PROPERTY_NAME = nameof(_isAddressable);
            private const string SCENE_ADDRESSABLE_ADDRESS_PROPERTY_NAME = nameof(_addressableAddress);
            private const string SCENE_ADDRESSABLE_ASSET_GUID_PROPERTY_NAME = nameof(_addressableAssetGuid);
#endif

            private const float PAD_SIZE = 2f;
            private const float HEADER_SIZE = 5f;
            private const float FOOTER_HEIGHT = 5f;

            private static readonly RectOffset BoxPadding = EditorStyles.helpBox.padding;

            private static readonly float LineHeight = EditorGUIUtility.singleLineHeight;
            private static readonly float PaddedLineHeight = LineHeight + PAD_SIZE;

#if SUPPORT_ADDRESABBLES
            private static readonly GUIContent LinkedDot = EditorGUIUtility.IconContent("linked");
#endif
            private static readonly GUIContent GreenDot = EditorGUIUtility.IconContent("greenlight");
            private static readonly GUIContent YellowDot = EditorGUIUtility.IconContent("orangelight");
            private static readonly GUIContent RedDot = EditorGUIUtility.IconContent("redlight");

            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                position.height -= HEADER_SIZE;
                position.y += HEADER_SIZE;

                var propertyPathParts = property.propertyPath.Split('.');
                var isArrayElement = propertyPathParts.Length > 2 && propertyPathParts[propertyPathParts.Length - 2] == "Array";

                EditorGUI.BeginProperty(position, label, property);
                {
                    if (!isArrayElement)
                    {
                        position = EditorGUI.PrefixLabel(position, label);
                    }

                    var sceneAssetProperty = property.FindPropertyRelative(SCENE_ASSET_PROPERTY_NAME);

                    // Draw the Box Background
                    position.height -= FOOTER_HEIGHT;
                    GUI.Box(EditorGUI.IndentedRect(position), GUIContent.none, EditorStyles.helpBox);
                    position = BoxPadding.Remove(position);

                    // Draw the main Object field
                    position.height = LineHeight;

                    EditorGUI.BeginChangeCheck();
                    {
                        var color = GUI.color;
                        if (!sceneAssetProperty.objectReferenceValue)
                        {
                            GUI.color = Color.red;
                        }

                        EditorGUI.PropertyField(position, sceneAssetProperty, new GUIContent { tooltip = "The actual Scene Asset reference.\nOn serialize this is also stored as the asset's path." }, false);
                        GUI.color = color;
                    }

                    var sceneAssetPath = sceneAssetProperty.objectReferenceValue ? AssetDatabase.GetAssetPath(sceneAssetProperty.objectReferenceValue) : string.Empty;
                    var isAddressableScene = false;
#if SUPPORT_ADDRESABBLES
                    var addressableSceneData = AddressablesUtils.GetSceneData(sceneAssetPath);
                    isAddressableScene = addressableSceneData.IsAddressable;
                    SyncAddressableProperties(property, addressableSceneData);
#endif

                    var buildScene = BuildUtils.GetBuildScene(sceneAssetProperty.objectReferenceValue);
                    if (EditorGUI.EndChangeCheck())
                    {
                        var pathProperty = property.FindPropertyRelative(SCENE_PATH_PROPERTY_NAME);
                        var nameProperty = property.FindPropertyRelative(SCENE_NAME_PROPERTY_NAME);

                        if (sceneAssetProperty.objectReferenceValue)
                        {
                            pathProperty.stringValue = sceneAssetPath;
                            nameProperty.stringValue = sceneAssetProperty.objectReferenceValue.name;
                        }
                        else
                        {
                            pathProperty.stringValue = string.Empty;
                            nameProperty.stringValue = string.Empty;
                        }
                    }

                    position.y += PaddedLineHeight;

                    if (isAddressableScene)
                    {
#if SUPPORT_ADDRESABBLES
                        DrawAddressablesInfoGUI(position, addressableSceneData);
#endif
                    }
                    else if (!buildScene.AssetGuid.Empty())
                    {
                        var readOnly = BuildUtils.IsReadOnly();
                        var readOnlyWarning = readOnly ? "\n\nWARNING: Build Settings is not checked out and so cannot be modified." : string.Empty;

                        // Draw the Build Settings Info of the selected Scene
                        DrawSceneInfoGUI(position, buildScene, readOnly, readOnlyWarning);

                        position.y += PaddedLineHeight;

                        if (!EditorApplication.isPlayingOrWillChangePlaymode)
                        {
                            DrawSceneButtonsGUI(position, buildScene, readOnly, readOnlyWarning);
                        }
                    }
                }

                EditorGUI.EndProperty();
            }

            /// <summary>
            /// Ensure that what we draw in OnGUI always has the room it needs
            /// </summary>
            public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                var sceneAssetProperty = property.FindPropertyRelative(SCENE_ASSET_PROPERTY_NAME);
                var sceneAssetPath = sceneAssetProperty.objectReferenceValue ? AssetDatabase.GetAssetPath(sceneAssetProperty.objectReferenceValue) : string.Empty;
                var isAddressableScene = false;

#if SUPPORT_ADDRESABBLES
                isAddressableScene = AddressablesUtils.GetSceneData(sceneAssetPath).IsAddressable;
#endif

                int lines;
                if (sceneAssetProperty.objectReferenceValue == null)
                {
                    lines = 1;
                }
                else if (isAddressableScene)
                {
                    lines = 2;
                }
                else if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    lines = 2;
                }
                else
                {
                    lines = 3;
                }

                return HEADER_SIZE + BoxPadding.vertical + LineHeight * lines + PAD_SIZE * (lines - 1) + FOOTER_HEIGHT;
            }

            /// <summary>
            /// Draws button controls of the provided scene
            /// </summary>
            private static void DrawSceneButtonsGUI(Rect position, BuildUtils.BuildScene buildScene, bool readOnly, string readOnlyWarning)
            {
                // Right context buttons
                var buttonRect = position;
                buttonRect.width /= 3f;

                using (new EditorGUI.DisabledScope(readOnly))
                {
                    // NOT in build settings
                    if (buildScene.BuildIndex == -1)
                    {
                        buttonRect.width *= 2;
                        var tooltipMsg = "Add this scene to build settings. It will be appended to the end of the build scenes" + readOnlyWarning;
                        if (DrawUtils.ButtonHelper(buttonRect, "Add...", "Add to Build", EditorStyles.miniButtonLeft, tooltipMsg))
                        {
                            BuildUtils.AddBuildScene(buildScene);
                        }

                        buttonRect.width /= 2;
                        buttonRect.x += buttonRect.width;
                    }

                    // In build settings
                    else
                    {
                        var isEnabled = buildScene.Scene.enabled;
                        var stateString = isEnabled ? "Disable" : "Enable";
                        var tooltipMsg = $"{stateString} this scene in build settings.\n{(isEnabled ? "It will no longer be included in builds" : "It will be included in builds")}.{readOnlyWarning}";

                        if (DrawUtils.ButtonHelper(buttonRect, stateString, $"{stateString} In Build", EditorStyles.miniButtonLeft, tooltipMsg))
                        {
                            BuildUtils.SetBuildSceneState(buildScene, !isEnabled);
                        }

                        buttonRect.x += buttonRect.width;

                        tooltipMsg = "Completely remove this scene from build settings.\nYou will need to add it again for it to be included in builds!" + readOnlyWarning;
                        if (DrawUtils.ButtonHelper(buttonRect, "Remove...", "Remove from Build", EditorStyles.miniButtonMid, tooltipMsg))
                        {
                            BuildUtils.RemoveBuildScene(buildScene);
                        }
                    }
                }

                buttonRect.x += buttonRect.width;

                var buildSettingsTooltip = "Open the 'Build Settings' Window for managing scenes." + readOnlyWarning;
                if (DrawUtils.ButtonHelper(buttonRect, "Settings", "Build Settings", EditorStyles.miniButtonRight, buildSettingsTooltip))
                {
                    BuildUtils.OpenBuildSettings();
                }
            }

            /// <summary>
            /// Draws info of the provided scene
            /// </summary>
            private static void DrawSceneInfoGUI(Rect position, BuildUtils.BuildScene buildScene, bool readOnly, string readOnlyWarning)
            {
                // Label Prefix
                GUIContent iconContent;
                var labelContent = new GUIContent();

                if (buildScene.BuildIndex == -1)
                {
                    // Missing from build scenes
                    iconContent = RedDot;
                    labelContent.text = "NOT In Build Settings!";
                    labelContent.tooltip = "This scene is NOT in build settings.\nIt will be NOT included in builds.";
                }
                else if (!buildScene.Scene.enabled)
                {
                    // In build scenes and disabled
                    iconContent = YellowDot;
                    labelContent.text = "BuildIndex: " + buildScene.BuildIndex + " (DISABLED!)";
                    labelContent.tooltip = "This scene is in build settings but DISABLED.\nIt will be NOT included in builds.";
                }
                else
                {
                    // In build scenes and enabled
                    iconContent = GreenDot;
                    labelContent.text = "BuildIndex: " + buildScene.BuildIndex;
                    labelContent.tooltip = "This scene is in build settings and ENABLED.\nIt will be included in builds." + readOnlyWarning;
                }

                // status label
                using (new EditorGUI.DisabledScope(readOnly))
                {
                    var iconRect = position;
                    iconRect.width = PaddedLineHeight;
                    EditorGUI.LabelField(iconRect, iconContent);

                    var labelRect = position;
                    labelRect.width -= iconRect.width;
                    labelRect.x += iconRect.width;
                    EditorGUI.LabelField(labelRect, labelContent);
                }
            }

#if SUPPORT_ADDRESABBLES
            private static void DrawAddressablesInfoGUI(Rect position, AddressableSceneData addressableSceneData)
            {
                GUIContent iconContent;
                var labelContent = new GUIContent();

                if (addressableSceneData.IsAddressable)
                {
                    iconContent = LinkedDot;
                    labelContent.text = string.IsNullOrWhiteSpace(addressableSceneData.Address)
                        ? "Addressable"
                        : "Address: " + addressableSceneData.Address;
                    labelContent.tooltip = string.IsNullOrWhiteSpace(addressableSceneData.Address)
                        ? "Runtime key uses the asset GUID: " + addressableSceneData.AssetGuid
                        : "Runtime key uses the asset GUID: " + addressableSceneData.AssetGuid + "\nAddress: " + addressableSceneData.Address;
                }
                else
                {
                    return;
                }

                var iconRect = position;
                iconRect.width = PaddedLineHeight;
                EditorGUI.LabelField(iconRect, iconContent);

                var labelRect = position;
                labelRect.width -= iconRect.width;
                labelRect.x += iconRect.width;
                EditorGUI.LabelField(labelRect, labelContent);
            }

            private static void SyncAddressableProperties(SerializedProperty property, AddressableSceneData addressableSceneData)
            {
                var isAddressableProperty = property.FindPropertyRelative(SCENE_IS_ADDRESSABLE_PROPERTY_NAME);
                if (isAddressableProperty.boolValue != addressableSceneData.IsAddressable)
                {
                    isAddressableProperty.boolValue = addressableSceneData.IsAddressable;
                }

                var addressableAddressProperty = property.FindPropertyRelative(SCENE_ADDRESSABLE_ADDRESS_PROPERTY_NAME);
                if (addressableAddressProperty.stringValue != addressableSceneData.Address)
                {
                    addressableAddressProperty.stringValue = addressableSceneData.Address;
                }

                var addressableAssetGuidProperty = property.FindPropertyRelative(SCENE_ADDRESSABLE_ASSET_GUID_PROPERTY_NAME);
                if (addressableAssetGuidProperty.stringValue != addressableSceneData.AssetGuid)
                {
                    addressableAssetGuidProperty.stringValue = addressableSceneData.AssetGuid;
                }
            }
#endif


            private static class DrawUtils
            {
                /// <summary>
                /// Draw a GUI button, choosing between a short and a long button text based on if it fits
                /// </summary>
                public static bool ButtonHelper(Rect position, string msgShort, string msgLong, GUIStyle style, string tooltip = null)
                {
                    var content = new GUIContent(msgLong)
                    {
                        tooltip = tooltip
                    };

                    var longWidth = style.CalcSize(content).x;
                    if (longWidth > position.width)
                    {
                        content.text = msgShort;
                    }

                    return GUI.Button(position, content, style);
                }
            }

            /// <summary>
            /// Various BuildSettings interactions
            /// </summary>
            private static class BuildUtils
            {
                // time in seconds that we have to wait before we query again when IsReadOnly() is called.
                private const float MIN_CHECK_WAIT = 3;
                private const string EDITORBUILDSETTINGS_ASSET_PATH = "ProjectSettings/EditorBuildSettings.asset";

                private static float _lastTimeChecked;
                private static bool _cachedReadonlyVal = true;

                /// <summary>
                /// Check if the build settings asset is readonly.
                /// Caches value and only queries state a max of every 'minCheckWait' seconds.
                /// </summary>
                public static bool IsReadOnly()
                {
                    var curTime = Time.realtimeSinceStartup;
                    var timeSinceLastCheck = curTime - _lastTimeChecked;

                    if (!(timeSinceLastCheck > MIN_CHECK_WAIT))
                    {
                        return _cachedReadonlyVal;
                    }

                    _lastTimeChecked = curTime;
                    _cachedReadonlyVal = QueryBuildSettingsStatus();

                    return _cachedReadonlyVal;
                }

                /// <summary>
                /// A blocking call to the Version Control system to see if the build settings asset is readonly.
                /// Use BuildSettingsIsReadOnly for version that caches the value for better responsiveness.
                /// </summary>
                private static bool QueryBuildSettingsStatus()
                {
                    // If no version control provider, assume not readonly
                    if (!Provider.enabled)
                    {
                        return false;
                    }

                    // If we cannot checkout, then assume we are not readonly
                    if (!Provider.hasCheckoutSupport)
                    {
                        return false;
                    }

                    // If offline (and are using a version control provider that requires checkout) we cannot edit.
                    if (Provider.onlineState == OnlineState.Offline)
                        return true;

                    // Try to get status for file
                    var status = Provider.Status(EDITORBUILDSETTINGS_ASSET_PATH, false);
                    status.Wait();

                    // If no status listed we can edit
                    if (status.assetList == null || status.assetList.Count != 1)
                    {
                        return true;
                    }

                    // If is checked out, we can edit
                    return !status.assetList[0].IsState(Asset.States.CheckedOutLocal);
                }

                /// <summary>
                /// For a given Scene Asset object reference, extract its build settings data, including buildIndex.
                /// </summary>
                public static BuildScene GetBuildScene(Object sceneObject)
                {
                    var entry = new BuildScene
                    {
                        BuildIndex = -1,
                        AssetGuid = new GUID(string.Empty)
                    };

                    if (sceneObject as SceneAsset == null)
                    {
                        return entry;
                    }

                    entry.AssetPath = AssetDatabase.GetAssetPath(sceneObject);
                    entry.AssetGuid = new GUID(AssetDatabase.AssetPathToGUID(entry.AssetPath));

                    var scenes = EditorBuildSettings.scenes;
                    for (var index = 0; index < scenes.Length; ++index)
                    {
                        if (!entry.AssetGuid.Equals(scenes[index].guid))
                        {
                            continue;
                        }

                        entry.Scene = scenes[index];
                        entry.BuildIndex = index;
                        return entry;
                    }

                    return entry;
                }

                /// <summary>
                /// Enable/Disable a given scene in the buildSettings
                /// </summary>
                public static void SetBuildSceneState(BuildScene buildScene, bool enabled)
                {
                    var modified = false;
                    var scenesToModify = EditorBuildSettings.scenes;
                    foreach (var curScene in scenesToModify.Where(curScene => curScene.guid.Equals(buildScene.AssetGuid)))
                    {
                        curScene.enabled = enabled;
                        modified = true;
                        break;
                    }

                    if (modified)
                    {
                        EditorBuildSettings.scenes = scenesToModify;
                    }
                }

                /// <summary>
                /// Display Dialog to add a scene to build settings
                /// </summary>
                public static void AddBuildScene(BuildScene buildScene)
                {
                    var newScene = new EditorBuildSettingsScene(buildScene.AssetGuid, true);
                    var tempScenes = EditorBuildSettings.scenes.ToList();
                    tempScenes.Add(newScene);
                    EditorBuildSettings.scenes = tempScenes.ToArray();
                }

                /// <summary>
                /// Display Dialog to remove a scene from build settings (or just disable it)
                /// </summary>
                public static void RemoveBuildScene(BuildScene buildScene)
                {
                    // User chose to fully remove the scene from build settings
                    var tempScenes = EditorBuildSettings.scenes.ToList();
                    tempScenes.RemoveAll(scene => scene.guid.Equals(buildScene.AssetGuid));
                    EditorBuildSettings.scenes = tempScenes.ToArray();
                }

                /// <summary>
                /// Opens the default Unity Build Settings window
                /// </summary>
                public static void OpenBuildSettings()
                {
                    EditorWindow.GetWindow<BuildPlayerWindow>();
                }

                /// <summary>
                /// A small container for tracking scene data BuildSettings
                /// </summary>
                public struct BuildScene
                {
                    public int BuildIndex;
                    public GUID AssetGuid;
                    public string AssetPath;
                    public EditorBuildSettingsScene Scene;
                }
            }
        }
#endif
    }
}
