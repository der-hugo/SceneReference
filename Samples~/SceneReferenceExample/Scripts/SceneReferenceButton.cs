using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace derHugo.Unity.SceneReference.Samples
{
    public class SceneReferenceButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _label;

        private SceneReference _sceneReference;

        public void Initialize(SceneReference sceneReference)
        {
            _sceneReference = sceneReference;
            
            var validState = _sceneReference.IsValidScene;
            _button.interactable = validState == SceneReference.ValidState.Valid;

            switch (validState)
            {
                case SceneReference.ValidState.Valid:
                    _label.text = $"Load {sceneReference.Name}";
                    _button.onClick.AddListener(OnClick);
                    break;
                case SceneReference.ValidState.InvalidUnassigned:
                    _label.text = $"<not assigned reference>";
                    break;
                case SceneReference.ValidState.InvalidNotInBuildSettings:
                    _label.text = $"{_sceneReference.Name} <not in build settings>";
                    break;
                case SceneReference.ValidState.InvalidAddressableKey:
                    _label.text = $"{_sceneReference.Name} <missing addressables key>";
                    break;
            }
        }

        private void OnClick()
        {
            StartCoroutine(HandleClickAsync());
        }

        private IEnumerator HandleClickAsync()
        {
            _button.interactable = false;

            if (_sceneReference.IsLoaded)
            {
                yield return _sceneReference.UnloadAsync(UnloadSceneOptions.UnloadAllEmbeddedSceneObjects);
            }
            else
            {
                yield return _sceneReference.LoadAsync(LoadSceneMode.Additive);
            }

            _button.interactable = true;
            
            _label.text = $"{(_sceneReference.IsLoaded ? "Unload" : "Load")} {_sceneReference.Name}";
        }
    }
}