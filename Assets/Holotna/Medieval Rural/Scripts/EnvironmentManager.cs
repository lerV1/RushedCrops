using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Holotna
{
    #if UNITY_EDITOR
    public class EnvironmentManager : MonoBehaviour
    {
        [Header("Environment")]
        [SerializeField] private Time _time;
        [SerializeField] private Wind _wind;

        [Header("Time")]
        [SerializeField] private TimeScriptableObject _morning;
        [SerializeField] private TimeScriptableObject _afternoon;
        [SerializeField] private TimeScriptableObject _evening;
        [SerializeField] private TimeScriptableObject _night;
        private TimeScriptableObject _currentTime;
        private Time _previousTime;
        private enum Time { Morning, Afternoon, Evening, Night };

        [Header("Wind")]
        [SerializeField] private List<WindScriptableObject> _affectedObjects;
        private Wind _previousWind;
        private enum Wind { Off, Calm, Breeze, Storm };

        [Header("Misc")]
        [SerializeField] private Light _light;
        [SerializeField] private ReflectionProbe _reflection;

        private void OnValidate()
        {
            if (_time != _previousTime)
            {
                switch (_time)
                {
                    case Time.Morning:
                        _currentTime = _morning;
                        break;
                    case Time.Afternoon:
                        _currentTime = _afternoon;
                        break;
                    case Time.Evening:
                        _currentTime = _evening;
                        break;
                    case Time.Night:
                        _currentTime = _night;
                        break;
                }

                if (_light != null)
                {
                    _light.transform.rotation = _currentTime.lightRotation;
                    _light.color = _currentTime.lightColor;
                    _light.intensity = _currentTime.lightIntensity;
                    _light.shadowStrength = _currentTime.shadowStrength;
                }
                if (_reflection != null)
                {
                    _reflection.enabled = false;
                    _reflection.enabled = true;
                }
                RenderSettings.skybox = _currentTime.skybox;
                RenderSettings.ambientIntensity = _currentTime.skyboxIntensity;
                _currentTime.skybox.SetFloat("_Rotation", _currentTime.skyboxRotation);
                RenderSettings.fogColor = _currentTime.fogColor;
                RenderSettings.fogStartDistance = _currentTime.fogStart;
                RenderSettings.fogEndDistance = _currentTime.fogEnd;
                Lightmapping.lightingDataAsset = _currentTime.lightingData;

                _previousTime = _time;
            }

            if (_wind != _previousWind)
            {
                float windEnabled = 1f;
                if (_wind == Wind.Off)
                {
                    windEnabled = 0f;
                }

                int windState = (int)_wind;

                for (int i = 0; i < _affectedObjects.Count; i++)
                {
                    for (int j = 0; j < _affectedObjects[i].materials.Count; j++)
                    {
                        _affectedObjects[i].materials[j].SetFloat("_Wind", windEnabled);

                        _affectedObjects[i].materials[j].SetFloat("_Bend_Strength", _affectedObjects[i].windStates[windState].bendStrength);
                        _affectedObjects[i].materials[j].SetFloat("_Bend_Speed", _affectedObjects[i].windStates[windState].bendSpeed);
                        _affectedObjects[i].materials[j].SetFloat("_Bend_Distortion", _affectedObjects[i].windStates[windState].bendDistortion);
                        _affectedObjects[i].materials[j].SetVector("_Bend_Direction", _affectedObjects[i].windStates[windState].bendDirection);
                        _affectedObjects[i].materials[j].SetFloat("_Wiggle_Strength", _affectedObjects[i].windStates[windState].wiggleStrength);
                        _affectedObjects[i].materials[j].SetFloat("_Wiggle_Speed", _affectedObjects[i].windStates[windState].wiggleSpeed);
                    }
                }

                _previousWind = _wind;
            }
        }
    }
    #endif
}