using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Nome leggibile e descrizione di ogni abilita'. Le abilita' sono componenti
/// senza etichetta: senza questa tabella l'unica cosa mostrabile sarebbe il nome
/// del tipo C#.
/// </summary>
public static class AbilityCatalog
{
    struct Entry { public string glyph, name, effect; }

    static readonly Dictionary<string, Entry> Table = new Dictionary<string, Entry>
    {
        // Carte
        ["VanguardStrike"]     = new Entry { glyph = "A", name = "Vanguard Strike",  effect = "piu' danno quando attacca in Fronte" },
        ["ChargeBoost"]        = new Entry { glyph = "C", name = "Charge Boost",     effect = "oltre la soglia di cariche piu' danno; a 3 cariche colpisce tutti gli slot" },
        ["ClassSynergyBoost"]  = new Entry { glyph = "S", name = "Class Synergy",    effect = "piu' danno se una carta adiacente e' della stessa classe" },
        ["AdjacencyShield"]    = new Entry { glyph = "D", name = "Adjacency Shield", effect = "in Fronte passa block a una carta adiacente sotto attacco" },
        ["RetroShield"]        = new Entry { glyph = "R", name = "Retro Shield",     effect = "in Retro aggiunge block quando viene attaccata" },
        ["BlockAllAttacks"]    = new Entry { glyph = "0", name = "Block All",        effect = "azzera il danno in arrivo" },
        ["PulseHeal"]          = new Entry { glyph = "+", name = "Pulse Heal",       effect = "cura a ogni flip" },
        ["OnFlipDealDamage"]   = new Entry { glyph = "F", name = "Flip Strike",      effect = "al flip colpisce lo slot della sua corsia, o il boss se la corsia e' vuota" },
        ["OnFlipGainAP"]       = new Entry { glyph = "P", name = "Flip Momentum",    effect = "al flip restituisce AP" },
        ["OnEndTurnDealDamage"]= new Entry { glyph = "E", name = "End Turn Strike",  effect = "attacca a fine turno" },
        ["GetBonusBack"]       = new Entry { glyph = "B", name = "Faction Aura",     effect = "in Retro applica i bonus di fazione alle altre carte" },

        // Slot
        ["SlotArmorFront"]   = new Entry { glyph = "A", name = "Armatura",     effect = "in Fronte riduce il danno in arrivo" },
        ["SlotBerserker"]    = new Entry { glyph = "!", name = "Berserker",    effect = "accumula furia ogni turno in Fronte; alla soglia colpisce doppio" },
        ["SlotRetroRegen"]   = new Entry { glyph = "+", name = "Rigenerazione",effect = "recupera HP passando in Retro" },
        ["SlotAdjacentBuff"] = new Entry { glyph = "L", name = "Legame",       effect = "in Retro da' block agli slot adiacenti" },
        ["SlotStrikeOnAct"]  = new Entry { glyph = "*", name = "Comportamento",effect = "firma parametrica: replica gli effetti sopra" },
    };

    public static string Describe(Component ability)
    {
        if (ability == null) return string.Empty;
        string type = ability.GetType().Name;
        return Table.TryGetValue(type, out var e)
            ? $"<b>{e.name}</b> — {e.effect}"
            : $"<b>{Humanize(type)}</b>";
    }

    /// <summary>Glifo compatto per le icone sulla cella: il dettaglio sta nell'ispettore.</summary>
    public static string Glyph(Component ability)
    {
        if (ability == null) return "?";
        string type = ability.GetType().Name;
        return Table.TryGetValue(type, out var e) ? e.glyph : type.Substring(0, 1);
    }

    public static string Name(Component ability)
    {
        if (ability == null) return string.Empty;
        string type = ability.GetType().Name;
        return Table.TryGetValue(type, out var e) ? e.name : Humanize(type);
    }

    static string Humanize(string typeName)
    {
        var sb = new System.Text.StringBuilder(typeName.Length + 6);
        for (int i = 0; i < typeName.Length; i++)
        {
            if (i > 0 && char.IsUpper(typeName[i]) && !char.IsUpper(typeName[i - 1])) sb.Append(' ');
            sb.Append(typeName[i]);
        }
        return sb.ToString();
    }
}
