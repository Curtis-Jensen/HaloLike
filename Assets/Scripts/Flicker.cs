using UnityEngine;

// Flickering firelight: wobbles the light intensity and the flame cube.
public class Flicker : MonoBehaviour
{
    Light l; float baseIntensity, seed;
    void Start() { l = GetComponentInChildren<Light>(); if (l) baseIntensity = l.intensity; seed = Random.value * 100f; }
    void Update()
    {
        if (l) l.intensity = baseIntensity * (0.7f + 0.6f * Mathf.PerlinNoise(Time.time * 6f, seed));
        transform.localScale = new Vector3(1f, 0.8f + 0.4f * Mathf.PerlinNoise(seed, Time.time * 5f), 1f);
    }
}
