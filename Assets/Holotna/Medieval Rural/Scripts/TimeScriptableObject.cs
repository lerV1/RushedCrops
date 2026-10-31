using UnityEditor;
using UnityEngine;

namespace Holotna
{
    #if UNITY_EDITOR
    [CreateAssetMenu(fileName = "Time", menuName = "Scriptable Objects/Time", order = 1)]
    public class TimeScriptableObject : ScriptableObject
    {
        [Header("Directional Light")]
        public Quaternion lightRotation;
        public Color lightColor;
        public float lightIntensity;
        public float shadowStrength;

        [Header("Skybox")]
        public Material skybox;
        public float skyboxRotation;
        public float skyboxIntensity;

        [Header("Fog")]
        public Color fogColor;
        public float fogStart;
        public float fogEnd;

        [Header("Baked Lighting")]
        public LightingDataAsset lightingData;
    }
    #endif
}