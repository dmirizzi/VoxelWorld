using UnityEngine;

public class TorchController : MonoBehaviour
{
    public float BaseIntensity = .5f;

    public float FlickeringIntensityFraction = .2f;

    public float FlickeringFrequencyFactor = 8f;

    private Light _light;

    private Renderer _torchRenderer;

    private float _noiseSeed;

    void Start()
    {
        _light = GetComponentInChildren<Light>();
        _noiseSeed = Random.Range(0f, 10000f);
        _torchRenderer = GetComponent<MeshRenderer>();
    }

    void Update()
    {
        var intensity = BaseIntensity + Mathf.PerlinNoise(Time.time * FlickeringFrequencyFactor, _noiseSeed) * FlickeringIntensityFraction + (1 - FlickeringIntensityFraction);
        _light.intensity = intensity;
    }
}
