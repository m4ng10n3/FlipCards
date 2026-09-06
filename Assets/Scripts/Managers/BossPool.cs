using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Le caselle del boss sono un mazzo numerato, non un generatore casuale.
///
/// PERCHE': prima il rullo pescava una casella nuova a ogni giro e il danno non
/// letale spariva con lei. Attaccare senza uccidere in un colpo solo era tempo
/// buttato, e il giocatore non aveva modo di sapere quante caselle mancassero
/// alla fine. Qui il pool e' la corazza del boss: N caselle numerate, ognuna con
/// la sua vita, che il rullo ripesca finche' sono vive.
///
/// Le due regole che ne discendono, ed e' tutta la meccanica:
///  - la vita resta sulla casella. Se la #4 se ne va con 2 ferite, quando
///    ritorna ha ancora 2 ferite. Colpire serve sempre.
///  - uccidere una casella la toglie dal pool per il resto della partita. Il
///    rullo si accorcia, e si vede: le facce che girano sono quelle vive.
///
/// Quando il pool si svuota il boss resta scoperto e le corsie lo colpiscono
/// direttamente: e' la fine della partita, non un caso limite.
/// </summary>
public class BossPool
{
    public class Entry
    {
        /// <summary>Il numero stampato sulla casella: 1..N, stabile per tutta la partita.</summary>
        public int number;
        public GameObject prefab;
        public SlotDefinition.Spec spec;
        public int health;
        public bool alive => health > 0;
        public bool Wounded => health < spec.maxHealth;

        public override string ToString() => $"#{number} {spec.SlotName} {health}/{spec.maxHealth}";
    }

    readonly List<Entry> entries = new List<Entry>();
    readonly Dictionary<GameObject, Entry> byPrefab = new Dictionary<GameObject, Entry>();

    public IReadOnlyList<Entry> Entries => entries;

    public int Count => entries.Count;

    public int AliveCount
    {
        get
        {
            int n = 0;
            foreach (var e in entries) if (e.alive) n++;
            return n;
        }
    }

    /// <summary>Vita residua di tutto il pool: e' la vera barra della corazza.</summary>
    public int AliveHealth
    {
        get
        {
            int n = 0;
            foreach (var e in entries) if (e.alive) n += e.health;
            return n;
        }
    }

    public int TotalHealth
    {
        get
        {
            int n = 0;
            foreach (var e in entries) n += e.spec.maxHealth;
            return n;
        }
    }

    /// <summary>
    /// Costruisce il pool dai prefab, in ordine: l'indice nella lista diventa il
    /// numero della casella. healthScale e' la manopola della difficolta'.
    /// </summary>
    public void Build(IEnumerable<GameObject> prefabs, float healthScale, float attackScale)
    {
        entries.Clear();
        byPrefab.Clear();
        if (prefabs == null) return;

        foreach (var prefab in prefabs)
        {
            if (prefab == null || byPrefab.ContainsKey(prefab)) continue;
            var definition = prefab.GetComponent<SlotDefinition>();
            if (definition == null) continue;

            var spec = definition.BuildSpec();
            spec.maxHealth = Mathf.Max(1, Mathf.RoundToInt(spec.maxHealth * healthScale));
            spec.atkDamage = Mathf.Max(1, Mathf.RoundToInt(spec.atkDamage * attackScale));

            var entry = new Entry
            {
                number = entries.Count + 1,
                prefab = prefab,
                spec = spec,
                health = spec.maxHealth,
            };
            entries.Add(entry);
            byPrefab[prefab] = entry;
        }
    }

    public Entry Of(GameObject prefab)
        => prefab != null && byPrefab.TryGetValue(prefab, out var e) ? e : null;

    /// <summary>I prefab ancora in gioco: e' da qui che il rullo pesca, e sono le facce che mostra.</summary>
    public List<GameObject> AlivePrefabs()
    {
        var list = new List<GameObject>(entries.Count);
        foreach (var e in entries) if (e.alive) list.Add(e.prefab);
        return list;
    }

    /// <summary>Riassunto per la HUD: "corazza 5/10 caselle - 23 vita".</summary>
    public string Summary() => $"{AliveCount}/{entries.Count} caselle · {AliveHealth} vita";
}
