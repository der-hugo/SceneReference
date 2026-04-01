using System;
using UnityEngine;
using UnityEngine.SceneManagement;

#if SUPPORT_ADDRESABBLES
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
#endif

namespace derHugo.Unity.SceneReference
{
    /// <summary>
    /// Wraps an in-flight scene load so callers can observe progress and optionally trigger activation.
    /// </summary>
    public sealed class SceneReferenceLoadOperation : CustomYieldInstruction
    {
        private readonly SceneReference _sceneReference;
        private readonly bool _activateOnLoad;
        private readonly int _priority;
        private readonly Exception _operationException;
        private readonly AsyncOperation _sceneOperation;
        private AsyncOperation _activationOperation;

#if SUPPORT_ADDRESABBLES
        private readonly bool _isAddressable;
        private readonly AsyncOperationHandle<SceneInstance> _addressableLoadHandle;
#endif

        private SceneReferenceLoadOperation(SceneReference sceneReference, AsyncOperation sceneOperation, bool activateOnLoad, int priority)
        {
            _sceneReference = sceneReference;
            _sceneOperation = sceneOperation;
            _activateOnLoad = activateOnLoad;
            _priority = priority;
        }

        private SceneReferenceLoadOperation(SceneReference sceneReference, Exception operationException)
        {
            _sceneReference = sceneReference;
            _operationException = operationException;
        }

#if SUPPORT_ADDRESABBLES
        private SceneReferenceLoadOperation(SceneReference sceneReference, AsyncOperationHandle<SceneInstance> addressableLoadHandle, bool activateOnLoad, int priority)
        {
            _sceneReference = sceneReference;
            _addressableLoadHandle = addressableLoadHandle;
            _activateOnLoad = activateOnLoad;
            _priority = priority;
            _isAddressable = true;
        }
#endif

        internal static SceneReferenceLoadOperation CreateSceneManager(SceneReference sceneReference, AsyncOperation sceneOperation, bool activateOnLoad, int priority)
        {
            return new SceneReferenceLoadOperation(sceneReference, sceneOperation, activateOnLoad, priority);
        }

        internal static SceneReferenceLoadOperation CreateInvalid(SceneReference sceneReference, Exception operationException)
        {
            return new SceneReferenceLoadOperation(sceneReference, operationException);
        }

#if SUPPORT_ADDRESABBLES
        internal static SceneReferenceLoadOperation CreateAddressable(SceneReference sceneReference, AsyncOperationHandle<SceneInstance> addressableLoadHandle, bool activateOnLoad, int priority)
        {
            return new SceneReferenceLoadOperation(sceneReference, addressableLoadHandle, activateOnLoad, priority);
        }
#endif

        public SceneReference SceneReference => _sceneReference;
        public bool IsAddressable
        {
            get
            {
#if SUPPORT_ADDRESABBLES
                return _isAddressable;
#else
                return false;
#endif
            }
        }
        public bool ActivateOnLoad => _activateOnLoad;
        public int Priority => _priority;
        public override bool keepWaiting => !IsDone;

        public bool IsValid
        {
            get
            {
#if SUPPORT_ADDRESABBLES
                if (_isAddressable)
                {
                    return _addressableLoadHandle.IsValid() || _activationOperation != null;
                }
#endif
                return _sceneOperation != null || _activationOperation != null;
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

                return !IsAddressable && _sceneOperation == null && _activationOperation == null;
            }
        }

        public bool IsReadyForActivation
        {
            get
            {
                if (_activateOnLoad || _activationOperation != null)
                {
                    return false;
                }

#if SUPPORT_ADDRESABBLES
                if (_isAddressable)
                {
                    return _addressableLoadHandle.IsValid()
                        && _addressableLoadHandle.Status == AsyncOperationStatus.Succeeded
                        && _addressableLoadHandle.IsDone
                        && !IsLoaded;
                }
#endif

                return _sceneOperation != null && !_sceneOperation.isDone && _sceneOperation.progress >= 0.9f;
            }
        }

        public bool IsActivating => _activationOperation != null && !_activationOperation.isDone;

        public bool IsDone
        {
            get
            {
                if (_activationOperation != null)
                {
                    return _activationOperation.isDone;
                }

                if (!_activateOnLoad)
                {
#if SUPPORT_ADDRESABBLES
                    if (_isAddressable)
                    {
                        return (_addressableLoadHandle.IsValid() && _addressableLoadHandle.IsDone) || IsLoaded;
                    }
#endif
                    return _sceneOperation == null || IsReadyForActivation || IsLoaded;
                }

#if SUPPORT_ADDRESABBLES
                if (_isAddressable)
                {
                    return _addressableLoadHandle.IsValid() && _addressableLoadHandle.IsDone;
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
                if (_isAddressable)
                {
                    if (!_addressableLoadHandle.IsValid())
                    {
                        return 0f;
                    }

                    return _addressableLoadHandle.IsDone ? 1f : _addressableLoadHandle.PercentComplete;
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

                return _activateOnLoad ? _sceneOperation.progress : Mathf.Clamp01(_sceneOperation.progress / 0.9f);
            }
        }

        public float Progress
        {
            get
            {
                if (_activationOperation != null)
                {
                    return _activationOperation.isDone ? 1f : _activationOperation.progress;
                }

                if (IsReadyForActivation)
                {
                    return 0.9f;
                }

#if SUPPORT_ADDRESABBLES
                if (_isAddressable)
                {
                    return _addressableLoadHandle.IsValid() ? _addressableLoadHandle.PercentComplete : 0f;
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
                if (_isAddressable && _addressableLoadHandle.IsValid())
                {
                    return _addressableLoadHandle.OperationException;
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
                if (_isAddressable
                    && _addressableLoadHandle.IsValid()
                    && _addressableLoadHandle.Status == AsyncOperationStatus.Succeeded
                    && _addressableLoadHandle.IsDone)
                {
                    return _addressableLoadHandle.Result.Scene;
                }
#endif

                return string.IsNullOrWhiteSpace(_sceneReference.Path)
                    ? default
                    : SceneManager.GetSceneByPath(_sceneReference.Path);
            }
        }

        public AsyncOperation SceneOperation => _activationOperation ?? _sceneOperation;
        public AsyncOperation ActivationOperation => _activationOperation;

#if SUPPORT_ADDRESABBLES
        public AsyncOperationHandle<SceneInstance> AddressablesLoadHandle => _addressableLoadHandle;
#endif

        public AsyncOperation ActivateAsync()
        {
            if (_activationOperation != null)
            {
                return _activationOperation;
            }

            if (_activateOnLoad)
            {
                return SceneOperation;
            }

#if SUPPORT_ADDRESABBLES
            if (_isAddressable)
            {
                if (!_addressableLoadHandle.IsValid() || !_addressableLoadHandle.IsDone)
                {
                    return null;
                }

                if (_addressableLoadHandle.Status != AsyncOperationStatus.Succeeded)
                {
                    return null;
                }

                _activationOperation = _addressableLoadHandle.Result.ActivateAsync();
                if (_activationOperation != null)
                {
                    _activationOperation.priority = _priority;
                }

                return _activationOperation;
            }
#endif

            if (_sceneOperation == null)
            {
                return null;
            }

            _sceneOperation.allowSceneActivation = true;
            _activationOperation = _sceneOperation;
            return _activationOperation;
        }

        public SceneReferenceUnloadOperation UnloadAsync(UnloadSceneOptions unloadOptions = UnloadSceneOptions.None, bool autoReleaseHandle = true)
        {
            return Unload(unloadOptions, autoReleaseHandle);
        }

        public SceneReferenceUnloadOperation Unload(UnloadSceneOptions unloadOptions = UnloadSceneOptions.None, bool autoReleaseHandle = true)
        {
#if SUPPORT_ADDRESABBLES
            if (_isAddressable)
            {
                if (!_addressableLoadHandle.IsValid() || _addressableLoadHandle.Status != AsyncOperationStatus.Succeeded)
                {
                    return SceneReferenceUnloadOperation.CreateInvalid(_sceneReference);
                }

                return SceneReferenceUnloadOperation.CreateAddressable(
                    _sceneReference,
                    Addressables.UnloadSceneAsync(_addressableLoadHandle, unloadOptions, autoReleaseHandle)
                );
            }
#endif

            var scene = Scene;
            return SceneReferenceUnloadOperation.CreateSceneManager(
                _sceneReference,
                scene.IsValid() ? SceneManager.UnloadSceneAsync(scene, unloadOptions) : null
            );
        }
    }

    /// <summary>
    /// Wraps an in-flight scene unload for both regular and Addressables-driven scenes.
    /// </summary>
    public sealed class SceneReferenceUnloadOperation : CustomYieldInstruction
    {
        private readonly SceneReference _sceneReference;
        private readonly AsyncOperation _sceneOperation;

#if SUPPORT_ADDRESABBLES
        private readonly bool _isAddressable;
        private readonly AsyncOperationHandle<SceneInstance> _addressableUnloadHandle;
#endif

        private SceneReferenceUnloadOperation(SceneReference sceneReference, AsyncOperation sceneOperation)
        {
            _sceneReference = sceneReference;
            _sceneOperation = sceneOperation;
        }

#if SUPPORT_ADDRESABBLES
        private SceneReferenceUnloadOperation(SceneReference sceneReference, AsyncOperationHandle<SceneInstance> addressableUnloadHandle)
        {
            _sceneReference = sceneReference;
            _addressableUnloadHandle = addressableUnloadHandle;
            _isAddressable = true;
        }
#endif

        internal static SceneReferenceUnloadOperation CreateSceneManager(SceneReference sceneReference, AsyncOperation sceneOperation)
        {
            return new SceneReferenceUnloadOperation(sceneReference, sceneOperation);
        }

        internal static SceneReferenceUnloadOperation CreateInvalid(SceneReference sceneReference)
        {
            return new SceneReferenceUnloadOperation(sceneReference, null);
        }

#if SUPPORT_ADDRESABBLES
        internal static SceneReferenceUnloadOperation CreateAddressable(SceneReference sceneReference, AsyncOperationHandle<SceneInstance> addressableUnloadHandle)
        {
            return new SceneReferenceUnloadOperation(sceneReference, addressableUnloadHandle);
        }
#endif

        public SceneReference SceneReference => _sceneReference;
        public bool IsAddressable
        {
            get
            {
#if SUPPORT_ADDRESABBLES
                return _isAddressable;
#else
                return false;
#endif
            }
        }
        public override bool keepWaiting => !IsDone;

        public bool IsValid
        {
            get
            {
#if SUPPORT_ADDRESABBLES
                if (_isAddressable)
                {
                    return _addressableUnloadHandle.IsValid();
                }
#endif
                return _sceneOperation != null;
            }
        }

        public bool IsDone
        {
            get
            {
#if SUPPORT_ADDRESABBLES
                if (_isAddressable)
                {
                    return !_addressableUnloadHandle.IsValid() || _addressableUnloadHandle.IsDone;
                }
#endif
                return _sceneOperation == null || _sceneOperation.isDone;
            }
        }

        public bool HasFailed => OperationException != null;

        public float Progress
        {
            get
            {
#if SUPPORT_ADDRESABBLES
                if (_isAddressable)
                {
                    return _addressableUnloadHandle.IsValid()
                        ? (_addressableUnloadHandle.IsDone ? 1f : _addressableUnloadHandle.PercentComplete)
                        : 0f;
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
                if (_isAddressable && _addressableUnloadHandle.IsValid())
                {
                    return _addressableUnloadHandle.OperationException;
                }
#endif
                return null;
            }
        }

        public AsyncOperation SceneOperation => _sceneOperation;

#if SUPPORT_ADDRESABBLES
        public AsyncOperationHandle<SceneInstance> AddressablesUnloadHandle => _addressableUnloadHandle;
#endif
    }
}
