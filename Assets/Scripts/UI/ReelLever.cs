using UnityEngine;

/// <summary>Mechanical pull follows the real reel state; never intercepts card input.</summary>
public class ReelLever : MonoBehaviour
{
    RectTransform _rt;
    SlotBatchManager _batch;
    float _pull;
    void Awake()
    {
        _rt = (RectTransform)transform;
        // Move the pivot without moving the graphic.
        var position = _rt.anchoredPosition;
        _rt.pivot = new Vector2(0.5f, 0.1f);
        _rt.anchoredPosition = position + new Vector2(_rt.rect.width * 0.5f, -_rt.rect.height * 0.9f);
    }
    void LateUpdate()
    {
        if (_batch == null) _batch = Object.FindAnyObjectByType<SlotBatchManager>();
        float target = _batch != null && _batch.IsRolling ? 1f : 0f;
        _pull = Mathf.MoveTowards(_pull, target, Time.deltaTime * 4f);
        _rt.localRotation = Quaternion.Euler(-35f * _pull, 0f, -16f * _pull);
    }
}
