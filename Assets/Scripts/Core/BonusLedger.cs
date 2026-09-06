using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Il registro dei bonus temporanei: quanto, e <b>da cosa</b>.
///
/// PERCHE' ESISTE: sulla cella di una carta o di una casella compare un numero
/// modificato — "5" dove la carta ne aveva 3 — e prima non c'era nessun posto
/// dove leggere <em>chi</em> gliel'ha dato. Il giocatore vedeva un bonus che non
/// sapeva di avere, quindi non sapeva nemmeno come tenerselo o come averne due:
/// e in un gioco dove la mossa del turno e' spostare le carte, un bonus senza
/// causa e' un bonus inutilizzabile.
///
/// Con il registro il numero e la sua spiegazione nascono insieme, nello stesso
/// punto del codice: chi somma deve dire perche'. L'ispettore non ricostruisce
/// niente, legge. E' l'unico modo per cui la spiegazione non puo' andare fuori
/// sincrono con l'effetto — se un'abilita' nuova aggiunge un bonus, la sua
/// riga nell'ispettore compare da sola.
///
/// I totali restano gli stessi <c>int</c> di prima (<c>tempAtkBonus</c>,
/// <c>tempBlockBonus</c>): chi li legge non cambia. Cambia chi li scrive, che
/// ora passa da <c>Add</c> e non puo' scordarsi la ragione, perche' e' un
/// parametro obbligatorio.
/// </summary>
public class BonusLedger
{
    public readonly struct Entry
    {
        public readonly string reason;
        public readonly int amount;

        public Entry(string reason, int amount)
        {
            this.reason = reason;
            this.amount = amount;
        }

        public override string ToString() => $"{(amount >= 0 ? "+" : "")}{amount} {reason}";
    }

    readonly List<Entry> _entries = new List<Entry>(4);

    /// <summary>Somma dei bonus registrati. E' il valore che finisce nei conti.</summary>
    public int Total { get; private set; }

    public IReadOnlyList<Entry> Entries => _entries;
    public bool Any => _entries.Count > 0;

    /// <summary>
    /// Registra un bonus con la sua causa. Un contributo da zero non si scrive:
    /// una riga "+0 da qualcosa" occupa spazio nell'ispettore senza dire niente.
    /// </summary>
    public void Add(int amount, string reason)
    {
        if (amount == 0) return;

        Total += amount;
        _entries.Add(new Entry(string.IsNullOrEmpty(reason) ? "ignoto" : reason, amount));
    }

    public void Clear()
    {
        Total = 0;
        _entries.Clear();
    }

    /// <summary>Riassunto su una riga, per i log: "+1 insegna, +2 furia".</summary>
    public string Describe()
    {
        if (_entries.Count == 0) return "nessun bonus";

        var sb = new System.Text.StringBuilder(48);
        for (int i = 0; i < _entries.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(_entries[i].ToString());
        }
        return sb.ToString();
    }
}
