using UnityEngine;
using UnityEngine.UI;

/// <summary>Per-card material driven by the existing visual tilt and flip rotation.</summary>
[RequireComponent(typeof(Image))]
public class ShaderCode : MonoBehaviour
{
    public Editions edition = Editions.REGULAR;
    static readonly int Rotation = Shader.PropertyToID("_Rotation");
    Image _image;
    Material _instance;
    Material _source;

    void Start()
    {
        _image = GetComponent<Image>();
        _source = _image.material;
        if (_source == null || !_source.HasProperty(Rotation)) return;
        _instance = new Material(_source);
        _image.material = _instance;
        if (_instance.HasProperty("_EDITION"))
        {
            foreach (var keyword in _instance.enabledKeywords)
                if (keyword.name.StartsWith("_EDITION_")) _instance.DisableKeyword(keyword);
            _instance.EnableKeyword("_EDITION_" + edition.ToString().ToUpperInvariant());
        }
    }

    void LateUpdate()
    {
        if (_instance == null || transform.parent == null) return;
        var angles = transform.parent.localEulerAngles;
        float x = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.x), -90f, 90f) / 40f;
        float y = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.y), -90f, 90f) / 40f;
        _instance.SetVector(Rotation, new Vector4(x, y, 0f, 0f));
    }

    void OnDestroy()
    {
        if (_instance == null) return;
        if (_image != null) _image.material = _source;
        if (Application.isPlaying) Destroy(_instance);
        else DestroyImmediate(_instance);
    }
}
