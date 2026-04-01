using UnityEngine;

namespace derHugo.Unity.SceneReference.Samples
{
    public class SceneReferenceButtonsController : MonoBehaviour
    {
        [SerializeField] private SceneReferenceButton _prefab;
        [SerializeField] private SceneReferenceExample _sceneReferenceExample;

        private void Awake()
        {
            foreach (var sceneReference in _sceneReferenceExample.SceneReferences)
            {
                var button = Instantiate(_prefab, transform);
                button.Initialize(sceneReference);
            }
        }
    }
}