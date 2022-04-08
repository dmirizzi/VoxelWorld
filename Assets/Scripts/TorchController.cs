using UnityEngine;

public class TorchController : MonoBehaviour, IPlayerHoldable
{
    public Vector3 HoldingOffset { get; } = new Vector3( 0.8f, -0.4f, 1.2f );

    public Quaternion HoldingRotation { get; } = Quaternion.Euler( 0f, 0f, 350f );

    public float BaseIntensity = .5f;

    public float FlickeringIntensityFraction = .2f;

    public float FlickeringFrequencyFactor = 8f;

    public void OnHold(GameObject playerObject)
    {
        // While the torch is being held, move the light source of the torch
        // to the player object, otherwise the light will clip into objects
        _lightTransform.parent = playerObject.transform;
        _oldLightPos = _lightTransform.localPosition;
        _lightTransform.localPosition = Vector3.zero;
    }

    public void OnRemove(GameObject playerObject)
    {
        // When player isnt holding torch anymore, move it back under the torch game object
        _lightTransform.parent = transform;     
        _lightTransform.localPosition = _oldLightPos;   
    }

    void Awake()
    {
        _light = GetComponentInChildren<Light>();
        _lightTransform = _light.gameObject.transform;
        _noiseSeed = Random.Range(0f, 10000f);
        _torchRenderer = GetComponent<MeshRenderer>();
    }

    void Update()
    {
        var intensity = BaseIntensity + Mathf.PerlinNoise(Time.time * FlickeringFrequencyFactor, _noiseSeed) * FlickeringIntensityFraction + (1 - FlickeringIntensityFraction);
        _light.intensity = intensity;
    }

    private Vector3 _oldLightPos;

    private Light _light;

    private Transform _lightTransform;

    private Renderer _torchRenderer;

    private float _noiseSeed;

}
