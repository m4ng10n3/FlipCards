using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Delivered final sprites with live values. Owned by CardOverlay's disposable chrome.</summary>
public sealed class FinalCardInk : MonoBehaviour
{
    sealed class Face
    {
        public RectTransform root;
        public Image[] health, power, charges;
        public TextMeshProUGUI healthOverflow, powerOverflow;
        public string prefix;
    }

    Face _front, _back;
    int _health = int.MinValue, _power = int.MinValue, _charges = -1, _face = -1;
    static Sprite Sprite(string face, string name) => UiSkin.Sprite("final_" + face + "_" + name);

    static Image Stamp(RectTransform parent, string name, Sprite sprite, Vector2 center, Vector2 size)
    {
        var rt = UiBuild.Rect(name, parent);
        UiBuild.Band(rt, (center.x - size.x * .5f) * FinalCardLayout.Scale,
            (center.y - size.y * .5f) * FinalCardLayout.Scale, size.x * FinalCardLayout.Scale, size.y * FinalCardLayout.Scale);
        var image = UiBuild.Fill(rt, Color.white);
        image.sprite = sprite;
        image.enabled = sprite != null;
        return image;
    }

    static Vector2 InkSize(Sprite sprite, Vector2 envelope)
    {
        // Delivered symbols are padded 256px cells; size by the manifest's alpha envelope.
        if (sprite == null) return envelope;
        if (sprite.name == "attack_spade") return new Vector2(envelope.x * 256f / 144f, envelope.y * 256f / 192f);
        if (sprite.name == "defense_club_B") return new Vector2(envelope.x * 256f / 179f, envelope.y * 256f / 192f);
        return envelope * (256f / 182f); // charge ink alpha box: 37..219
    }

    public void Build(CardDefinition.Spec spec)
    {
        _front = BuildFace(true, spec);
        _back = BuildFace(false, spec);
        _back.root.gameObject.SetActive(false);
    }

    Face BuildFace(bool front, CardDefinition.Spec spec)
    {
        var face = new Face { prefix = front ? "front" : "back", root = UiBuild.Rect(front ? "FinalFront" : "FinalBack", transform) };
        UiBuild.Stretch(face.root);
        string family = spec.faction == Faction.A ? "sun" : spec.faction == Faction.B ? "moon" : "saturn";
        var centers = front ? FinalCardLayout.FrontFactions : FinalCardLayout.BackFactions;
        for (int i = 0; i < centers.Length; i++)
            Stamp(face.root, "Faction" + i, Sprite(face.prefix, "faction_" + family), centers[i], front ? FinalCardLayout.FrontFactionSize : FinalCardLayout.BackFactionSize);
        if (front)
        {
            Stamp(face.root, "ChargeBackplates", Sprite("front", "charge_backplates"), new Vector2(512, 768), new Vector2(1024, 1536));
            Stamp(face.root, "StarTop", Sprite("front", "star"), new Vector2(248, 73), new Vector2(60, 60));
            Stamp(face.root, "StarBottom", Sprite("front", "star"), new Vector2(776, 1463), new Vector2(60, 60));
        }
        else BuildBanner(face.root, spec);
        face.health = Column(face, "Health", front ? FinalCardLayout.FrontHealth : FinalCardLayout.BackHealth,
            front ? FinalCardLayout.FrontHealthSize : FinalCardLayout.BackHealthSize, "drop_empty");
        face.power = Column(face, "Power", front ? FinalCardLayout.FrontPower : FinalCardLayout.BackPower,
            front ? FinalCardLayout.FrontPowerSize : FinalCardLayout.BackPowerSize, front ? "attack_empty" : "defense_empty");
        centers = front ? FinalCardLayout.FrontCharges : FinalCardLayout.BackCharges;
        face.charges = new Image[centers.Length];
        for (int i = 0; i < centers.Length; i++)
            face.charges[i] = Stamp(face.root, "Charge" + i, Sprite(face.prefix, "charge_ring"), centers[i],
                InkSize(Sprite(face.prefix, "charge_ring"), Vector2.one * (front ? 32f : 38f)));
        // Preserve exact values beyond the seven delivered slots, instead of silently clipping bonuses.
        face.healthOverflow = Overflow(face, "HealthOverflow", front ? 3f : 23f);
        face.powerOverflow = Overflow(face, "PowerOverflow", front ? 190f : 180f);
        return face;
    }

    static Image[] Column(Face face, string name, Vector2[] centers, Vector2 size, string empty)
    {
        var images = new Image[centers.Length];
        for (int i = 0; i < images.Length; i++) images[i] = Stamp(face.root, name + i, Sprite(face.prefix, empty), centers[i], size);
        return images;
    }

    static TextMeshProUGUI Overflow(Face face, string name, float x)
    {
        var label = UiBuild.Text(name, face.root, "", 14, face.prefix == "front" ? GamePalette.Ink : GamePalette.Paper, TextAlignmentOptions.Center);
        UiBuild.Band(label.rectTransform, x, 307f, 30f, 16f);
        label.enabled = false;
        return label;
    }

    static void BuildBanner(RectTransform parent, CardDefinition.Spec spec)
    {
        bool both = spec.backDamageBonusSameFaction > 0 && spec.backBlockBonusSameFaction > 0;
        if (spec.backDamageBonusSameFaction > 0) Banner(parent, "AttackBanner", "attack_spade", spec.backDamageBonusSameFaction, both ? -125f : 0f);
        if (spec.backBlockBonusSameFaction > 0) Banner(parent, "DefenseBanner", "defense_club_B", spec.backBlockBonusSameFaction, both ? 125f : 0f);
    }

    static void Banner(RectTransform parent, string name, string key, int value, float offset)
    {
        var sprite = Sprite("back", key);
        Stamp(parent, name, sprite, new Vector2(466 + offset, 146), InkSize(sprite, new Vector2(80, 108)));
        var label = UiBuild.Text(name + "Value", parent, value.ToString(), 140f * FinalCardLayout.Scale, GamePalette.Paper, TextAlignmentOptions.Center);
        UiBuild.Band(label.rectTransform, (516 + offset) * FinalCardLayout.Scale, 76f * FinalCardLayout.Scale, 100f * FinalCardLayout.Scale, 140f * FinalCardLayout.Scale);
    }

    public void Refresh(bool front, int health, int power, int charges)
    {
        if (_face == (front ? 1 : 0) && _health == health && _power == power && _charges == charges) return;
        _face = front ? 1 : 0; _health = health; _power = power; _charges = charges;
        _front.root.gameObject.SetActive(front);
        _back.root.gameObject.SetActive(!front);
        var face = front ? _front : _back;
        for (int i = 0; i < 7; i++)
        {
            face.health[i].sprite = Sprite(face.prefix, i < health ? "drop_full" : "drop_empty");
            bool filled = front ? 6 - i < power : i < power;
            face.power[i].sprite = Sprite(face.prefix, (front ? "attack_" : "defense_") + (filled ? "full" : "empty"));
        }
        for (int i = 0; i < face.charges.Length; i++) face.charges[i].sprite = Sprite(face.prefix, i < charges ? "charge_full" : "charge_ring");
        face.healthOverflow.enabled = health > 7; face.healthOverflow.text = health > 7 ? health.ToString() : "";
        face.powerOverflow.enabled = power > 7; face.powerOverflow.text = power > 7 ? power.ToString() : "";
    }
}
