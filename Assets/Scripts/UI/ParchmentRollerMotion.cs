using UnityEngine;
using UnityEngine.UI;

/// <summary>The roller silhouette stays fixed; its cylindrical surface rotates with paper travel.</summary>
public sealed class ParchmentRollerMotion : MonoBehaviour
{
    public ScrollRect scroll;
    public Image top, bottom;
    Material _top, _bottom, _topSource, _bottomSource;
    void OnEnable()
    {
        if (top != null) { _topSource = top.material; _top = new Material(_topSource); top.material = _top; }
        if (bottom != null) { _bottomSource = bottom.material; _bottom = new Material(_bottomSource); bottom.material = _bottom; }
    }
    void LateUpdate()
    {
        float travel = scroll != null && scroll.content != null ? scroll.content.anchoredPosition.y : 0;
        if (_top != null) _top.SetFloat("_Roll", travel / 90f);
        if (_bottom != null) _bottom.SetFloat("_Roll", -travel / 90f);
    }
    void OnDisable()
    {
        if (_top != null) { top.material = _topSource; Destroy(_top); }
        if (_bottom != null) { bottom.material = _bottomSource; Destroy(_bottom); }
    }
}
