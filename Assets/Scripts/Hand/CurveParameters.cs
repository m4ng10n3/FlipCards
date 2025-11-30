using UnityEngine;

[CreateAssetMenu(fileName = "CurveParameters", menuName = "Hand Curve Parameters")]
public class CurveParameters : ScriptableObject
{
    public AnimationCurve positioning;
    public float positioningInfluence = 0.1f;
    public AnimationCurve rotation;
    public float rotationInfluence = 10f;
    [HideInInspector] public int version;
#if UNITY_EDITOR
    private void OnValidate() { version++; }
#endif
}
