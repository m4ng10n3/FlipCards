using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mette un simbolo di <see cref="GlyphSprites"/> su un'Image, a runtime.
///
/// PERCHE' UN COMPONENTE E NON LO SPRITE DIRETTO: i glifi sono texture
/// costruite in memoria con <c>HideFlags.HideAndDontSave</c>, quindi non si
/// possono serializzare. Il builder del layout gira nell'editor e salva la
/// scena: uno sprite assegnato la' dentro sparisce al primo domain reload e in
/// Play resta un'Image senza sprite, che Unity disegna come un rettangolo
/// pieno. E' esattamente cosi' che la legenda mostrava due quadrati bianchi al
/// posto della spada e dello scudo.
///
/// Quello che si salva nella scena e' invece questo componente col suo enum:
/// un dato serializzabile, che a Play riassegna lo sprite vero.
/// </summary>
[RequireComponent(typeof(Image))]
[DisallowMultipleComponent]
public class GlyphIcon : MonoBehaviour
{
    public GlyphSprites.Kind kind = GlyphSprites.Kind.Sword;

    void Awake() => Apply();
    void OnEnable() => Apply();

    void Apply()
    {
        var img = GetComponent<Image>();
        if (img == null) return;

        img.sprite = GlyphSprites.Of(kind);
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
    }
}
