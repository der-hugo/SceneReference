using System;
using derHugo.Unity.SceneReference;
using UnityEngine;

namespace derHugo.Unity.SceneReference.Samples
{
    public class SceneReferenceExample : ScriptableObject
    {
        [SerializeField]
        private SceneReference _singleSceneReference;

        [SerializeField]
        private SceneReference[] _sceneReferenceList;

        [SerializeField] private NestingExample _nestingExample;
        
        [Serializable]
        private class NestingExample
        {
            [SerializeField] private SceneReference _nestedSceneReference;
        }
    }
}
