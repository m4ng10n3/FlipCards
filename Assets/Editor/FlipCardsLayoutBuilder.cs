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
/// Due fasi indipendenti:
///  1. i prefab di carte e slot vengono portati alla dimensione della cella;
///  2. il Canvas viene ricostruito a bande e ricablato su GameManager/HandManager.
/// </summary>
public static class FlipCardsLayoutBuilder
{
    // ── Misure (LAYOUT_SPEC §6.2 / §6.3 / §6.4) ───────────────────────────────

    const float RefW = 1920f, RefH = 1080f;
    const float FieldW = 1440f;
    const float SideW = 480f, SidePad = 40f, SideContentW = 400f;

    const float CellW = 220f, CellH = 330f;   // carta e slot
    const float LaneGap = 68f;                // colonna 240 - cella 220 + gap 48
    const float LaneColumn = 240f;

    // Bande del campo di gioco
    const float TopBarY = 0f, TopBarH = 56f;
    const float BossY = 56f, BossH = 92f;
    const float EnemyY = 148f, EnemyH = 330f;
    const float AxisY = 478f, AxisH = 68f;
    const float PlayerY = 546f, PlayerH = 340f;
    const float PlayerBandY = 886f, PlayerBandH = 52f;
    const float HandY = 938f, HandH = 142f;
    const float HandRootW = 1400f;   // vedi BuildHandZone: detta anche lo spacing

    // Bande della colonna destra
    const float HeaderY = 56f, HeaderH = 40f;
    const float InspectorY = 96f, InspectorH = 424f;
    const float LogY = 520f, LogH = 336f;
    const float CommandsY = 856f, CommandsH = 224f;
    const float CommandH = 56f, CommandGap = 16f;

    [MenuItem("FlipCards/Ricostruisci layout di gioco")]
    public static void Rebuild()
    {
        ResizePrefabs();
        BuildScene();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Layout] Ricostruito: campo 1440x1080, colonna destra 480, celle 220x330.");
    }

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
        // sostituiscono, altrimenti le corsie saltano a ogni morte.
        var gm = Object.FindAnyObjectByType<GameManager>();
        if (gm != null)
        {
            ResizePlaceholder(gm.EmptySpot);
            ResizePlaceholder(gm.EmptySlot);
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

            ScaleTree(rt, CellW, CellH);
            SetLayoutElement(root, CellW, CellH);
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

                // Bande della cella carta (LAYOUT_SPEC §6.5). Il Template resta a
                // tutta cella: e' la cornice.
                Place(cell, "Template", 0f, 0f, CellW, CellH);
                Place(cell, "Name", 4f, 2f, CellW - 8f, 30f);
                Place(cell, "imagecharacter", 0f, 36f, CellW, 158f);
                Place(cell, "FrontDamage", 4f, 198f, 68f, 44f);
                Place(cell, "HP", 76f, 198f, 68f, 44f);
                Place(cell, "BackBlock", 148f, 198f, 68f, 44f);

                StyleText(cell, "Name", 20, TextAnchor.MiddleCenter, GamePalette.TextPrimary);
                StyleText(cell, "FrontDamage", 26, TextAnchor.MiddleCenter, GamePalette.TextPrimary);
                StyleText(cell, "HP", 26, TextAnchor.MiddleCenter, GamePalette.TextPrimary);
                StyleText(cell, "BackBlock", 26, TextAnchor.MiddleCenter, GamePalette.TextPrimary);

                // Fazione e lato li disegna CardOverlay come badge e fascia colorata.
                // I Text del prefab restano (CardView li scrive e usa sideText per
                // riconoscere il flip) ma diventano invisibili.
                Hide(cell, "Faction");
                Hide(cell, "Side");

                // L'hint galleggia SOPRA la carta in z, sovrapposto all'artwork,
                // e non nel flusso della cella: accodato dentro un rect da 89x16
                // mostrava una riga e troncava il resto. Fuori dalla cella
                // finirebbe addosso all'asse delle corsie.
                var hint = cell.Find("HintText") as RectTransform;
                if (hint != null)
                {
                    hint.anchorMin = hint.anchorMax = new Vector2(0.5f, 0.5f);
                    hint.pivot = new Vector2(0.5f, 0.5f);
                    hint.sizeDelta = new Vector2(CellW, 40f);
                    hint.anchoredPosition = new Vector2(0f, CellH * 0.5f - 140f);

                    var text = hint.GetComponent<Text>();
                    if (text != null)
                    {
                        text.resizeTextForBestFit = false;
                        text.fontSize = 22;
                        text.alignment = TextAnchor.MiddleCenter;
                        text.horizontalOverflow = HorizontalWrapMode.Overflow;
                        text.verticalOverflow = VerticalWrapMode.Overflow;
                        text.fontStyle = FontStyle.Bold;
                        text.color = GamePalette.Fronte;
                        var shadow = text.GetComponent<Shadow>();
                        if (shadow == null) shadow = text.gameObject.AddComponent<Shadow>();
                        shadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
                        shadow.effectDistance = new Vector2(2f, -2f);
                    }
                }

                if (view.GetComponent<CardOverlay>() == null)
                    view.gameObject.AddComponent<CardOverlay>();
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void ResizeSlotPrefab(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var rt = root.transform as RectTransform;
            if (rt == null) return;

            ScaleTree(rt, CellW, CellH);
            SetLayoutElement(root, CellW, CellH);
            SilenceChildRaycasts(root);

            var view = root.GetComponent<SlotView>();
            if (view != null)
            {
                // Letto in Awake per il LayoutElement: se resta al vecchio valore
                // l'HorizontalLayoutGroup impagina corsie di larghezza sbagliata.
                view.preferredSize = new Vector2(CellW, CellH);
                EditorUtility.SetDirty(view);
            }

            // Fondo della cella: SlotView.Blink lo colora di giallo e lo rimette,
            // quindi il colore base va impostato qui.
            var bg = root.GetComponent<Image>();
            if (bg != null) bg.color = new Color(0.129f, 0.145f, 0.192f, 1f);

            var cell = (RectTransform)root.transform;

            // Bande della cella slot (LAYOUT_SPEC §6.6). Il figlio "Sprite" resta
            // quadrato: il reel ne copia il rect e al reveal l'immagine non deve
            // cambiare dimensione.
            Place(cell, "Name", 8f, 2f, CellW - 44f, 28f);
            Place(cell, "Sprite", 0f, 34f, CellW, CellW);
            Place(cell, "HP", 76f, 258f, 68f, 30f);
            Place(cell, "Def", 148f, 258f, 68f, 30f);

            StyleText(cell, "Name", 18, TextAnchor.MiddleLeft, GamePalette.TextPrimary);
            StyleText(cell, "HP", 18, TextAnchor.MiddleCenter, GamePalette.TextPrimary);
            StyleText(cell, "Def", 18, TextAnchor.MiddleCenter, GamePalette.TextPrimary);

            // La fazione la disegna SlotOverlay come badge colorato, uguale a
            // quello delle carte: il Text del prefab conteneva un valore fisso.
            Hide(cell, "Faction");

            var hint = cell.Find("HintText") as RectTransform;
            if (hint != null)
            {
                hint.anchorMin = hint.anchorMax = new Vector2(0.5f, 0.5f);
                hint.pivot = new Vector2(0.5f, 0.5f);
                hint.sizeDelta = new Vector2(CellW, 40f);
                hint.anchoredPosition = new Vector2(0f, CellH * 0.5f - 150f);

                var text = hint.GetComponent<Text>();
                if (text != null)
                {
                    text.resizeTextForBestFit = false;
                    text.fontSize = 22;
                    text.alignment = TextAnchor.MiddleCenter;
                    text.horizontalOverflow = HorizontalWrapMode.Overflow;
                    text.verticalOverflow = VerticalWrapMode.Overflow;
                    text.fontStyle = FontStyle.Bold;
                    text.color = GamePalette.Danger;
                    var shadow = text.GetComponent<Shadow>();
                    if (shadow == null) shadow = text.gameObject.AddComponent<Shadow>();
                    shadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
                    shadow.effectDistance = new Vector2(2f, -2f);
                }
            }

            if (root.GetComponent<SlotOverlay>() == null)
                root.AddComponent<SlotOverlay>();

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void ResizePlaceholder(GameObject placeholder)
    {
        if (placeholder == null) return;

        string path = AssetDatabase.GetAssetPath(placeholder);
        if (string.IsNullOrEmpty(path))
        {
            // Oggetto di scena: si modifica direttamente.
            ApplyPlaceholder(placeholder);
            EditorUtility.SetDirty(placeholder);
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            ApplyPlaceholder(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void ApplyPlaceholder(GameObject go)
    {
        var rt = go.transform as RectTransform;
        if (rt == null) return;

        ScaleTree(rt, CellW, CellH);
        SetLayoutElement(go, CellW, CellH);

        var outline = go.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = GamePalette.Fronte;
            outline.effectDistance = new Vector2(5f, -5f);
            outline.useGraphicAlpha = false;
        }

        // Una corsia vuota e' una falla: lo slot in Fronte colpisce direttamente
        // gli HP. La casella deve leggersi, non essere un rettangolo al 7%.
        var img = go.GetComponent<Image>();
        if (img != null) img.color = new Color(1f, 1f, 1f, 0.045f);

        if (go.transform.Find("_Frame") != null) return;

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
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasGO.GetComponent<GraphicRaycaster>() == null) canvasGO.AddComponent<GraphicRaycaster>();

        UiBuild.ClearChildren(canvas.transform);

        var root = (RectTransform)canvas.transform;

        // Sfondo: nessun Raycast Target sopra l'area di gioco, o drag-and-drop e
        // swap smettono di funzionare.
        var backdrop = UiBuild.Rect("Backdrop", root);
        UiBuild.Band(backdrop, 0f, 0f, RefW, RefH);
        UiBuild.Fill(backdrop, GamePalette.Background);

        var field = UiBuild.Rect("Field", root);
        UiBuild.Band(field, 0f, 0f, FieldW, RefH);

        var side = UiBuild.Rect("SidePanel", root);
        UiBuild.Band(side, FieldW, 0f, SideW, RefH);
        UiBuild.Fill(side, GamePalette.Panel);

        var hud = canvasGO.GetComponent<HudController>() ?? canvasGO.AddComponent<HudController>();

        BuildTopBar(field, hud);
        BuildBossBand(field, hud);
        var aiBoardRoot = BuildEnemyLanes(field);
        BuildLaneAxis(field);
        var playerBoardRoot = BuildPlayerLanes(field);
        BuildPlayerBand(field, hud);
        var (handRoot, spawnPoint) = BuildHandZone(field);

        BuildSideHeader(side, hud);
        BuildInspector(side);
        var logText = BuildLog(side);
        var (btnDraw, btnAttack, btnEndTurn) = BuildCommands(side);

        BuildEndPanel(root, hud);

        // Le corsie del giocatore vanno riferite all'asse dopo la creazione.
        var axis = field.Find("LaneAxis").GetComponent<LaneAxisView>();
        axis.laneReferenceRoot = playerBoardRoot;

        WireGameManager(gm, playerBoardRoot, aiBoardRoot, btnAttack, btnEndTurn, logText);
        WireHandManager(hand, handRoot, spawnPoint, btnDraw);

        EditorUtility.SetDirty(canvasGO);
        EditorUtility.SetDirty(gm);
        EditorUtility.SetDirty(hand);
    }

    // ── Campo di gioco ────────────────────────────────────────────────────────

    static void BuildTopBar(RectTransform field, HudController hud)
    {
        var bar = UiBuild.Rect("TopBar", field);
        UiBuild.Band(bar, 0f, TopBarY, FieldW, TopBarH);

        hud.turnText = UiBuild.Text("Turn", bar, "TURNO 1 / 12", 26f, GamePalette.TextPrimary,
                                    TextAlignmentOptions.Left, FontStyles.Bold);
        UiBuild.Band(hud.turnText.rectTransform, 40f, 12f, 400f, 32f);

        var chip = UiBuild.Rect("PhaseChip", bar);
        UiBuild.Band(chip, FieldW - 40f - 560f, 10f, 560f, 36f);
        hud.phaseChip = UiBuild.Fill(chip, GamePalette.WithAlpha(GamePalette.Good, 0.16f));

        hud.phaseText = UiBuild.Text("Phase", chip, "FASE AZIONI", 19f, GamePalette.Good,
                                     TextAlignmentOptions.Center, FontStyles.Bold);
        UiBuild.Stretch(hud.phaseText.rectTransform);
    }

    static void BuildBossBand(RectTransform field, HudController hud)
    {
        var band = UiBuild.PanelBox("BossBand", field, GamePalette.PanelSunken);
        UiBuild.Band(band, 40f, BossY, FieldW - 80f, BossH);

        hud.bossNameText = UiBuild.Text("Name", band, "BOSS", 20f, GamePalette.BossHp,
                                        TextAlignmentOptions.Left, FontStyles.Bold);
        UiBuild.Band(hud.bossNameText.rectTransform, 18f, 10f, 300f, 26f);

        var note = UiBuild.Text("Note", band, "il fronte nemico viene sostituito a ogni fine turno",
                                14f, GamePalette.TextMuted, TextAlignmentOptions.Right);
        UiBuild.Band(note.rectTransform, FieldW - 80f - 18f - 620f, 12f, 620f, 22f);

        hud.bossHpBar = UiBuild.Bar("HpBar", band, GamePalette.BossHp, out var barRt);
        UiBuild.Band(barRt, 18f, 46f, FieldW - 80f - 36f - 140f, 28f);

        hud.bossHpText = UiBuild.Text("HpText", band, "24/24", 20f, GamePalette.TextPrimary,
                                      TextAlignmentOptions.Right, FontStyles.Bold);
        UiBuild.Band(hud.bossHpText.rectTransform, FieldW - 80f - 18f - 130f, 46f, 130f, 28f);
    }

    static RectTransform BuildEnemyLanes(RectTransform field)
    {
        // Wrapper: _ReelOverlayLayer nasce come FRATELLO di AIBoardRoot, quindi il
        // board deve avere un parent proprio e nessuna scala diversa dal fratello.
        var zone = UiBuild.Rect("EnemyLanes", field);
        UiBuild.Band(zone, 20f, EnemyY, FieldW - 40f, EnemyH);

        var board = UiBuild.Rect("AIBoardRoot", zone);
        UiBuild.Stretch(board);
        LaneGroup(board);
        return board;
    }

    static void BuildLaneAxis(RectTransform field)
    {
        var axis = UiBuild.Rect("LaneAxis", field);
        UiBuild.Band(axis, 20f, AxisY, FieldW - 40f, AxisH);

        var view = axis.gameObject.AddComponent<LaneAxisView>();
        view.columnWidth = LaneColumn;
    }

    static RectTransform BuildPlayerLanes(RectTransform field)
    {
        var zone = UiBuild.Rect("PlayerLanes", field);
        UiBuild.Band(zone, 20f, PlayerY, FieldW - 40f, PlayerH);

        var board = UiBuild.Rect("PlayerBoardRoot", zone);
        UiBuild.Stretch(board);
        LaneGroup(board);
        return board;
    }

    /// <summary>
    /// Colonne di corsia: le celle restano 220 e il passo diventa 220+68 = 288,
    /// cioe' esattamente la colonna da 240 con il gap da 48 della specifica.
    /// childControl spento: il gruppo posiziona ma non ridimensiona, cosi il rect
    /// della cella (da cui _BoardContainer prende la misura) resta quello del prefab.
    /// </summary>
    static void LaneGroup(RectTransform board)
    {
        var group = board.gameObject.AddComponent<HorizontalLayoutGroup>();
        group.spacing = LaneGap;
        group.childAlignment = TextAnchor.MiddleCenter;
        group.childControlWidth = false;
        group.childControlHeight = false;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = false;
        group.padding = new RectOffset(0, 0, 0, 0);
    }

    static void BuildPlayerBand(RectTransform field, HudController hud)
    {
        var band = UiBuild.PanelBox("PlayerBand", field, GamePalette.PanelSunken);
        UiBuild.Band(band, 40f, PlayerBandY, FieldW - 80f, PlayerBandH);

        float w = FieldW - 80f;

        var label = UiBuild.Text("Label", band, "TU", 18f, GamePalette.PlayerHp,
                                 TextAlignmentOptions.Left, FontStyles.Bold);
        UiBuild.Band(label.rectTransform, 18f, 14f, 60f, 24f);

        hud.playerHpBar = UiBuild.Bar("HpBar", band, GamePalette.PlayerHp, out var barRt);
        UiBuild.Band(barRt, 70f, 14f, 520f, 24f);

        hud.playerHpText = UiBuild.Text("HpText", band, "20/20", 18f, GamePalette.TextPrimary,
                                        TextAlignmentOptions.Left, FontStyles.Bold);
        UiBuild.Band(hud.playerHpText.rectTransform, 604f, 14f, 120f, 24f);

        var apLabel = UiBuild.Text("ApLabel", band, "AP", 18f, GamePalette.Ap,
                                   TextAlignmentOptions.Left, FontStyles.Bold);
        UiBuild.Band(apLabel.rectTransform, 760f, 14f, 40f, 24f);

        hud.apPipsRoot = UiBuild.Rect("ApPips", band);
        UiBuild.Band(hud.apPipsRoot, 796f, 14f, 180f, 24f);

        hud.apText = UiBuild.Text("ApText", band, "4/5", 18f, GamePalette.TextPrimary,
                                  TextAlignmentOptions.Left, FontStyles.Bold);
        UiBuild.Band(hud.apText.rectTransform, 986f, 14f, 100f, 24f);

        var costs = UiBuild.Text("Costs", band, "pescare · giocare · girare · scambiare = 1 AP",
                                 14f, GamePalette.TextMuted, TextAlignmentOptions.Right);
        UiBuild.Band(costs.rectTransform, w - 18f - 500f, 16f, 500f, 20f);
    }

    static (Transform handRoot, Transform spawnPoint) BuildHandZone(RectTransform field)
    {
        var zone = UiBuild.Rect("HandZone", field);
        UiBuild.Band(zone, 0f, HandY, FieldW, HandH);

        // Niente LayoutGroup: HandManager riscrive container.localPosition ogni
        // frame e un gruppo attivo ci combatte.
        //
        // Il pivot DEVE stare al centro: HandManager posiziona i container con
        // localPosition simmetrica intorno allo zero (-spacing..+spacing), e lo
        // zero locale e' il pivot del parent. Con pivot in alto a sinistra la mano
        // finisce fuori dal campo.
        //
        // Larghezza: spacing = handRect.width / maxHandSize, quindi la larghezza
        // e' anche la regola di non sovrapposizione.
        var handRoot = UiBuild.Rect("PlayerHand", zone);
        handRoot.anchorMin = handRoot.anchorMax = new Vector2(0f, 1f);
        handRoot.pivot = new Vector2(0.5f, 0.5f);
        handRoot.sizeDelta = new Vector2(HandRootW, CellH);
        handRoot.anchoredPosition = new Vector2(FieldW * 0.5f, -CellH * 0.5f);

        var spawn = UiBuild.Rect("spawnPoint", zone);
        UiBuild.Band(spawn, FieldW - 160f, 0f, CellW, CellH);

        return (handRoot, spawn);
    }

    // ── Colonna destra ────────────────────────────────────────────────────────

    static void BuildSideHeader(RectTransform side, HudController hud)
    {
        var header = UiBuild.Rect("Header", side);
        UiBuild.Band(header, SidePad, HeaderY, SideContentW, HeaderH);

        hud.deckText = UiBuild.Text("Deck", header, "MAZZO 0", 17f, GamePalette.TextPrimary,
                                    TextAlignmentOptions.Left, FontStyles.Bold);
        UiBuild.Band(hud.deckText.rectTransform, 0f, 8f, 200f, 24f);

        hud.handText = UiBuild.Text("Hand", header, "MANO 0/5", 17f, GamePalette.TextPrimary,
                                    TextAlignmentOptions.Right, FontStyles.Bold);
        UiBuild.Band(hud.handText.rectTransform, SideContentW - 200f, 8f, 200f, 24f);
    }

    static void BuildInspector(RectTransform side)
    {
        var box = UiBuild.PanelBox("Inspector", side, GamePalette.PanelSunken);
        UiBuild.Band(box, SidePad, InspectorY, SideContentW, InspectorH);

        var panel = box.gameObject.AddComponent<InspectorPanel>();

        var strip = UiBuild.Rect("SideStrip", box);
        UiBuild.Band(strip, 0f, 0f, 6f, InspectorH);
        panel.sideStrip = UiBuild.Fill(strip, GamePalette.Neutral);

        panel.titleText = UiBuild.Text("Title", box, "ISPETTORE", 22f, GamePalette.TextPrimary,
                                       TextAlignmentOptions.Left, FontStyles.Bold);
        UiBuild.Band(panel.titleText.rectTransform, 20f, 12f, 364f, 30f);

        panel.subtitleText = UiBuild.Text("Subtitle", box, "", 14f, GamePalette.TextMuted);
        UiBuild.Band(panel.subtitleText.rectTransform, 20f, 42f, 364f, 22f);

        panel.sideText = UiBuild.Text("Side", box, "", 13f, GamePalette.TextMuted,
                                      TextAlignmentOptions.Left, FontStyles.Bold);
        UiBuild.Band(panel.sideText.rectTransform, 20f, 64f, 364f, 20f);

        panel.bodyText = UiBuild.Text("Body", box, "", 15f, GamePalette.TextPrimary);
        UiBuild.Band(panel.bodyText.rectTransform, 20f, 92f, 364f, 288f);
        panel.bodyText.alignment = TextAlignmentOptions.TopLeft;
        panel.bodyText.textWrappingMode = TextWrappingModes.Normal;
        panel.bodyText.overflowMode = TextOverflowModes.Truncate;
        panel.bodyText.lineSpacing = 6f;

        panel.hintText = UiBuild.Text("Hint", box, "", 12f, GamePalette.TextMuted);
        UiBuild.Band(panel.hintText.rectTransform, 20f, 384f, 364f, 34f);
        panel.hintText.alignment = TextAlignmentOptions.TopLeft;
        panel.hintText.textWrappingMode = TextWrappingModes.Normal;
    }

    static TMP_Text BuildLog(RectTransform side)
    {
        var box = UiBuild.PanelBox("Log", side, GamePalette.PanelSunken);
        UiBuild.Band(box, SidePad, LogY, SideContentW, LogH);

        var title = UiBuild.Text("Title", box, "LOG", 13f, GamePalette.TextMuted,
                                 TextAlignmentOptions.Left, FontStyles.Bold);
        UiBuild.Band(title.rectTransform, 12f, 8f, 200f, 18f);

        var scrollRt = UiBuild.Rect("Scroll", box);
        UiBuild.Band(scrollRt, 8f, 30f, SideContentW - 16f, LogH - 38f);
        var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
        scrollRt.gameObject.AddComponent<LogPanel>();

        var viewport = UiBuild.Rect("Viewport", scrollRt);
        UiBuild.Stretch(viewport, 4f, 4f, 14f, 4f);
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = UiBuild.Rect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0f, 0f);
        content.anchoredPosition = Vector2.zero;

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var logText = UiBuild.Text("LogText", content, "", 14f, GamePalette.TextPrimary);
        UiBuild.Stretch(logText.rectTransform);
        logText.alignment = TextAlignmentOptions.TopLeft;
        logText.textWrappingMode = TextWrappingModes.Normal;
        logText.overflowMode = TextOverflowModes.Overflow;
        logText.raycastTarget = false;

        // Il testo e' il contenuto: senza ContentSizeFitter sul testo stesso il
        // fitter del content non ha un ILayoutElement da misurare.
        var textFitter = logText.gameObject.AddComponent<ContentSizeFitter>();
        textFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var textRt = logText.rectTransform;
        textRt.anchorMin = new Vector2(0f, 1f);
        textRt.anchorMax = new Vector2(1f, 1f);
        textRt.pivot = new Vector2(0.5f, 1f);
        textRt.sizeDelta = Vector2.zero;
        textRt.anchoredPosition = Vector2.zero;

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

    static (Button draw, Button attack, Button endTurn) BuildCommands(RectTransform side)
    {
        var box = UiBuild.Rect("Commands", side);
        UiBuild.Band(box, SidePad, CommandsY, SideContentW, CommandsH);

        float y = 12f;
        var draw = UiBuild.Command("BtnDraw", box, "PESCA", "1 AP", GamePalette.Ap, out _);
        UiBuild.Band((RectTransform)draw.transform, 0f, y, SideContentW, CommandH);

        y += CommandH + CommandGap;
        var attack = UiBuild.Command("BtnAttack", box, "ATTACCA", "0 AP · chiude la fase", GamePalette.Fronte, out _);
        UiBuild.Band((RectTransform)attack.transform, 0f, y, SideContentW, CommandH);

        y += CommandH + CommandGap;
        var endTurn = UiBuild.Command("BtnEndTurn", box, "CHIUDI TURNO", "0 AP", GamePalette.Retro, out _);
        UiBuild.Band((RectTransform)endTurn.transform, 0f, y, SideContentW, CommandH);

        return (draw, attack, endTurn);
    }

    static void BuildEndPanel(RectTransform root, HudController hud)
    {
        var panel = UiBuild.Rect("EndMatchPanel", root);
        UiBuild.Band(panel, 0f, 0f, RefW, RefH);
        UiBuild.Fill(panel, new Color(0f, 0f, 0f, 0.78f), raycast: true);

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

    static void WireHandManager(HandManager hand, Transform handRoot, Transform spawnPoint, Button draw)
    {
        var so = new SerializedObject(hand);
        so.FindProperty("handRoot").objectReferenceValue = handRoot;
        so.FindProperty("spawnPoint").objectReferenceValue = spawnPoint;
        so.FindProperty("btnDraw").objectReferenceValue = draw;
        // La carta nasce gia' alla dimensione della cella: il moltiplicatore
        // serviva quando il prefab era 100x154 e ora la sparerebbe a 330x495.
        so.FindProperty("spawnScaleMultiplier").floatValue = 1f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
