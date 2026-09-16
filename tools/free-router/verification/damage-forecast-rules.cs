using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

internal class CommandScript : IRunCommand
{
    GameManager gm; CardView view; SlotView target; CardInstance card; SlotInstance slot;
    readonly StringBuilder log = new StringBuilder();
    public void Execute(ExecutionResult result)
    {
        gm=GameManager.Instance;
        if(gm==null || !gm.CanAct)throw new Exception("Fresh Play, ready for actions required");
        var cards=new List<CardView>(gm.HandManager.HandRoot.GetComponentsInChildren<CardView>());
        gm.player.actionPoints=20;
        for(int lane=0;lane<2;lane++){gm.OnEmptySpotClicked(gm.playerBoardRoot.GetChild(lane));gm.OnCardClicked(cards[lane]);}
        gm.StartCoroutine(Run()); result.Log("Forecast/combat comparison started");
    }
    IEnumerator Run()
    {
        for(int i=0;i<25;i++)yield return null;
        try {
            view=gm.GetPlayerCardViewAtLane(0);target=gm.GetEnemySlotViewAtLane(0);card=view.instance;slot=target.instance;
            for(int lane=0;lane<3;lane++){
                var c=gm.GetPlayerCardViewAtLane(lane);var s=gm.GetEnemySlotViewAtLane(lane);
                if(c!=null)foreach(var a in c.GetComponents<AbilityBase>())a.Unbind();
                if(s!=null)foreach(var a in s.GetComponents<AbilityBase>())a.Unbind();
            }
            Setup();Compare("basic");
            Setup();var v=view.gameObject.AddComponent<VanguardStrike>();v.bonusDamage=2;v.Bind(card,gm.player,gm.ai);Compare("vanguard");v.Unbind();
            Setup();card.flipCharge=3;var charge=view.gameObject.AddComponent<ChargeBoost>();charge.chargeThreshold=2;charge.bonusDamage=1;charge.splashDamage=1;charge.Bind(card,gm.player,gm.ai);Compare("charge");charge.Unbind();
            Setup();var neighbor=gm.GetPlayerCardAtLane(1);neighbor.def.cardClass=card.def.cardClass;
            var same=view.gameObject.AddComponent<ClassSynergyBoost>();same.bonusDamage=2;same.Bind(card,gm.player,gm.ai);Compare("same-class");same.Unbind();
            Setup();neighbor.side=Side.Retro;neighbor.def.faction=card.def.faction;neighbor.def.backDamageBonusSameFaction=2;Compare("banner");neighbor.def.backDamageBonusSameFaction=0;
            Setup();var armor=target.gameObject.AddComponent<SlotArmorFront>();armor.armorValue=3;armor.Bind(null,gm.ai,gm.player);Compare("reactive-armor");
            Setup();slot.def.faction=card.def.faction;Compare("resonance-skips-armor");armor.Unbind();
            Setup();var strike=target.gameObject.AddComponent<SlotStrikeOnAct>();strike.signature=SlotStrikeOnAct.SlotSignature.ArmorFront;strike.power=2;strike.Bind(null,gm.ai,gm.player);Compare("signature-armor");strike.Unbind();
            Setup();slot.incomingDamageOverride=0;Compare("incoming-override");
            Setup();slot.health=0;Compare("direct-hole");
            log.AppendLine("PASS ten forecast/actual-combat cases");
        } catch(Exception ex){log.AppendLine("FAIL "+ex);}
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/damage-forecast-rules.txt",log.ToString());
    }
    void Setup()
    {
        DamagePreviewController.Instance?.Cancel();
        card.health=99;card.side=Side.Fronte;card.def.faction=Faction.A;card.def.frontDamage=5;card.flipCharge=0;card.ClearAtkBonus();card.ClearBlockBonus();
        slot.health=3;slot.side=Side.Fronte;slot.def.faction=Faction.B;slot.def.blockFront=2;slot.def.atkDamage=0;slot.ClearCombatBonuses();gm.ai.hp=100;
        var neighbor=gm.GetPlayerCardAtLane(1);neighbor.side=Side.Fronte;neighbor.def.backDamageBonusSameFaction=0;
    }
    void Compare(string name)
    {
        DamagePreviewController.Show(view);
        var p=DamagePreviewController.Active;if(p==null)throw new Exception(name+": missing preview");
        int expectedHealth=slot.health-p.HealthLost,expectedBoss=gm.ai.hp-p.BossDamage,expectedAttack=p.Attack;
        DamagePreviewController.Instance.Cancel();
        card.ClearAtkBonus();card.ClearBlockBonus();
        EventBus.Publish(GameEventType.Custom,new EventContext{owner=gm.player,opponent=gm.ai,phase="PrepareBattle"});
        SynergyResolver.Resolve(gm,gm.player,gm.ai);
        LaneResolver.Resolve(0,card,slot.alive?slot:null,gm.player,gm.ai);
        if(slot.health!=expectedHealth || gm.ai.hp!=expectedBoss)
            throw new Exception(name+": forecast health/boss="+expectedHealth+"/"+expectedBoss+" actual="+slot.health+"/"+gm.ai.hp);
        log.AppendLine(name+" PASS attack="+expectedAttack+" slot="+slot.health+" boss="+gm.ai.hp);
    }
}
