using UnityEngine;

/// <summary>A read-only forecast shared by the cabinet and HUD. Gameplay owns all real values.</summary>
public sealed class DamagePreviewController : MonoBehaviour
{
    public const float Duration = 2.8f;
    public static DamagePreviewController Instance { get; private set; }
    public static DamagePreviewController Active => Instance != null && Instance.Validate() ? Instance : null;
    public int Lane { get; private set; }
    public int Attack { get; private set; }
    public int Guard { get; private set; }
    public int GuardAbsorbed { get; private set; }
    public int HealthLost { get; private set; }
    public int BossDamage { get; private set; }
    public int BossHpBefore { get; private set; }
    public float Elapsed => Time.unscaledTime - _started;
    public float Blink => .18f + .82f * (.5f + .5f * Mathf.Cos(Elapsed * Mathf.PI * 4f));
    public int DisplayedBossHp => Mathf.RoundToInt(Mathf.Lerp(BossHpBefore,
        Mathf.Max(0, BossHpBefore - BossDamage), Mathf.Clamp01(Elapsed / .65f)));

    bool _active;
    float _started;
    int _turn;
    CardView _view;
    CardInstance _card;
    CardInstance[] _cards;
    SlotInstance[] _slots;
    int[] _cardStates, _slotStates, _healthLosses;

    public static void Show(CardView view)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (Instance == null) gm.gameObject.AddComponent<DamagePreviewController>();
        Instance.StartPreview(view);
    }
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }
    void Update() => Validate();
    void OnDisable() => Cancel();
    void OnDestroy() { if (Instance == this) Instance = null; }
    public void Cancel() { _active = false; _view = null; _card = null; }

    public void StartPreview(CardView view)
    {
        Cancel();
        var gm = GameManager.Instance;
        if (gm == null || !gm.CanAct || gm.MatchEnded || view == null || view.owner != gm.player || view.instance == null) return;
        var card = view.instance;
        int lane = gm.GetLaneIndexFor(card);
        if (lane < 0 || !card.alive || card.side != Side.Fronte || gm.GetPlayerCardAtLane(lane) != card) return;
        int count = Mathf.Max(gm.playerBoardRoot.childCount, gm.aiBoardRoot.childCount);
        _cards = new CardInstance[count]; _slots = new SlotInstance[count];
        _cardStates = new int[count]; _slotStates = new int[count]; _healthLosses = new int[count];
        _view = view; _card = card; Lane = lane; _turn = gm.CurrentTurn;
        Attack = SynergyResolver.ForecastCardAttack(gm, lane);
        var slot = gm.GetEnemySlotAtLane(lane);
        Guard = slot != null ? SynergyResolver.ForecastSlotBlock(gm, lane) : 0;
        int incoming = Mathf.Max(0, slot != null ? slot.incomingDamageOverride ?? Attack : Attack);
        GuardAbsorbed = Mathf.Min(incoming, Guard);
        int net = Mathf.Max(0, incoming - Guard);
        HealthLost = slot != null ? Mathf.Min(net, slot.health) : 0;
        BossDamage = net - HealthLost;
        BossHpBefore = gm.ai.hp;
        for (int i = 0; i < count; i++)
        {
            _cards[i] = gm.GetPlayerCardAtLane(i); _slots[i] = gm.GetEnemySlotAtLane(i);
            _cardStates[i] = CardState(_cards[i]); _slotStates[i] = SlotState(_slots[i]);
            _healthLosses[i] = i == lane ? HealthLost : 0;
        }
        // Charge burst happens before the primary hit, only when there is a target.
        if (slot != null)
            foreach (var charge in view.GetComponents<ChargeBoost>())
                if (charge.IsBound && card.flipCharge >= charge.chargeThreshold && card.flipCharge >= CardInstance.MaxFlipCharge)
                    for (int i = 0; i < count; i++)
                        if (i != lane && _slots[i] != null)
                            _healthLosses[i] = Mathf.Min(_slots[i].health, _healthLosses[i] + Mathf.Max(0, charge.splashDamage));
        _started = Time.unscaledTime; _active = true;
    }
    public int HealthLossAt(int lane) => lane >= 0 && lane < _healthLosses.Length ? _healthLosses[lane] : 0;
    public int HealthBeforeAt(int lane) => lane >= 0 && lane < _slots.Length && _slots[lane] != null ? _slots[lane].health : 0;

    bool Validate()
    {
        if (!_active) return false;
        var gm = GameManager.Instance;
        if (gm == null || !gm.CanAct || gm.MatchEnded || gm.CurrentTurn != _turn || gm.ai.hp != BossHpBefore ||
            _view == null || !_view.isActiveAndEnabled || _view.instance != _card || _view.IsDragging ||
            gm.GetLaneIndexFor(_card) != Lane || Elapsed >= Duration ||
            Mathf.Max(gm.playerBoardRoot.childCount, gm.aiBoardRoot.childCount) != _cards.Length)
        { Cancel(); return false; }
        for (int i = 0; i < _cards.Length; i++)
            if (gm.GetPlayerCardAtLane(i) != _cards[i] || gm.GetEnemySlotAtLane(i) != _slots[i] ||
                CardState(_cards[i]) != _cardStates[i] || SlotState(_slots[i]) != _slotStates[i])
            { Cancel(); return false; }
        return true;
    }
    // Fingerprint only reads relevant values. It never calls events or advances RNG.
    static int CardState(CardInstance card)
    {
        if (card == null) return 0;
        unchecked {
            int h = card.health; h = h * 31 + (int)card.side; h = h * 31 + (int)card.def.faction;
            h = h * 31 + card.ComputeAttackDamage(); h = h * 31 + card.flipCharge;
            h = h * 31 + card.def.backDamageBonusSameFaction; h = h * 31 + card.def.backBlockBonusSameFaction;
            return h * 31 + (int)card.def.cardClass;
        }
    }
    static int SlotState(SlotInstance slot)
    {
        if (slot == null) return 0;
        unchecked {
            int h = slot.health; h = h * 31 + (int)slot.side; h = h * 31 + (int)slot.def.faction;
            h = h * 31 + slot.ComputeSelfBlock(); return h * 31 + (slot.incomingDamageOverride ?? -1);
        }
    }
}
