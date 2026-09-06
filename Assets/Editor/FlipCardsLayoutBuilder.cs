using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Costruisce il layout di gioco descritto in LAYOUT_SPEC.md.
///
/// E' uno script e non un lavoro a mano perche' le misure sono un sistema: la
/// colonna corsia, il gap che ospita i connettori di combo e le bande verticali
/// devono restare coerenti fra loro. Rilanciarlo ricostruisce tutto dagli stessi
/// numeri.
///
/// **Da dove vengono i numeri.** Non sono inventati: sono `layouts.board` di
/// `flipcards_ui_manifest.json` (kit Arcade Horror CRT) moltiplicati per 2, cioe'
/// il tabellone 960x540 del kit portato sul canvas 1920x1080. Lo stesso file
/// fornisce `board_bg`, un fondo 1920x1080 con gia' disegnati i pozzetti di ogni
/// zona: se le nostre bande e le sue coincidono, il tabellone si presenta come il
/// preview del kit. Se le fai divergere, i contenuti finiscono accanto ai
/// pozzetti invece che dentro.
///
/// Due fasi indipendenti:
///  1. i prefab di carte e slot vengono portati alla dimensione della cella;
///  2. il Canvas viene ricostruito a bande e ricablato su GameManager/HandManager.
/// </summary>
public static class FlipCardsLayoutBuilder
{
    // ── Misure (LAYOUT_SPEC §6.2 / §6.3 / §6.4) ───────────────────────────────

    const float RefW = 1920f, RefH = 1080f;

    // Tre colonne: rail del giocatore, campo, colonna destra.
    const float RailX = 12f, RailY = 12f, RailW = 294f, RailH = 1056f;
    const float FieldX = 316f, FieldW = 1178f;
    const float SideX = 1504f, SideContentW = 400f;

    // Celle. La carta e' verticale, la casella nemica orizzontale: il fronte
    // nemico e' un rullo da slot machine, non una fila di carte, e la forma e'
    // cio' che lo dice prima di qualunque etichetta.
    const float CardW = CardOverlay.CardW, CardH = CardOverlay.CardH;   // 224 x 336
    const float SlotW = SlotOverlay.CellW, SlotH = SlotOverlay.CellH;   // 352 x 288

    // Passo di corsia unico per i due lati: le colonne del rullo e le corsie del
    // giocatore devono stare sugli stessi centri (508 / 904 / 1300 sul canvas),
    // o l'asse dei pronostici punterebbe fra due corsie.
    const float LanePitch = 396f;
    const float PlayerLaneGap = LanePitch - CardW;   // 172
    const float EnemyLaneGap = LanePitch - SlotW;    // 44
    const int Lanes = 3;

    static float PlayerBoardW => Lanes * CardW + (Lanes - 1) * PlayerLaneGap;   // 1016
    static float EnemyBoardW => Lanes * SlotW + (Lanes - 1) * EnemyLaneGap;     // 1144

    // Bande del campo, in coordinate canvas (il rect Field parte a x = FieldX ma
    // e' alto quanto lo schermo, quindi la y di banda e' gia' quella del canvas).
    const float TurnPlateW = 400f, TopPlateY = 12f, TopPlateH = 48f;
    const float PhaseX = 544f, PhaseW = 634f;
    const float BossY = 68f, BossH = 56f;
    const float ReelHousingY = 132f, ReelHousingH = 400f;
    const float EnemyY = 188f;
    const float PaylineY = 332f;
    const float AxisY = 528f, AxisH = 48f;
    const float PlayerY = 580f;

    // Mano. A riposo il centro della carta sta SOTTO il bordo basso: si vede solo
    // la fascia alta, cioe' la linguetta. All'ingresso del puntatore la mano sale
    // in blocco e si vede intera.
    const float HandDockY = 924f;
    const float HandRestY = -28f;
    const float HandRaisedY = 208f;
    const float HandRestH = RefH - HandDockY;   // 156
    const float HandRootW = 1148f;

    // Il passo della mano e' PIU' STRETTO della carta: le carte in mano si
    // sovrappongono, ed e' voluto. 8 linguette da 132 coprono 1148, gli stessi
    // hand_tab_slots del kit. La leggibilita' la danno l'arco della spline, la
    // rotazione a ventaglio e il pop-out della carta sotto il puntatore, non lo
    // spazio fra una carta e l'altra.
    const int MaxHandCards = 8;
    const float HandSpacing = 132f;

    // Rail del giocatore, in coordinate relative al rail.
    const float RailHpY = 56f, RailHpH = 46f;
    const float RailApY = 108f, RailApH = 38f;
    const float RailCostY = 152f, RailCostH = 34f;
    // Mazzo e legenda cadono nei due pozzetti a forma di carta che board_bg
    // disegna nel rail (`deck_slot` e `discard_slot` del manifest). Spostarli
    // senza spostare il fondo si vede subito: la pila finisce accanto al riquadro.
    const float RailDeckLabelY = 250f, RailDeckLabelH = 24f;
    const float RailDeckX = 12f, RailDeckY = 280f, RailDeckW = 256f, RailDeckH = 368f;
    const float RailDeckHintY = 652f, RailDeckHintH = 34f;
    const float RailLegendY = 688f, RailLegendH = 368f;

    // Colonna destra, relativa al rect Side.
    const float HeaderY = 12f, HeaderH = 52f;
    const float InspectorY = 72f, InspectorH = 528f;
    const float LogY = 608f, LogH = 304f;
    const float CommandsY = 920f, CommandH = 64f, CommandGap = 20f;

    // Fondo e overlay CRT del kit. Assenti (kit non importato) si degrada a
    // tinte piatte: il layout resta quello, cambia solo la pelle.
    const string KitRoot = "Assets/Graphics/FlipCards_ArcadeHorrorUI/ArcadeHorrorUI/2x";

    static Sprite _boardBg;
    static bool HasBackdrop => _boardBg != null;

    [MenuItem("FlipCards/Ricostruisci layout di gioco")]
    public static void Rebuild()
    {
        _boardBg = KitSprite("board/board_bg");

        ResizePrefabs();
        BuildScene();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[Layout] Ricostruito sul tabellone del kit: rail 294, campo 1178, colonna destra 400; " +
                  $"carte {CardW}x{CardH}, caselle {SlotW}x{SlotH}, passo di corsia {LanePitch}. " +
                  (HasBackdrop ? "Fondo board_bg del kit attivo." : "board_bg non trovato: fondi a tinta piatta."));
    }

    static Sprite KitSprite(string relativePath)
        => AssetDatabase.LoadAssetAtPath<Sprite>($"{KitRoot}/{relativePath}.png");

    // ══════════════════════════════════════════════════════════════════════════
    //  1. Prefab alla dimensione della cella
    // ══════════════════════════════════════════════════════════════════════════

    static void ResizePrefabs()
    {
        foreach (var path in PrefabPaths("Assets/Prefabs/CardsPrefab"))
            ResizeCardPrefab(path);

        foreach (var path in PrefabPaths("Assets/Prefabs/SlotsPrefab"))
            ResizeSlotPrefab(path);

        // EmptySpot ed EmptySlot devono avere lo stesso rect di cio' che
        // sostituiscono, altrimenti le corsie saltano a ogni morte. Le due misure
        // sono diverse: la carta e' verticale, la casella nemica orizzontale.
        var gm = Object.FindAnyObjectByType<GameManager>();
        if (gm != null)
        {
            ResizePlaceholder(gm.EmptySpot, CardW, CardH, "slot/card_slot_empty");
            ResizePlaceholder(gm.EmptySlot, SlotW, SlotH, "slot/enemy_slot_empty");
        }

        AssetDatabase.SaveAssets();
    }

    static IEnumerable<string> PrefabPaths(string folder)
    {
        if (!Directory.Exists(folder)) yield break;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            yield return AssetDatabase.GUIDToAssetPath(guid);
    }

    static void ResizeCardPrefab(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var rt = root.transform as RectTransform;
            if (rt == null) return;

            ScaleTree(rt, CardW, CardH);
            SetLayoutElement(root, CardW, CardH);
            SilenceChildRaycasts(root);

            // L'ombra sta DIETRO la carta: in Screen Space - Camera la z decide
            // l'ordine fra il sub-canvas "Visual" e i fratelli. Partire da z
            // negativa la faceva lampeggiare davanti per qualche frame.
            var cardShadow = root.transform.Find("Shadow");
            if (cardShadow != null)
                cardShadow.localPosition = new Vector3(cardShadow.localPosition.x, cardShadow.localPosition.y, 1f);

            var view = root.GetComponentInChildren<CardView>(true);
            if (view != null)
            {
                var cell = (RectTransform)view.transform;

                // Bande della cella carta: le costanti stanno in CardOverlay, che
                // e' anche chi disegna i fondi. Un solo posto per questi numeri.
                //
                // Il Template resta a tutta cella: e' la cornice, ed e' anche il
                // Graphic su cui gira CardShaderGraph. Comprimerlo per far posto
                // ai chip vorrebbe dire rimpicciolire lo shader.
                Place(cell, "Template", 0f, 0f, CardW, CardH);
                Place(cell, "Name", CardOverlay.NameX + 10f, CardOverlay.NameY + 4f,
                                    CardOverlay.NameW - 44f, CardOverlay.NameH - 8f);
                Place(cell, "imagecharacter", CardOverlay.ArtX, CardOverlay.ArtY,
                                              CardOverlay.ArtW, CardOverlay.ArtH);

                ApplyCardTemplate(root, cell);
                var portrait = cell.Find("imagecharacter")?.GetComponent<Image>();
                if (portrait != null) portrait.preserveAspect = true;

                // Due caselle statistica, non tre, e la prima cambia con la
                // faccia: ATK in Fronte, BLOCCO in Retro. I due Text stanno nello
                // stesso posto e CardView ne accende uno solo, quello che serve da
                // quel lato. Il numero parte dopo l'icona del badge del kit.
                Place(cell, "FrontDamage", CardOverlay.StatTextX(0), CardOverlay.StatY,
                                           CardOverlay.StatTextW, CardOverlay.StatH);
                Place(cell, "BackBlock", CardOverlay.StatTextX(0), CardOverlay.StatY,
                                         CardOverlay.StatTextW, CardOverlay.StatH);
                Place(cell, "HP", CardOverlay.StatTextX(1), CardOverlay.StatY,
                                  CardOverlay.StatTextW, CardOverlay.StatH);

                StyleText(cell, "Name", 20, TextAnchor.MiddleLeft, GamePalette.TextPrimary);
                StyleText(cell, "FrontDamage", 26, TextAnchor.MiddleCenter, GamePalette.Danger);
                StyleText(cell, "HP", 22, TextAnchor.MiddleCenter, GamePalette.PlayerHp);
                StyleText(cell, "BackBlock", 26, TextAnchor.MiddleCenter, GamePalette.Retro);

                // Fazione e lato li disegna CardOverlay come badge e fascia colorata.
                // I Text del prefab restano (CardView li scrive e usa sideText per
                // riconoscere il flip) ma diventano invisibili.
                Hide(cell, "Faction");
                Hide(cell, "Side");

                PlaceHint(cell, CardH * 0.5f - CardOverlay.ArtY - CardOverlay.ArtH * 0.5f, GamePalette.Fronte);
                TuneCardView(view);

                if (view.GetComponent<CardOverlay>() == null)
                    view.gameObject.AddComponent<CardOverlay>();
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// <summary>
    /// Le due facce della carta, prese dal kit e scritte nel prefab.
    ///
    /// **E' il template a dire quale faccia sia**: <c>card_front_{fazione}</c> ha
    /// la finestra del ritratto aperta, <c>card_back_{fazione}</c> e' cieca e
    /// CardOverlay ci stampa sopra il sigillo. Sulla cella non c'e' nessuna
    /// scritta che dichiari il lato, ed e' voluto: il lato di una carta e' quello
    /// che si vede girandola.
    ///
    /// Vanno scritti nel prefab e non a runtime perche' <c>CardView.Init</c> legge
    /// il fronte dallo sprite del Template in quel momento; <c>backImage</c> e' un
    /// campo privato serializzato, quindi passa da SerializedObject.
    /// </summary>
    static void ApplyCardTemplate(GameObject root, RectTransform cell)
    {
        var definition = root.GetComponentInChildren<CardDefinition>(true);
        if (definition == null) return;

        var front = KitSprite($"card/card_front_{definition.faction}");
        var back = KitSprite($"card/card_back_{definition.faction}");
        if (front == null && back == null) return;

        var template = cell.Find("Template");
        var image = template != null ? template.GetComponent<Image>() : null;
        if (image != null && front != null)
        {
            image.sprite = front;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            // La tinta la porta lo sprite: un colore diverso da bianco lo
            // spegnerebbe, e FlashTemplate ci rientra sopra a ogni reazione.
            image.color = Color.white;
        }

        if (back == null) return;

        var view = root.GetComponentInChildren<CardView>(true);
        if (view == null) return;

        var so = new SerializedObject(view);
        var property = so.FindProperty("backImage");
        if (property != null) property.objectReferenceValue = back;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Numeri di regia della carta in mano. Stanno qui e non nell'Inspector
    /// perche' dipendono dal layout: <c>handHoverLift</c> era 190, tarato su una
    /// mano che stava altrove, e con il dock in basso spediva la carta sotto il
    /// puntatore fin dentro le corsie. Il sollevamento e' solo una parte del
    /// pop-out — le altre sono la scala, il raddrizzamento del ventaglio e il
    /// sorting, e stanno in CardView.
    /// </summary>
    static void TuneCardView(CardView view)
    {
        var so = new SerializedObject(view);
        SetFloat(so, "handHoverLift", 80f);
        SetFloat(so, "scaleOnHover", 1.18f);
        SetFloat(so, "scaleOnSelect", 1.26f);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetFloat(SerializedObject so, string property, float value)
    {
        var p = so.FindProperty(property);
        if (p != null) p.floatValue = value;
        else Debug.LogWarning($"[Layout] campo '{property}' non trovato su {so.targetObject.GetType().Name}");
    }

    static void ResizeSlotPrefab(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var rt = root.transform as RectTransform;
            if (rt == null) return;

            ScaleTree(rt, SlotW, SlotH);
            SetLayoutElement(root, SlotW, SlotH);
            SilenceChildRaycasts(root);

            var view = root.GetComponent<SlotView>();
            if (view != null)
            {
                // Letto in Awake per il LayoutElement: se resta al vecchio valore
                // l'HorizontalLayoutGroup impagina corsie di larghezza sbagliata.
                view.preferredSize = new Vector2(SlotW, SlotH);
                EditorUtility.SetDirty(view);
            }

            // Fondo della cella: SlotView.Blink lo colora di giallo e lo rimette,
            // quindi il colore base va impostato qui. Resta un fondo scuro e non
            // diventa la cornice del kit: la cornice deve stare SOPRA il simbolo
            // (la sua finestra e' trasparente) e l'Image della radice disegna per
            // prima. La monta SlotOverlay come figlio.
            var bg = root.GetComponent<Image>();
            if (bg != null) bg.color = GamePalette.WithAlpha(GamePalette.Panel, 0.88f);

            var cell = (RectTransform)root.transform;

            // Bande della cella del rullo: le costanti stanno in SlotOverlay.
            // Il figlio "Sprite" resta quadrato e centrato: il reel ne copia il
            // rect e al reveal l'immagine non deve cambiare dimensione.
            Place(cell, "Name", SlotOverlay.NameX, SlotOverlay.NameY, SlotOverlay.NameW, SlotOverlay.NameH);
            Place(cell, "Sprite", SlotOverlay.ArtX, SlotOverlay.ArtY, SlotOverlay.ArtSize, SlotOverlay.ArtSize);
            var symbol = cell.Find("Sprite")?.GetComponent<Image>();
            if (symbol != null) symbol.preserveAspect = true;
            Place(cell, "HP", SlotOverlay.ChipTextX(1), SlotOverlay.ChipY,
                              SlotOverlay.ChipTextW, SlotOverlay.ChipH);
            Place(cell, "Def", SlotOverlay.ChipTextX(2), SlotOverlay.ChipY,
                               SlotOverlay.ChipTextW, SlotOverlay.ChipH);

            StyleText(cell, "Name", 19, TextAnchor.MiddleLeft, GamePalette.TextPrimary);
            StyleText(cell, "HP", 20, TextAnchor.MiddleCenter, GamePalette.PlayerHp);
            StyleText(cell, "Def", 20, TextAnchor.MiddleCenter, GamePalette.Retro);

            // La fazione la disegna SlotOverlay come badge colorato, uguale a
            // quello delle carte: il Text del prefab conteneva un valore fisso.
            Hide(cell, "Faction");

            PlaceHint(cell, SlotH * 0.5f - SlotOverlay.ArtY - SlotOverlay.ArtSize * 0.5f, GamePalette.Danger);

            if (root.GetComponent<SlotOverlay>() == null)
                root.AddComponent<SlotOverlay>();

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// <summary>
    /// L'hint galleggia SOPRA la cella in z, sovrapposto all'artwork, e non nel
    /// flusso: accodato dentro il rect originale mostrava una riga e troncava il
    /// resto, e fuori dalla cella finirebbe addosso all'asse delle corsie.
    /// </summary>
    static void PlaceHint(RectTransform cell, float dy, Color color)
    {
        var hint = cell.Find("HintText") as RectTransform;
        if (hint == null) return;

        hint.anchorMin = hint.anchorMax = new Vector2(0.5f, 0.5f);
        hint.pivot = new Vector2(0.5f, 0.5f);
        hint.sizeDelta = new Vector2(cell.rect.width, 40f);
        hint.anchoredPosition = new Vector2(0f, dy);

        var text = hint.GetComponent<Text>();
        if (text == null) return;

        text.resizeTextForBestFit = false;
        text.fontSize = 22;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.fontStyle = FontStyle.Bold;
        text.color = color;

        var shadow = text.GetComponent<Shadow>() ?? text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
        shadow.effectDistance = new Vector2(2f, -2f);
    }

    static void ResizePlaceholder(GameObject placeholder, float w, float h, string kitSprite)
    {
        if (placeholder == null) return;

        string path = AssetDatabase.GetAssetPath(placeholder);
        if (string.IsNullOrEmpty(path))
        {
            // Oggetto di scena: si modifica direttamente.
            ApplyPlaceholder(placeholder, w, h, kitSprite);
            EditorUtility.SetDirty(placeholder);
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            ApplyPlaceholder(root, w, h, kitSprite);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void ApplyPlaceholder(GameObject go, float w, float h, string kitSprite)
    {
        var rt = go.transform as RectTransform;
        if (rt == null) return;

        ScaleTree(rt, w, h);
        SetLayoutElement(go, w, h);

        var sprite = KitSprite(kitSprite);
        var existingFrame = go.transform.Find("_Frame");

        var outline = go.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = GamePalette.Fronte;
            outline.effectDistance = new Vector2(5f, -5f);
            outline.useGraphicAlpha = false;
            outline.enabled = sprite == null;
        }

        // Una corsia vuota e' una falla: la casella carica colpisce direttamente
        // gli HP. La casella deve leggersi, non essere un rettangolo al 7%.
        var img = go.GetComponent<Image>();
        if (img != null)
        {
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Simple;
                img.color = Color.white;
            }
            else
            {
                img.sprite = null;
                img.color = new Color(1f, 1f, 1f, 0.045f);
            }
        }

        if (sprite != null)
        {
            // Il pozzetto del kit ha gia' la sua cornice: sommarne una seconda la
            // raddoppia. La striscia costruita da un rebuild precedente va via.
            if (existingFrame != null) Object.DestroyImmediate(existingFrame.gameObject);
            return;
        }

        if (existingFrame != null) return;

        // Cornice a quattro strisce: un Outline su un'Image trasparente non
        // disegnerebbe nulla, perche' duplica i vertici del grafico che decora.
        var frame = UiBuild.Rect("_Frame", go.transform);
        UiBuild.Stretch(frame);

        var edge = GamePalette.WithAlpha(GamePalette.Border, 0.85f);
        Edge(frame, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -3f), edge);
        Edge(frame, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 3f), edge);
        Edge(frame, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(3f, 0f), edge);
        Edge(frame, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(3f, 0f), edge);
    }

    static void Edge(RectTransform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 size, Color color)
    {
        var rt = UiBuild.Rect(name, parent);
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        UiBuild.Fill(rt, color);
    }

    /// <summary>
    /// Porta il rect alla dimensione della cella e scala proporzionalmente tutti
    /// i figli: nel prefab hanno anchor puntuali e posizioni assolute, quindi
    /// senza riscalarli resterebbero raggomitolati al centro.
    /// Idempotente: se la radice e' gia' alla misura giusta non fa nulla.
    /// </summary>
    static void ScaleTree(RectTransform root, float targetW, float targetH)
    {
        float w = root.rect.width, h = root.rect.height;
        if (w <= 1f || h <= 1f) return;
        if (Mathf.Abs(w - targetW) < 0.5f && Mathf.Abs(h - targetH) < 0.5f) return;

        float sx = targetW / w, sy = targetH / h;
        float sFont = Mathf.Min(sx, sy);

        var children = root.GetComponentsInChildren<RectTransform>(true);
        foreach (var rt in children)
        {
            if (rt == root) continue;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x * sx, rt.anchoredPosition.y * sy);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x * sx, rt.sizeDelta.y * sy);
        }

        foreach (var text in root.GetComponentsInChildren<Text>(true))
        {
            text.fontSize = Mathf.Max(8, Mathf.RoundToInt(text.fontSize * sFont));
            if (text.resizeTextForBestFit)
            {
                text.resizeTextMinSize = Mathf.Max(4, Mathf.RoundToInt(text.resizeTextMinSize * sFont));
                text.resizeTextMaxSize = Mathf.Max(8, Mathf.RoundToInt(text.resizeTextMaxSize * sFont));
            }
        }

        foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
            tmp.fontSize *= sFont;

        root.sizeDelta = new Vector2(targetW, targetH);
    }

    /// <summary>
    /// Posiziona un figlio della cella in coordinate banda (origine in alto a
    /// sinistra della cella). I figli del prefab hanno anchor puntuali al centro,
    /// quindi la conversione e' un'unica traslazione.
    /// </summary>
    static void Place(RectTransform cell, string childName, float x, float y, float w, float h)
    {
        var child = cell.Find(childName) as RectTransform;
        if (child == null) return;

        child.anchorMin = child.anchorMax = new Vector2(0.5f, 0.5f);
        child.pivot = new Vector2(0.5f, 0.5f);
        child.sizeDelta = new Vector2(w, h);
        child.anchoredPosition = new Vector2(
            x + w * 0.5f - cell.rect.width * 0.5f,
            cell.rect.height * 0.5f - (y + h * 0.5f));
    }

    /// <summary>
    /// I Text del prefab sono scalati da rect da 26 px: senza overflow esplicito
    /// il testo viene troncato ("DEF 4" diventava "D").
    /// </summary>
    static void StyleText(RectTransform cell, string childName, int size, TextAnchor anchor, Color color)
    {
        var child = cell.Find(childName);
        var text = child != null ? child.GetComponent<Text>() : null;
        if (text == null) return;

        text.resizeTextForBestFit = false;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
    }

    /// <summary>
    /// Rende invisibile un Text senza toglierlo: CardView lo scrive comunque e
    /// usa sideText per riconoscere il cambio di lato.
    /// </summary>
    static void Hide(RectTransform cell, string childName)
    {
        var child = cell.Find(childName) as RectTransform;
        var text = child != null ? child.GetComponent<Text>() : null;
        if (text == null) return;

        text.color = new Color(1f, 1f, 1f, 0f);
        child.sizeDelta = new Vector2(2f, 2f);
        child.anchoredPosition = new Vector2(-cell.rect.width, 0f);
    }

    static void SetLayoutElement(GameObject go, float w, float h)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.preferredWidth = w;
        le.preferredHeight = h;
        le.minWidth = w;
        le.minHeight = h;
    }

    /// <summary>
    /// Spegne il Raycast Target su tutti i figli: EventSystem.RaycastAll restituisce
    /// il figlio colpito e FindBoardCardUnderPointer ci cerca dentro un CardView.
    /// Un Text o un artwork che intercetta il click fa fallire lo swap.
    /// La radice resta bersaglio: e' lei che gestisce l'input.
    /// </summary>
    static void SilenceChildRaycasts(GameObject root)
    {
        foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic.gameObject == root) continue;
            graphic.raycastTarget = false;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  2. Scena
    // ══════════════════════════════════════════════════════════════════════════

    static void BuildScene()
    {
        var gm = Object.FindAnyObjectByType<GameManager>();
        var hand = Object.FindAnyObjectByType<HandManager>();
        if (gm == null || hand == null)
        {
            Debug.LogError("[Layout] GameManager o HandManager mancanti in scena.");
            return;
        }

        var canvasGO = GameObject.Find("Canvas") ?? new GameObject("Canvas", typeof(Canvas));
        var canvas = canvasGO.GetComponent<Canvas>() ?? canvasGO.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = Camera.main;
        canvas.planeDistance = 100f;
        canvas.sortingOrder = 0;
        canvasGO.transform.localScale = Vector3.one;
        canvasGO.transform.localRotation = Quaternion.identity;
        canvasGO.transform.position = Vector3.zero;

        var scaler = canvasGO.GetComponent<CanvasScaler>() ?? canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefW, RefH);
        // Il kit usa 1 pixel per unita': con il default 100 i bordi 9-slice
        // diventano cento volte piu' grandi e il centro delle barre sparisce.
        scaler.referencePixelsPerUnit = 1f;
        canvas.referencePixelsPerUnit = 1f;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasGO.GetComponent<GraphicRaycaster>() == null) canvasGO.AddComponent<GraphicRaycaster>();

        UiBuild.ClearChildren(canvas.transform);

        var root = (RectTransform)canvas.transform;

        // Sfondo: nessun Raycast Target sopra l'area di gioco, o drag-and-drop e
        // swap smettono di funzionare. Con il kit importato e' il tabellone del
        // manifest, che disegna gia' i pozzetti di ogni zona.
        var backdrop = UiBuild.Rect("Backdrop", root);
        UiBuild.Band(backdrop, 0f, 0f, RefW, RefH);
        var backdropImg = UiBuild.Fill(backdrop, HasBackdrop ? Color.white : GamePalette.Background);
        if (HasBackdrop)
        {
            backdropImg.sprite = _boardBg;
            backdropImg.type = Image.Type.Simple;
            backdropImg.preserveAspect = false;
        }

        var field = UiBuild.Rect("Field", root);
        UiBuild.Band(field, FieldX, 0f, FieldW, RefH);

        var side = UiBuild.Rect("SidePanel", root);
        UiBuild.Band(side, SideX, 0f, RefW - SideX, RefH);
        if (!HasBackdrop) UiBuild.Fill(side, GamePalette.Panel);

        var hud = canvasGO.GetComponent<HudController>() ?? canvasGO.AddComponent<HudController>();

        BuildPlayerRail(root, hud);
        BuildTopBar(field, hud);
        BuildBossBand(field, hud);
        var aiBoardRoot = BuildEnemyLanes(field);
        BuildLaneAxis(field);
        var playerBoardRoot = BuildPlayerLanes(field);
        var (handRoot, spawnPoint) = BuildHandZone(field);

        BuildSideHeader(side, hud, gm);
        BuildInspector(side);
        var logText = BuildLog(side);
        var (btnAttack, btnEndTurn) = BuildCommands(side, hud);

        BuildCrtOverlay(root);
        BuildEndPanel(root, hud);

        // Le corsie del giocatore vanno riferite all'asse dopo la creazione.
        var axis = field.Find("LaneAxis").GetComponent<LaneAxisView>();
        axis.laneReferenceRoot = playerBoardRoot;

        WireGameManager(gm, playerBoardRoot, aiBoardRoot, btnAttack, btnEndTurn, logText);
        WireHandManager(hand, handRoot, spawnPoint);

        EditorUtility.SetDirty(canvasGO);
        EditorUtility.SetDirty(gm);
        EditorUtility.SetDirty(hand);
    }

    /// <summary>Pannello di zona: sopra board_bg basta un velo, senza il fondo si disegna la scatola.</summary>
    static RectTransform Zone(string name, Transform parent, float x, float y, float w, float h)
    {
        var rt = HasBackdrop
            ? UiBuild.Rect(name, parent)
            : UiBuild.PanelBox(name, parent, GamePalette.PanelSunken);

        if (HasBackdrop) UiBuild.Fill(rt, GamePalette.WithAlpha(GamePalette.PanelSunken, 0.55f));
        UiBuild.Band(rt, x, y, w, h);
        return rt;
    }

    // ── Campo di gioco ────────────────────────────────────────────────────────

    static void BuildTopBar(RectTransform field, HudController hud)
    {
        var plate = UiBuild.Rect("TurnPlate", field);
        UiBuild.Band(plate, 0f, TopPlateY, TurnPlateW, TopPlateH);
        Kit(plate, "banner/banner_flat", Image.Type.Sliced);

        hud.turnText = UiBuild.Text("Turn", plate, "TURNO 1 / 12", 24f, GamePalette.TextPrimary,
                                    TextAlignmentOptions.Left, FontStyles.Bold);
        UiBuild.Stretch(hud.turnText.rectTransform, 22f, 0f, 12f, 0f);
        hud.turnText.alignment = TextAlignmentOptions.Left;

        var chip = UiBuild.Rect("PhaseBanner", field);
        UiBuild.Band(chip, PhaseX, TopPlateY, PhaseW, TopPlateH);
        Kit(chip, "banner/banner_phase", Image.Type.Sliced);

        // Il velo colorato della fase sta su un figlio, non sul banner: HudController
        // lo tinge con alpha 0.16 e su uno sprite quella tinta lo cancellerebbe.
        var tint = UiBuild.Rect("Tint", chip);
        UiBuild.Stretch(tint, 8f, 6f, 8f, 6f);
        hud.phaseChip = UiBuild.Fill(tint, GamePalette.WithAlpha(GamePalette.Good, 0.12f));

        hud.phaseText = UiBuild.Text("Phase", chip, "FASE AZIONI", 20f, GamePalette.Good,
                                     TextAlignmentOptions.Center, FontStyles.Bold);
        UiBuild.Stretch(hud.phaseText.rectTransform);
    }

    /// <summary>
    /// Pelle del kit su un rect gia' posizionato. Se lo sprite non c'e' non
    /// disegna niente: il layout non deve dipendere dalla presenza del kit.
    /// </summary>
    static Image Kit(RectTransform rt, string relativePath, Image.Type type = Image.Type.Simple, float alpha = 1f)
    {
        var sprite = KitSprite(relativePath);
        if (sprite == null) return null;

        var img = UiBuild.Fill(rt, new Color(1f, 1f, 1f, alpha));
        img.sprite = sprite;
        img.type = type;
        img.preserveAspect = false;
        return img;
    }

    static void BuildBossBand(RectTransform field, HudController hud)
    {
        var band = Zone("BossBand", field, 0f, BossY, FieldW, BossH);

        hud.bossNameText = UiBuild.Text("Name", band, "BOSS", 19f, GamePalette.BossHp,
                                        TextAlignmentOptions.Left, FontStyles.Bold);
        UiBuild.Band(hud.bossNameText.rectTransform, 16f, 4f, 300f, 24f);

        var note = UiBuild.Text("Note", band, "il rullo gira a fine turno e cambia tutte le caselle",
                                14f, GamePalette.TextMuted, TextAlignmentOptions.Left);
        UiBuild.Band(note.rectTransform, 16f, 28f, 480f, 22f);
        hud.rollSummaryText = note;

        hud.bossHpBar = UiBuild.Bar("HpBar", band, GamePalette.BossHp, out var barRt, kind: "boss");
        UiBuild.Band(barRt, 520f, 8f, 520f, 40f);

        hud.bossHpText = UiBuild.Text("HpText", band, "24/24", 20f, GamePalette.TextPrimary,
                                      TextAlignmentOptions.Right, FontStyles.Bold);
        UiBuild.Band(hud.bossHpText.rectTransform, 1046f, 14f, 116f, 28f);
    }

    static RectTransform BuildEnemyLanes(RectTransform field)
    {
        // La cassa del rullo e' piu' alta delle caselle: sopra e sotto restano le
        // fasce in cui il reel di fine turno fa scorrere le caselle parziali.
        var housing = UiBuild.Rect("ReelHousing", field);
        UiBuild.Band(housing, 0f, ReelHousingY, FieldW, ReelHousingH);
        if (!HasBackdrop) UiBuild.Fill(housing, GamePalette.WithAlpha(Color.black, 0.35f));

        // Wrapper: _ReelOverlayLayer nasce come FRATELLO di AIBoardRoot, quindi il
        // board deve avere un parent proprio e nessuna scala diversa dal fratello.
        var zone = UiBuild.Rect("EnemyLanes", field);
        UiBuild.Band(zone, (FieldW - EnemyBoardW) * 0.5f, EnemyY, EnemyBoardW, SlotH);

        var board = UiBuild.Rect("AIBoardRoot", zone);
        UiBuild.Stretch(board);
        LaneGroup(board, EnemyLaneGap);

        // Cornice, payline e vetro vanno DOPO le corsie: le caselle sono opache e
        // li coprirebbero, e la payline deve attraversarle, non passarci dietro.
        // Il fondo della cassa e gli aloni di colonna stanno invece sotto, cioe'
        // dentro housing: li monta ReelChrome, che ha i due strati.
        var glassLayer = UiBuild.Rect("ReelGlass", field);
        UiBuild.Band(glassLayer, 0f, ReelHousingY, FieldW, ReelHousingH);

        var chrome = housing.gameObject.AddComponent<ReelChrome>();
        chrome.underLayer = housing;
        chrome.overLayer = glassLayer;
        chrome.laneReferenceRoot = board;
        chrome.cellTop = EnemyY - ReelHousingY;
        chrome.cellWidth = SlotW;
        chrome.cellHeight = SlotH;

        // Senza il kit la cassa non si disegna: resta la riga della payline, che
        // e' l'unica cosa che rende la fila un rullo anche a tinte piatte.
        if (KitSprite("reel/reel_payline") == null)
        {
            var payline = UiBuild.Rect("PaylineFlat", field);
            UiBuild.Band(payline, 8f, PaylineY - 1f, FieldW - 16f, 2f);
            UiBuild.Fill(payline, GamePalette.Payline);
        }

        return board;
    }

    static void BuildLaneAxis(RectTransform field)
    {
        var axis = UiBuild.Rect("LaneAxis", field);
        UiBuild.Band(axis, 0f, AxisY, FieldW, AxisH);

        var view = axis.gameObject.AddComponent<LaneAxisView>();
        view.columnWidth = LanePitch - 76f;
    }

    static RectTransform BuildPlayerLanes(RectTransform field)
    {
        var zone = UiBuild.Rect("PlayerLanes", field);
        UiBuild.Band(zone, (FieldW - PlayerBoardW) * 0.5f, PlayerY, PlayerBoardW, CardH);

        var board = UiBuild.Rect("PlayerBoardRoot", zone);
        UiBuild.Stretch(board);
        LaneGroup(board, PlayerLaneGap);
        return board;
    }

    /// <summary>
    /// Colonne di corsia. Il gap e' diverso per i due lati perche' le celle lo
    /// sono, ma il passo risultante e' lo stesso: 396. E' quello che tiene rullo,
    /// asse e corsie sugli stessi tre centri.
    /// childControl spento: il gruppo posiziona ma non ridimensiona, cosi il rect
    /// della cella (da cui _BoardContainer prende la misura) resta quello del prefab.
    /// </summary>
    static void LaneGroup(RectTransform board, float gap)
    {
        var group = board.gameObject.AddComponent<HorizontalLayoutGroup>();
        group.spacing = gap;
        group.childAlignment = TextAnchor.MiddleCenter;
        group.childControlWidth = false;
        group.childControlHeight = false;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = false;
        group.padding = new RectOffset(0, 0, 0, 0);
    }

    /// <summary>
    /// Rail verticale del giocatore: stato, mazzo e legenda in colonna sul bordo
    /// sinistro. In orizzontale costavano una banda intera al campo di gioco.
    /// </summary>
    static void BuildPlayerRail(RectTransform root, HudController hud)
    {
        var rail = UiBuild.Rect("PlayerRail", root);
        UiBuild.Band(rail, RailX, RailY, RailW, RailH);
        if (!HasBackdrop) UiBuild.Fill(rail, GamePalette.PanelSunken);

        var label = UiBuild.Text("Label", rail, "TU", 18f, GamePalette.PlayerHp,
                                 TextAlignmentOptions.Left, FontStyles.Bold);
        UiBuild.Band(label.rectTransform, 12f, 12f, 120f, 26f);

        hud.playerHpBar = UiBuild.Bar("HpBar", rail, GamePalette.PlayerHp, out var barRt, kind: "hp");
        UiBuild.Band(barRt, 0f, RailHpY + 2f, RailW, RailHpH - 4f);

        hud.playerHpText = UiBuild.Text("HpText", rail, "20/20", 16f, GamePalette.TextPrimary,
                                        TextAlignmentOptions.Right, FontStyles.Bold);
        UiBuild.Band(hud.playerHpText.rectTransform, RailW - 108f, RailHpY + 8f, 100f, RailHpH - 16f);

        var apLabel = UiBuild.Text("ApLabel", rail, "AP", 15f, GamePalette.Ap,
                                   TextAlignmentOptions.Left, FontStyles.Bold);
        UiBuild.Band(apLabel.rectTransform, 8f, RailApY + 6f, 40f, 26f);

        hud.apPipsRoot = UiBuild.Rect("ApPips", rail);
        UiBuild.Band(hud.apPipsRoot, 48f, RailApY + 8f, 176f, 22f);

        hud.apText = UiBuild.Text("ApText", rail, "4/5", 16f, GamePalette.TextPrimary,
                                  TextAlignmentOptions.Right, FontStyles.Bold);
        UiBuild.Band(hud.apText.rectTransform, RailW - 68f, RailApY + 6f, 60f, 26f);

        var costs = UiBuild.Text("Costs", rail, "PESCA 1 / GIOCA 1 / FLIP 1 / ATK 1 AP", 13f, GamePalette.TextMuted,
                                 TextAlignmentOptions.Left);
        UiBuild.Band(costs.rectTransform, 10f, RailCostY, RailW - 20f, RailCostH);
        costs.fontSize = 12f;
        costs.textWrappingMode = TextWrappingModes.Normal;
        hud.actionCostsText = costs;

        BuildDeck(rail, hud);
        BuildLegend(rail);
    }

    static (Transform handRoot, Transform spawnPoint) BuildHandZone(RectTransform field)
    {
        // Area di attivazione ancorata al fondo: cresce verso l'alto quando la
        // mano sale, cosi il puntatore resta dentro mentre sceglie una carta.
        // A riposo copre solo la fascia mano e non intercetta l'area di gioco.
        var zone = UiBuild.Rect("HandZone", field);
        zone.anchorMin = zone.anchorMax = new Vector2(0f, 0f);
        zone.pivot = new Vector2(0f, 0f);
        zone.sizeDelta = new Vector2(FieldW, HandRestH);
        zone.anchoredPosition = Vector2.zero;
        UiBuild.Fill(zone, new Color(1f, 1f, 1f, 0f), raycast: true);

        // Binario della mano abbassata. Ancorato al fondo dell'area, che cresce
        // verso l'alto quando la mano sale: senza, il binario salirebbe con lei.
        // Creato prima di PlayerHand, cosi le carte gli passano davanti.
        var dock = UiBuild.Rect("HandDock", zone);
        dock.anchorMin = dock.anchorMax = new Vector2(0.5f, 0f);
        dock.pivot = new Vector2(0.5f, 0f);
        dock.sizeDelta = new Vector2(FieldW, RefH - HandDockY);
        dock.anchoredPosition = Vector2.zero;
        Kit(dock, "hand/hand_dock_low");

        // Niente LayoutGroup: HandManager riscrive container.localPosition ogni
        // frame e un gruppo attivo ci combatte.
        //
        // Il pivot DEVE stare al centro: HandManager posiziona i container con
        // localPosition simmetrica intorno allo zero (-spacing..+spacing), e lo
        // zero locale e' il pivot del parent.
        //
        // Ancoraggio al fondo dell'area: cosi quando l'area cresce la mano non si
        // sposta, la muove solo il tween di HandTray.
        var handRoot = UiBuild.Rect("PlayerHand", zone);
        handRoot.anchorMin = handRoot.anchorMax = new Vector2(0.5f, 0f);
        handRoot.pivot = new Vector2(0.5f, 0.5f);
        handRoot.sizeDelta = new Vector2(HandRootW, CardH);
        handRoot.anchoredPosition = new Vector2(0f, HandRestY);

        var spawn = UiBuild.Rect("spawnPoint", zone);
        spawn.anchorMin = spawn.anchorMax = new Vector2(1f, 0f);
        spawn.pivot = new Vector2(0.5f, 0.5f);
        spawn.sizeDelta = new Vector2(CardW, CardH);
        spawn.anchoredPosition = new Vector2(-140f, HandRestY);

        var tray = zone.gameObject.AddComponent<HandTray>();
        tray.handRoot = handRoot;
        tray.restY = HandRestY;
        tray.raisedY = HandRaisedY;
        tray.restHeight = HandRestH;

        return (handRoot, spawn);
    }

    // ── Rail: mazzo e legenda ─────────────────────────────────────────────────

    /// <summary>
    /// Il mazzo: una pila di carte vere che si assottiglia, cliccabile.
    /// Il bottone PESCA non diceva quante carte restassero ne' cosa stesse per
    /// uscire, e a mazzo vuoto non faceva nulla senza spiegare perche'.
    /// Sta nel rail, non nella colonna destra: e' un oggetto del giocatore, come
    /// i suoi HP e i suoi AP.
    /// </summary>
    static void BuildDeck(RectTransform rail, HudController hud)
    {
        var label = UiBuild.Text("DeckLabel", rail, "MAZZO", 14f, GamePalette.TextMuted,
                                 TextAlignmentOptions.Left, FontStyles.Bold);
        UiBuild.Band(label.rectTransform, 12f, RailDeckLabelY, 120f, RailDeckLabelH);

        hud.deckText = UiBuild.Text("DeckCount", rail, "MAZZO 0", 22f, GamePalette.TextPrimary,
                                    TextAlignmentOptions.Right, FontStyles.Bold);
        UiBuild.Band(hud.deckText.rectTransform, RailW - 140f, RailDeckLabelY - 2f, 128f, RailDeckLabelH + 4f);

        var box = UiBuild.Rect("Deck", rail);
        UiBuild.Band(box, RailDeckX, RailDeckY, RailDeckW, RailDeckH);

        // Unico Raycast Target di questa banda, e sta fuori dall'area di gioco:
        // e' il bersaglio del clic che pesca. I figli — le carte della pila —
        // fanno risalire il proprio clic fin qui.
        UiBuild.Fill(box, new Color(1f, 1f, 1f, 0.02f), raycast: true);

        var view = box.gameObject.AddComponent<DeckView>();

        var stack = UiBuild.Rect("Stack", box);
        UiBuild.Stretch(stack);
        view.stackRoot = stack;

        // Con il kit lo spessore e' gia' negli sprite (deck_stack_0..5) e non
        // serve impilare prefab veri: sei carte vere nel rail costano sei Canvas
        // annidati per una pila che non si puo' nemmeno leggere.
        var stackRt = UiBuild.Rect("StackImage", stack);
        UiBuild.Stretch(stackRt);
        view.stackImage = Kit(stackRt, "deck/deck_stack_5");
        if (view.stackImage != null) view.stackImage.preserveAspect = true;

        var pulseRt = UiBuild.Rect("Pulse", stack);
        UiBuild.Stretch(pulseRt, -8f, -8f, -8f, -8f);
        view.pulseImage = Kit(pulseRt, "deck/deck_pulse");
        if (view.pulseImage != null)
        {
            view.pulseImage.preserveAspect = true;
            view.pulseImage.enabled = false;
        }

        view.hintText = UiBuild.Text("Hint", rail, "clic per pescare · 1 AP", 14f, GamePalette.TextMuted,
                                     TextAlignmentOptions.Center);
        UiBuild.Band(view.hintText.rectTransform, 12f, RailDeckHintY, RailW - 24f, RailDeckHintH);
        view.hintText.textWrappingMode = TextWrappingModes.Normal;
    }

    /// <summary>
    /// Legenda. Sulla cella carta non e' scritto niente per scelta — la faccia la
    /// dice il template, il ruolo dei numeri lo dice il badge — e le caselle
    /// nemiche non sono carte: senza una chiave, queste due scelte si pagano alla
    /// prima partita di chi non ha scritto il codice.
    /// </summary>
    static void BuildLegend(RectTransform rail)
    {
        var box = Zone("Legend", rail, 0f, RailLegendY, RailW, RailLegendH);

        var title = UiBuild.Text("Title", box, "LEGENDA", 13f, GamePalette.TextMuted,
                                 TextAlignmentOptions.Left, FontStyles.Bold);
        UiBuild.Band(title.rectTransform, 12f, 10f, 200f, 20f);

        float y = 38f;
        y = LegendGroup(box, y, "LE TUE CARTE");
        y = LegendRow(box, y, GamePalette.Fronte, "RITRATTO", "attacca: ATK");
        y = LegendRow(box, y, GamePalette.Retro, "SIGILLO", "blocca: BLOCCO");
        y = LegendRow(box, y, GamePalette.Charge, "TACCHE", "cariche: bonus al colpo");

        y = LegendGroup(box, y + 6f, "IL RULLO NEMICO");
        y = LegendRow(box, y, GamePalette.Fronte, "CARICA", "colpisce questo giro");
        y = LegendRow(box, y, GamePalette.Retro, "DIFESA", "non colpisce, para");
        y = LegendRow(box, y, GamePalette.TextMuted, "PIP", "i giri che verranno");

        y = LegendGroup(box, y + 6f, "NUMERI");
        y = LegendRow(box, y, GamePalette.Danger, "ATK", "danno");
        y = LegendRow(box, y, GamePalette.PlayerHp, "HP", "vita");
        y = LegendRow(box, y, GamePalette.Retro, "BLOCCO", "danno assorbito");


    }

    static float LegendGroup(RectTransform box, float y, string label)
    {
        var text = UiBuild.Text($"Group_{label}", box, label, 11f, GamePalette.TextFaint,
                                TextAlignmentOptions.Left, FontStyles.Bold);
        UiBuild.Band(text.rectTransform, 12f, y, RailW - 24f, 16f);
        return y + 20f;
    }

    static float LegendRow(RectTransform box, float y, Color color, string name, string note)
    {
        const float h = 22f;

        var swatch = UiBuild.Rect($"Swatch_{name}", box);
        UiBuild.Band(swatch, 14f, y + 5f, 12f, 12f);
        UiBuild.Fill(swatch, color);

        var text = UiBuild.Text($"Row_{name}", box, name, 13f, color,
                                TextAlignmentOptions.Left, FontStyles.Bold);
        UiBuild.Band(text.rectTransform, 34f, y, 90f, h);

        if (!string.IsNullOrEmpty(note))
        {
            var noteText = UiBuild.Text($"Note_{name}", box, note, 12f, GamePalette.TextFaint);
            UiBuild.Band(noteText.rectTransform, 122f, y, RailW - 134f, h);
        }

        return y + h;
    }

    // ── Colonna destra ────────────────────────────────────────────────────────

    static void BuildSideHeader(RectTransform side, HudController hud, GameManager gm)
    {
        var header = UiBuild.Rect("Header", side);
        UiBuild.Band(header, 0f, HeaderY, SideContentW, HeaderH);

        // Il conteggio del mazzo vive sulla pila nel rail: ripeterlo qui sarebbe
        // lo stesso numero scritto due volte.
        hud.handText = UiBuild.Text("Hand", header, "MANO 0/8", 17f, GamePalette.TextPrimary,
                                    TextAlignmentOptions.Left, FontStyles.Bold);
        UiBuild.Band(hud.handText.rectTransform, 16f, 14f, 200f, 24f);

        // Il seed non cambia mai in partita: e' un'etichetta, non un valore da
        // aggiornare, e serve a poter ripetere una partita identica.
        var seed = UiBuild.Text("Seed", header, $"SEED {gm.seed}", 14f, GamePalette.TextFaint,
                                TextAlignmentOptions.Right);
        UiBuild.Band(seed.rectTransform, SideContentW - 200f, 14f, 184f, 24f);
    }

    static void BuildInspector(RectTransform side)
    {
        var box = Zone("Inspector", side, 0f, InspectorY, SideContentW, InspectorH);

        var panel = box.gameObject.AddComponent<InspectorPanel>();

        var strip = UiBuild.Rect("SideStrip", box);
        UiBuild.Band(strip, 0f, 0f, 6f, InspectorH);
        panel.sideStrip = UiBuild.Fill(strip, GamePalette.Neutral);

        panel.titleText = UiBuild.Text("Title", box, "ISPETTORE", 22f, GamePalette.TextPrimary,
                                       TextAlignmentOptions.Left, FontStyles.Bold);
        UiBuild.Band(panel.titleText.rectTransform, 20f, 14f, 364f, 30f);

        panel.subtitleText = UiBuild.Text("Subtitle", box, "", 14f, GamePalette.TextMuted);
        UiBuild.Band(panel.subtitleText.rectTransform, 20f, 44f, 364f, 22f);

        panel.sideText = UiBuild.Text("Side", box, "", 13f, GamePalette.TextMuted,
                                      TextAlignmentOptions.Left, FontStyles.Bold);
        UiBuild.Band(panel.sideText.rectTransform, 20f, 66f, 364f, 20f);

        panel.bodyText = UiBuild.Text("Body", box, "", 15f, GamePalette.TextPrimary);
        UiBuild.Band(panel.bodyText.rectTransform, 20f, 96f, 364f, 384f);
        panel.bodyText.alignment = TextAlignmentOptions.TopLeft;
        panel.bodyText.textWrappingMode = TextWrappingModes.Normal;
        panel.bodyText.overflowMode = TextOverflowModes.Truncate;
        panel.bodyText.lineSpacing = 6f;

        panel.hintText = UiBuild.Text("Hint", box, "", 12f, GamePalette.TextMuted);
        UiBuild.Band(panel.hintText.rectTransform, 20f, 484f, 364f, 34f);
        panel.hintText.alignment = TextAlignmentOptions.TopLeft;
        panel.hintText.textWrappingMode = TextWrappingModes.Normal;
    }

    static TMP_Text BuildLog(RectTransform side)
    {
        var box = Zone("Log", side, 0f, LogY, SideContentW, LogH);

        var title = UiBuild.Text("Title", box, "LOG", 13f, GamePalette.TextMuted,
                                 TextAlignmentOptions.Left, FontStyles.Bold);
        UiBuild.Band(title.rectTransform, 14f, 8f, 200f, 18f);

        var scrollRt = UiBuild.Rect("Scroll", box);
        UiBuild.Band(scrollRt, 8f, 30f, SideContentW - 16f, LogH - 38f);
        var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
        scrollRt.gameObject.AddComponent<LogPanel>();

        var viewport = UiBuild.Rect("Viewport", scrollRt);
        UiBuild.Stretch(viewport, 4f, 4f, 14f, 4f);
        viewport.gameObject.AddComponent<RectMask2D>();

        // Il testo E' il contenuto dello ScrollRect, senza un rect intermedio.
        // Un ContentSizeFitter misura l'ILayoutElement del proprio GameObject,
        // non i figli: un "Content" con il fitter e il testo dentro restava alto
        // zero, lo ScrollRect non aveva niente da scorrere e il RectMask2D
        // tagliava tutte le righe oltre la prima schermata.
        var logText = UiBuild.Text("LogText", viewport, "", 14f, GamePalette.TextPrimary);
        logText.alignment = TextAlignmentOptions.TopLeft;
        logText.textWrappingMode = TextWrappingModes.Normal;
        logText.overflowMode = TextOverflowModes.Overflow;
        logText.raycastTarget = false;

        var textFitter = logText.gameObject.AddComponent<ContentSizeFitter>();
        textFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var content = logText.rectTransform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        content.anchoredPosition = Vector2.zero;

        var scrollbar = BuildScrollbar(scrollRt);

        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 32f;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        return logText;
    }

    static Scrollbar BuildScrollbar(RectTransform parent)
    {
        var rt = UiBuild.Rect("Scrollbar", parent);
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(8f, 0f);
        rt.anchoredPosition = new Vector2(-2f, 0f);
        UiBuild.Fill(rt, GamePalette.WithAlpha(Color.black, 0.35f), raycast: true);

        var area = UiBuild.Rect("SlidingArea", rt);
        UiBuild.Stretch(area);

        var handle = UiBuild.Rect("Handle", area);
        UiBuild.Stretch(handle);
        var handleImg = UiBuild.Fill(handle, GamePalette.Border, raycast: true);

        var scrollbar = rt.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImg;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        return scrollbar;
    }

    static (Button attack, Button endTurn) BuildCommands(RectTransform side, HudController hud)
    {
        var box = UiBuild.Rect("Commands", side);
        UiBuild.Band(box, 0f, CommandsY, SideContentW, CommandH * 2f + CommandGap);

        // Quattro stati disegnati per tono, non una tinta moltiplicata: il rosso
        // sangue attacca, il verde fosforo chiude il turno.
        var attack = UiBuild.Command("BtnAttack", box, "ATTACCA", "1 AP / attacca con le carte in Fronte",
                                     GamePalette.Danger, out _, tone: "blood");
        UiBuild.Band((RectTransform)attack.transform, 0f, 0f, SideContentW, CommandH);

        var endTurn = UiBuild.Command("BtnEndTurn", box, "CHIUDI TURNO", "0 AP",
                                      GamePalette.PlayerHp, out _, tone: "phos");
        UiBuild.Band((RectTransform)endTurn.transform, 0f, CommandH + CommandGap, SideContentW, CommandH);

        hud.attackCostText = attack.transform.Find("Cost").GetComponent<TMP_Text>();
        hud.endTurnLabel = endTurn.transform.Find("Label").GetComponent<TMP_Text>();
        hud.endTurnCostText = endTurn.transform.Find("Cost").GetComponent<TMP_Text>();
        hud.attackCostText.fontSize = 13f;
        hud.endTurnCostText.fontSize = 13f;
        return (attack, endTurn);
    }

    /// <summary>
    /// Scanline e vignetta sopra tutto, senza Raycast Target: sono la pelle CRT
    /// del kit ed e' quello che fa leggere il tabellone come uno schermo invece
    /// che come una pagina. Se il kit non e' importato, non succede niente.
    /// </summary>
    static void BuildCrtOverlay(RectTransform root)
    {
        var scanlines = KitSprite("board/overlay_scanlines");
        var vignette = KitSprite("board/overlay_vignette");
        if (scanlines == null && vignette == null) return;

        var layer = UiBuild.Rect("CrtOverlay", root);
        UiBuild.Band(layer, 0f, 0f, RefW, RefH);

        CrtLayer(layer, "Scanlines", scanlines, 0.16f);
        CrtLayer(layer, "Vignette", vignette, 0.35f);
    }

    static void CrtLayer(RectTransform parent, string name, Sprite sprite, float alpha)
    {
        if (sprite == null) return;

        var rt = UiBuild.Rect(name, parent);
        UiBuild.Stretch(rt);
        var img = UiBuild.Fill(rt, new Color(1f, 1f, 1f, alpha));
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = false;
    }

    static void BuildEndPanel(RectTransform root, HudController hud)
    {
        var panel = UiBuild.Rect("EndMatchPanel", root);
        UiBuild.Band(panel, 0f, 0f, RefW, RefH);
        UiBuild.Fill(panel, new Color(0f, 0f, 0f, 0.82f), raycast: true);

        var box = UiBuild.PanelBox("Box", panel, GamePalette.Panel);
        UiBuild.Centered(box, 760f, 340f);

        hud.endTitle = UiBuild.Text("Title", box, "PARTITA FINITA", 56f, GamePalette.TextPrimary,
                                    TextAlignmentOptions.Center, FontStyles.Bold);
        UiBuild.Centered(hud.endTitle.rectTransform, 700f, 80f, 0f, 60f);

        hud.endDetail = UiBuild.Text("Detail", box, "", 22f, GamePalette.TextMuted, TextAlignmentOptions.Center);
        UiBuild.Centered(hud.endDetail.rectTransform, 700f, 120f, 0f, -50f);
        hud.endDetail.textWrappingMode = TextWrappingModes.Normal;

        hud.endPanel = panel.gameObject;
        panel.gameObject.SetActive(false);
    }

    // ── Cablaggio ─────────────────────────────────────────────────────────────

    static void WireGameManager(GameManager gm, Transform playerBoard, Transform aiBoard,
                                Button attack, Button endTurn, TMP_Text log)
    {
        var so = new SerializedObject(gm);
        so.FindProperty("playerBoardRoot").objectReferenceValue = playerBoard;
        so.FindProperty("aiBoardRoot").objectReferenceValue = aiBoard;
        so.FindProperty("btnAttack").objectReferenceValue = attack;
        so.FindProperty("btnEndTurn").objectReferenceValue = endTurn;
        so.FindProperty("logText").objectReferenceValue = log;
        // HP e AP li scrive HudController: barre, pallini e denominatore corretto.
        so.FindProperty("hpText").objectReferenceValue = null;
        so.FindProperty("apText").objectReferenceValue = null;
        so.FindProperty("EnemyHptxt").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void WireHandManager(HandManager hand, Transform handRoot, Transform spawnPoint)
    {
        var so = new SerializedObject(hand);
        so.FindProperty("handRoot").objectReferenceValue = handRoot;
        so.FindProperty("spawnPoint").objectReferenceValue = spawnPoint;
        // La carta nasce gia' alla dimensione della cella: il moltiplicatore
        // serviva quando il prefab era 100x154 e ora la sparerebbe a 330x495.
        SetFloat(so, "spawnScaleMultiplier", 1f);
        // Mano e passo sono misure di layout, non di bilanciamento: sono gli
        // hand_tab_slots del kit, 8 linguette da 132. Il passo e' piu' stretto
        // della carta perche' le carte in mano DEVONO sovrapporsi.
        so.FindProperty("maxHandSize").intValue = MaxHandCards;
        SetFloat(so, "handSpacing", HandSpacing);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
