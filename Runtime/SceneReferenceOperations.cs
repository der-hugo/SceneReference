using System;
using UnityEngine;
using UnityEngine.SceneManagement;

#if SUPPORT_ADDRESABBLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
#endif

namespace derHugo.SceneReference
{
    /// <summary>
    /// Wraps an in-flight scene load so callers can observe progress and optionally trigger activation.
    /// </summary>
    public sealed class SceneReferenceLoadOperation : CustomYieldInstruction
    {
        private readonly Exception _operationException;
        private readonly AsyncOperation _sceneOperation;

#if SUPPORT_ADDRESABBLES
        public bool IsAddressable { get; }

        public AsyncOperationHandle<SceneInstance> AddressablesLoadHandle { get; }

        private SceneReferenceLoadOperation(SceneReference sceneReference, AsyncOperationHandle<SceneInstance> addressableLoadHandle, bool activateOnLoad, int priority)
        {
            SceneReference = sceneReference;
            AddressablesLoadHandle = addressableLoadHandle;
            ActivateOnLoad = activateOnLoad;
            Priority = priority;
            IsAddressable = true;
        }

        internal static SceneReferenceLoadOperation CreateAddressable(SceneReference sceneReference, AsyncOperationHandle<SceneInstance> addressableLoadHandle, bool activateOnLoad, int priority)
        {
            return new SceneReferenceLoadOperation(sceneReference, addressableLoadHandle, activateOnLoad, priority);
        }
#endif

        private SceneReferenceLoadOperation(SceneReference sceneReference, AsyncOperation sceneOperation, bool activateOnLoad, int priority)
        {
            SceneReference = sceneReference;
            _sceneOperation = sceneOperation;
            ActivateOnLoad = activateOnLoad;
            Priority = priority;
        }

        private SceneReferenceLoadOperation(SceneReference sceneReference, Exception operationException)
        {
            SceneReference = sceneReference;
            _operationException = operationException;
        }

        internal static SceneReferenceLoadOperation CreateSceneManager(SceneReference sceneReference, AsyncOperation sceneOperation, bool activateOnLoad, int priority)
        {
            return new SceneReferenceLoadOperation(sceneReference, sceneOperation, activateOnLoad, priority);
        }

        internal static SceneReferenceLoadOperation CreateInvalid(SceneReference sceneReference, Exception operationException)
        {
            return new SceneReferenceLoadOperation(sceneReference, operationException);
        }

        public SceneReference SceneReference { get; }
        public bool ActivateOnLoad { get; }
        public int Priority { get; }

        public override bool keepWaiting => !IsDone;

        public bool IsValid
        {
            get
            {
#if SUPPORT_ADDRESABBLES
                if (IsAddressable)
                {
                    return AddressablesLoadHandle.IsValid() || ActivationOperation != null;
                }
#endif
                return _sceneOperation != null || ActivationOperation != null;
            }
        }

        public bool IsLoaded
        {
            get
            {
                var scene = Scene;
                return scene.IsValid() && scene.isLoaded;
            }
        }

        public bool HasFailed
        {
            get
            {
                if (OperationException != null)
                {
                    return true;
                }

                return
#if SUPPORT_ADDRESABBLES
                    !IsAddressable &&
#endif
                    _sceneOperation == null && ActivationOperation == null;
            }
        }

        public bool IsReadyForActivation
        {
            get
            {
                if (ActivateOnLoad || ActivationOperation != null)
                {
                    return false;
                }

#if SUPPORT_ADDRESABBLES
                if (IsAddressable)
                {
                    return AddressablesLoadHandle.IsValid()
                           && AddressablesLoadHandle.Status == AsyncOperationStatus.Succeeded
                           && AddressablesLoadHandle.IsDone
                           && !IsLoaded;
                }
#endif

                return _sceneOperation != null && !_sceneOperation.isDone && _sceneOperation.progress >= 0.9f;
            }
        }

        public bool IsActivating => ActivationOperation != null && !ActivationOperation.isDone;

        public bool IsDone
        {
            get
            {
                if (ActivationOperation != null)
                {
                    return ActivationOperation.isDone;
                }

                if (!ActivateOnLoad)
                {
#if SUPPORT_ADDRESABBLES
                    if (IsAddressable)
                    {
                        return (AddressablesLoadHandle.IsValid() && AddressablesLoadHandle.IsDone) || IsLoaded;
                    }
#endif
                    return _sceneOperation == null || IsReadyForActivation || IsLoaded;
                }

#if SUPPORT_ADDRESABBLES
                if (IsAddressable)
                {
                    return AddressablesLoadHandle.IsValid() && AddressablesLoadHandle.IsDone;
                }
#endif

                return _sceneOperation == null || _sceneOperation.isDone;
            }
        }

        public float LoadProgress
        {
            get
            {
#if SUPPORT_ADDRESABBLES
                if (IsAddressable)
                {
                    if (!AddressablesLoadHandle.IsValid())
                    {
                        return 0f;
                    }

                    return AddressablesLoadHandle.IsDone ? 1f : AddressablesLoadHandle.PercentComplete;
                }
#endif

                if (_sceneOperation == null)
                {
                    return 0f;
                }

                if (_sceneOperation.isDone)
                {
                    return 1f;
                }

                return ActivateOnLoad ? _sceneOperation.progress : Mathf.Clamp01(_sceneOperation.progress / 0.9f);
            }
        }

        public float Progress
        {
            get
            {
                if (ActivationOperation != null)
                {
                    return ActivationOperation.isDone ? 1f : ActivationOperation.progress;
                }

                if (IsReadyForActivation)
                {
                    return 0.9f;
                }

#if SUPPORT_ADDRESABBLES
                if (IsAddressable)
                {
                    return AddressablesLoadHandle.IsValid() ? AddressablesLoadHandle.PercentComplete : 0f;
                }
#endif

                if (_sceneOperation == null)
                {
                    return 0f;
                }

                return _sceneOperation.isDone ? 1f : _sceneOperation.progress;
            }
        }

        public Exception OperationException
        {
            get
            {
#if SUPPORT_ADDRESABBLES
                if (IsAddressable && AddressablesLoadHandle.IsValid())
                {
                    return AddressablesLoadHandle.OperationException;
                }
#endif
                return _operationException;
            }
        }

        public Scene Scene
        {
            get
            {
#if SUPPORT_ADDRESABBLES
                if (IsAddressable
                    && AddressablesLoadHandle.IsValid()
                    && AddressablesLoadHandle.Status == AsyncOperationStatus.Succeeded
                    && AddressablesLoadHandle.IsDone)
                {
                    return AddressablesLoadHandle.Result.Scene;
                }
#endif

                return string.IsNullOrWhiteSpace(SceneReference.Path)
                    ? default
                    : SceneManager.GetSceneByPath(SceneReference.Path);
            }
        }

        public AsyncOperation SceneOperation => ActivationOperation ?? _sceneOperation;
        public AsyncOperation ActivationOperation { get; private set; }

        public AsyncOperation ActivateAsync()
        {
            if (ActivationOperation != null)
            {
                return ActivationOperation;
            }

            if (ActivateOnLoad)
            {
                return SceneOperation;
            }

#if SUPPORT_ADDRESABBLES
            if (IsAddressable)
            {
                if (!AddressablesLoadHandle.IsValid() || !AddressablesLoadHandle.IsDone)
                {
                    return null;
                }

                if (AddressablesLoadHandle.Status != AsyncOperationStatus.Succeeded)
                {
                    return null;
                }

                ActivationOperation = AddressablesLoadHandle.Result.ActivateAsync();
                if (ActivationOperation != null)
                {
                    ActivationOperation.priority = Priority;
                }

                return ActivationOperation;
            }
#endif

            if (_sceneOperation == null)
            {
                return null;
            }

            _sceneOperation.allowSceneActivation = true;
            ActivationOperation = _sceneOperation;
            return ActivationOperation;
        }

        public SceneReferenceUnloadOperation UnloadAsync(UnloadSceneOptions unloadOptions = UnloadSceneOptions.None, bool autoReleaseHandle = true)
        {
            return Unload(unloadOptions, autoReleaseHandle);
        }

        public SceneReferenceUnloadOperation Unload(UnloadSceneOptions unloadOptions = UnloadSceneOptions.None, bool autoReleaseHandle = true)
        {
#if SUPPORT_ADDRESABBLES
            if (IsAddressable)
            {
                if (!AddressablesLoadHandle.IsValid() || AddressablesLoadHandle.Status != AsyncOperationStatus.Succeeded)
                {
                    return SceneReferenceUnloadOperation.CreateInvalid(SceneReference);
                }

                return SceneReferenceUnloadOperation.CreateAddressable(
                    SceneReference,
                    Addressables.UnloadSceneAsync(AddressablesLoadHandle, unloadOptions, autoReleaseHandle)
                );
            }
#endif

            var scene = Scene;
            return SceneReferenceUnloadOperation.CreateSceneManager(
                SceneReference,
                scene.IsValid() ? SceneManager.UnloadSceneAsync(scene, unloadOptions) : null
            );
        }
    }

    /// <summary>
    /// Wraps an in-flight scene unload for both regular and Addressables-driven scenes.
    /// </summary>
    public sealed class SceneReferenceUnloadOperation : CustomYieldInstruction
    {
#if SUPPORT_ADDRESABBLES

        private SceneReferenceUnloadOperation(SceneReference sceneReference, AsyncOperationHandle<SceneInstance> addressableUnloadHandle)
        {
            SceneReference = sceneReference;
            AddressablesUnloadHandle = addressableUnloadHandle;
            IsAddressable = true;
        }

        internal static SceneReferenceUnloadOperation CreateAddressable(SceneReference sceneReference, AsyncOperationHandle<SceneInstance> addressableUnloadHandle)
        {
            return new SceneReferenceUnloadOperation(sceneReference, addressableUnloadHandle);
        }

        public bool IsAddressable { get; }
        public AsyncOperationHandle<SceneInstance> AddressablesUnloadHandle { get; }
#endif

        private SceneReferenceUnloadOperation(SceneReference sceneReference, AsyncOperation sceneOperation)
        {
            SceneReference = sceneReference;
            SceneOperation = sceneOperation;
        }

        internal static SceneReferenceUnloadOperation CreateSceneManager(SceneReference sceneReference, AsyncOperation sceneOperation)
        {
            return new SceneReferenceUnloadOperation(sceneReference, sceneOperation);
        }

        internal static SceneReferenceUnloadOperation CreateInvalid(SceneReference sceneReference)
        {
            return new SceneReferenceUnloadOperation(sceneReference, null);
        }

        public SceneReference SceneReference { get; }

        public override bool keepWaiting => !IsDone;

        public bool IsValid
        {
            get
            {
#if SUPPORT_ADDRESABBLES
                if (IsAddressable)
                {
                    return AddressablesUnloadHandle.IsValid();
                }
#endif
                return SceneOperation != null;
            }
        }

        public bool IsDone
        {
            get
            {
#if SUPPORT_ADDRESABBLES
                if (IsAddressable)
                {
                    return !AddressablesUnloadHandle.IsValid() || AddressablesUnloadHandle.IsDone;
                }
#endif
                return SceneOperation == null || SceneOperation.isDone;
            }
        }

        public bool HasFailed => OperationException != null;

        public float Progress
        {
            get
            {
#if SUPPORT_ADDRESABBLES
                if (IsAddressable)
                {
                    return AddressablesUnloadHandle.IsValid()
                        ? (AddressablesUnloadHandle.IsDone ? 1f : AddressablesUnloadHandle.PercentComplete)
                        : 0f;
                }
#endif

                if (SceneOperation == null)
                {
                    return 0f;
                }

                return SceneOperation.isDone ? 1f : SceneOperation.progress;
            }
        }

        public Exception OperationException
        {
            get
            {
#if SUPPORT_ADDRESABBLES
                if (IsAddressable && AddressablesUnloadHandle.IsValid())
                {
                    return AddressablesUnloadHandle.OperationException;
                }
#endif
                return null;
            }
        }

        public AsyncOperation SceneOperation { get; }
    }
}