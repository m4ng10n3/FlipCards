using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Modal reading surfaces. Does not change AP, selection, turn state or time scale.</summary>
public sealed class TableOverlayController : MonoBehaviour
{
    public static TableOverlayController Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.modal != null && Instance.modal.activeSelf;
    public GameObject modal, detail, legend, logPanel;
    public RectTransform choices;
    public InspectorPanel inspector;
    public TMP_Text heading;
    public string CurrentCategory { get; private set; }
    void Awake() { Instance = this; Close(); }
    void OnDestroy() { if (Instance == this) Instance = null; }
    void Update()
    {
        if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        if (IsOpen && GameManager.Instance != null && GameManager.Instance.MatchEnded) Close();
    }
    public void Close()
    {
        if (modal != null) modal.SetActive(false);
        if (inspector != null) inspector.Clear();
    }
    public void OpenDetail()
    {
        modal.SetActive(true); detail.SetActive(true); legend.SetActive(false);
        heading.text = "DETTAGLIO";
        inspector.Clear(); ShowCategory("Campo");
    }
    /// <summary>La pergamena ha il suo titolo stampato: non tocca quello del libretto.</summary>
    public void OpenLegend()
    {
        modal.SetActive(true); detail.SetActive(false); legend.SetActive(true);
        logPanel.SetActive(false);
    }
    public void ShowField() => ShowCategory("Campo");
    public void ShowHand() => ShowCategory("Mano");
    public void ShowReels() => ShowCategory("Rullo");
    public void ShowLog()
    {
        choices.gameObject.SetActive(false); inspector.gameObject.SetActive(false); logPanel.SetActive(true);
    }
    public void ShowCategory(string category)
    {
        CurrentCategory = category;
        choices.gameObject.SetActive(true); inspector.gameObject.SetActive(true); logPanel.SetActive(false);
        foreach (Transform child in choices) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        inspector.Clear();
        var gm = GameManager.Instance;
        if (gm == null) return;
        int index = 0;
        if (category == "Campo")
        {
            foreach (var view in gm.playerBoardRoot.GetComponentsInChildren<CardView>())
                if (view.instance != null) AddChoice(view.instance.def.cardName, view, index++);
        }
        else if (category == "Mano")
        {
            foreach (var def in gm.HandManager.HandRoot.GetComponentsInChildren<CardDefinition>())
                AddChoice(def.cardName, def, index++);
        }
        else
        {
            foreach (var view in gm.aiBoardRoot.GetComponentsInChildren<SlotView>())
                if (view.instance != null) AddChoice(view.instance.def.SlotName, view, index++);
        }
        if (index == 0)
        {
            var empty = UiBuild.Text("Empty", choices, "Nessun elemento in questa zona.", 18, GamePalette.InkMuted);
            UiBuild.Band(empty.rectTransform, 8, 16, 400, 60);
            empty.textWrappingMode = TextWrappingModes.Normal;
        }
    }

    /// <summary>Una voce dell'indice sulla pagina sinistra: riga d'inchiostro su carta.</summary>
    void AddChoice(string label, Object source, int index)
    {
        var rt = UiBuild.Rect("Inspect_" + index, choices);
        UiBuild.Band(rt, 0, index * 64, 640, 54);
        var background = UiBuild.Fill(rt, new Color(.80f,.74f,.60f,.55f), true);
        var button = rt.gameObject.AddComponent<Button>(); button.targetGraphic = background;
        var text = UiBuild.Text("Label", rt, label, 21, GamePalette.InkStrong);
        text.font = UiBuild.Font; text.fontSize = 21;
        UiBuild.Stretch(text.rectTransform, 16, 6, 12, 6);
        button.onClick.AddListener(() => { if (source != null) inspector.InspectSelection(source); else ShowCategory(CurrentCategory); });
    }
}
