using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Due regole, e sono tutta la sinergia del gioco.
///
///  1. INSEGNA — una carta coperta e' un'insegna. Mostra sul retro il suo
///     simbolo nel colore della fazione: spada col numero, scudo col numero.
///     Da' quel numero alle carte ADIACENTI DELLA STESSA FAZIONE: la spada in
///     attacco, lo scudo in difesa. Non a se stessa, non a tutto il tavolo.
///
///  2. RISONANZA — se in una corsia la carta e la casella nemica sono della
///     stessa fazione, in quella corsia NESSUNO DEI DUE PARA. Il tuo colpo
///     passa attraverso la guardia della casella, e il suo passa attraverso la
///     tua.
///
/// PERCHE' COSI': prima le combo erano quattro (Blade Pair, Guard Link, Mystic
/// Pulse, risonanza +1), tutte fondate sulla classe della carta, che sul
/// tavolo non si vede. Il giocatore subiva bonus che non sapeva di avere. Ora
/// le regole guardano due cose entrambe stampate sulla carta e sulla casella —
/// la fazione (colore) e il lato — e una terza che il giocatore controlla: la
/// posizione.
///
/// E' li' il punto. Le caselle cambiano a ogni giro e le carte si girano da
/// sole a fine turno: la fila giusta di ieri oggi non lo e' piu'. Spostare le
/// carte non e' una mossa di ripiego, e' LA mossa del turno — si mette
/// l'insegna accanto a chi deve colpire, e si sceglie in quale corsia
/// risuonare, perche' la risonanza taglia da tutte e due le parti: sfondi, ma
/// resti scoperto.
///
/// I bonus vengono da qui e solo da qui, e il pronostico di corsia chiama gli
/// stessi metodi: quello che si legge prima di attaccare e' quello che
/// succede.
/// </summary>
public static class SynergyResolver
{
    // Lista di servizio per non allocare a ogni corsia risolta.
    static readonly List<Contribution> _scratch = new List<Contribution>(2);

    public static void Resolve(GameManager gm, PlayerState player, PlayerState ai)
    {
        if (gm == null) return;

        for (int lane = 0; lane < gm.playerBoardRoot.childCount; lane++)
        {
            var card = gm.GetPlayerCardAtLane(lane);
            if (card == null || !card.alive) continue;

            int atk = AttackBonus(gm, lane);
            int def = BlockBonus(gm, lane);

            if (atk > 0)
            {
                // Il bonus si registra con il nome di chi lo da': e' quello che
                // l'ispettore mostra, e cosi non puo' divergere dall'effetto.
                AttackBonus(gm, lane, _scratch);
                foreach (var r in _scratch) card.AddAtkBonus(r.amount, r.ToString());
                card.PushHint($"INSEGNA +{atk} ATT");
                Logger.Info($"Insegna: corsia {lane + 1} {card.def.cardName} +{atk} attacco ({card.AtkBonuses.Describe()})");
            }

            if (def > 0)
            {
                BlockBonus(gm, lane, _scratch);
                foreach (var r in _scratch) card.AddBlockBonus(r.amount, r.ToString());
                card.PushHint($"INSEGNA +{def} DIF");
                Logger.Info($"Insegna: corsia {lane + 1} {card.def.cardName} +{def} guardia ({card.BlockBonuses.Describe()})");
            }

            if (Resonates(gm, lane))
            {
                card.PushHint("RISONANZA: niente guardie");
                Logger.Info($"Risonanza: lane {lane + 1} {card.def.faction} - nessuno para");
            }
        }
    }

    /// <summary>
    /// Carta e casella della stessa fazione nella stessa corsia. Toglie la
    /// guardia a entrambi: e' un'occasione e un rischio nello stesso momento.
    /// </summary>
    public static bool Resonates(GameManager gm, int lane)
    {
        if (gm == null) return false;
        var card = gm.GetPlayerCardAtLane(lane);
        var slot = gm.GetEnemySlotAtLane(lane);
        return card != null && slot != null && slot.alive && card.def.faction == slot.def.faction;
    }

    /// <summary>Una carta coperta della fazione chiesta: e' un'insegna accesa.</summary>
    static bool IsBanner(CardInstance card, Faction faction)
        => card != null && card.alive && card.side == Side.Retro && card.def.faction == faction;

    /// <summary>
    /// Da dove viene un pezzo di bonus: quanto, e chi lo sta dando.
    ///
    /// Serve all'ispettore, che non deve spiegare "+1" ma "+1 dall'insegna di
    /// Scythe nella corsia 1". Un numero senza causa e' un numero che il
    /// giocatore non puo' cambiare, e tutto il gioco sta nel cambiarlo
    /// spostando le carte.
    /// </summary>
    public readonly struct Contribution
    {
        public readonly string who;
        public readonly int lane;      // corsia della carta che lo da', 1-based
        public readonly int amount;

        public Contribution(string who, int lane, int amount)
        {
            this.who = who;
            this.lane = lane;
            this.amount = amount;
        }

        public override string ToString() => $"insegna di {who}, corsia {lane}";
    }

    /// <summary>
    /// Spade puntate su questa corsia dalle insegne accanto. Passando
    /// <paramref name="reasons"/> si ottiene anche **chi** le sta puntando: la
    /// somma e l'elenco escono dallo stesso ciclo, quindi non possono divergere.
    /// </summary>
    public static int AttackBonus(GameManager gm, int lane, List<Contribution> reasons = null)
    {
        reasons?.Clear();

        var card = gm.GetPlayerCardAtLane(lane);
        if (card == null || card.side != Side.Fronte) return 0;

        int bonus = 0;
        for (int i = lane - 1; i <= lane + 1; i += 2)
        {
            var neighbour = gm.GetPlayerCardAtLane(i);
            if (!IsBanner(neighbour, card.def.faction)) continue;

            int amount = Mathf.Max(0, neighbour.def.backDamageBonusSameFaction);
            if (amount <= 0) continue;

            bonus += amount;
            reasons?.Add(new Contribution(neighbour.def.cardName, i + 1, amount));
        }
        return bonus;
    }

    /// <summary>Scudi puntati su questa corsia dalle insegne accanto.</summary>
    public static int BlockBonus(GameManager gm, int lane, List<Contribution> reasons = null)
    {
        reasons?.Clear();

        var card = gm.GetPlayerCardAtLane(lane);
        if (card == null) return 0;

        int bonus = 0;
        for (int i = lane - 1; i <= lane + 1; i += 2)
        {
            var neighbour = gm.GetPlayerCardAtLane(i);
            if (!IsBanner(neighbour, card.def.faction)) continue;

            int amount = Mathf.Max(0, neighbour.def.backBlockBonusSameFaction);
            if (amount <= 0) continue;

            bonus += amount;
            reasons?.Add(new Contribution(neighbour.def.cardName, i + 1, amount));
        }
        return bonus;
    }

    /// <summary>
    /// A chi sta servendo l'insegna di questa carta, adesso. E' la domanda che
    /// il giocatore si fa guardando una carta coperta: la tengo qui o la sposto?
    /// </summary>
    public static void CollectBannerTargets(GameManager gm, int lane, List<Contribution> into)
    {
        into.Clear();

        var source = gm.GetPlayerCardAtLane(lane);
        if (source == null || source.side != Side.Retro) return;

        int atk = Mathf.Max(0, source.def.backDamageBonusSameFaction);
        int def = Mathf.Max(0, source.def.backBlockBonusSameFaction);
        if (atk <= 0 && def <= 0) return;

        for (int i = lane - 1; i <= lane + 1; i += 2)
        {
            var target = gm.GetPlayerCardAtLane(i);
            if (target == null || !target.alive) continue;
            if (target.def.faction != source.def.faction) continue;

            // La spada vale solo per chi e' scoperto: da coperto non attacca.
            int given = (target.side == Side.Fronte ? atk : 0) + def;
            if (given <= 0) continue;

            into.Add(new Contribution(target.def.cardName, i + 1, given));
        }
    }

    /// <summary>
    /// La guardia che conta davvero in questa corsia: zero se risuona.
    /// La usano il pronostico e l'ispettore, cosi il numero mostrato e' quello
    /// che verra' sottratto per davvero.
    /// </summary>
    public static int EffectiveCardBlock(GameManager gm, int lane)
    {
        var card = gm.GetPlayerCardAtLane(lane);
        if (card == null) return 0;
        if (Resonates(gm, lane)) return 0;
        int baseBlock = card.side == Side.Fronte ? card.def.frontBlockValue : card.def.backBlockValue;
        return Mathf.Max(0, baseBlock + BlockBonus(gm, lane));
    }

    /// <summary>Idem per la casella nemica.</summary>
    public static int EffectiveSlotBlock(GameManager gm, int lane)
    {
        var slot = gm.GetEnemySlotAtLane(lane);
        if (slot == null) return 0;
        return Resonates(gm, lane) ? 0 : slot.ComputeSelfBlock();
    }
}
