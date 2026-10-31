using System;
using System.Collections.Generic;
using UnityEngine;

namespace Holotna
{
    #if UNITY_EDITOR
    [CreateAssetMenu(fileName = "Wind", menuName = "Scriptable Objects/Wind", order = 2)]
    public class WindScriptableObject : ScriptableObject
    {
        [Header("Wind")]
        public List<WindState> windStates = new List<WindState>();
        [Serializable]
        public struct WindState
        {
            public string name;
            public float bendStrength;
            public float bendSpeed;
            public float bendDistortion;
            public Vector3 bendDirection;
            public float wiggleStrength;
            public float wiggleSpeed;
        }

        [Header("Materials")]
        public List<Material> affectedMaterials;
        public List<Material> materials
        {
            get { return affectedMaterials; }
            set { affectedMaterials = value; }
        }
    }
    #endif
}