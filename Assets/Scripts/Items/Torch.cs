using UnityEngine;

public class Torch : MonoBehaviour, IPlayerHoldable
{
    public float BaseIntensity = .5f;

    public float FlickeringIntensityFraction = .2f;

    public float FlickeringFrequencyFactor = 8f;

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // IPlayerHoldable
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public Vector3 HoldingOffset { get; } = new Vector3( 0.9f, -1.0f, 1.5f );

    public Quaternion HoldingRotation { get; } = Quaternion.Euler( 0f, 0f, 350f );

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Controller
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void OnHold(PlayerController player)
    {
        // While the torch is being held, move the light source of the torch
        // to the player object, otherwise the light will clip into objects
        if(_lightTransform != null)
        {
            _lightTransform.parent = player.CameraTransform;
            _oldLightPos = _lightTransform.localPosition;
            _lightTransform.localPosition = Vector3.zero;
        }
    }

    public void OnRemove(PlayerController player)
    {
        if(_lightTransform != null)
        {
            // When player isnt holding torch anymore, move it back under the torch game object
            _lightTransform.parent = transform;     
            _lightTransform.localPosition = _oldLightPos;
        }
    }

    void Awake()
    {
        _light = GetComponentInChildren<Light>();
        if(_light != null)
        {
            _lightTransform = _light.gameObject.transform;
        }
        
        _noiseSeed = Random.Range(0f, 10000f);
        _torchRenderer = GetComponent<MeshRenderer>();
    }

    void Update()
    {
        if(_light != null)
        {
            var intensity = BaseIntensity + Mathf.PerlinNoise(Time.time * FlickeringFrequencyFactor, _noiseSeed) * FlickeringIntensityFraction + (1 - FlickeringIntensityFraction);
            _light.intensity = intensity;
        }
    }

    private Vector3 _oldLightPos;

    private Light _light;

    private Transform _lightTransform;

    private Renderer _torchRenderer;

    private float _noiseSeed;

}
