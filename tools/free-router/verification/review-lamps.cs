using UnityEngine;
using TMPro;
using System.Text;
internal class CommandScript : IRunCommand {
 int checks;
 void Assert(bool ok,string message) { if(!ok) throw new System.Exception(message); checks++; }
 void Check(MedallionInstruments panel,string name,string shape,int value) {
  var bank=panel.transform.Find(name); Assert(bank!=null,"Missing bank "+name);
  int count=0,lit=0;
  foreach(Transform child in bank) {
   if(!child.name.StartsWith("Lamp")) continue;
   count++; var image=child.GetComponent<UnityEngine.UI.Image>();
   Assert(image!=null && image.sprite!=null,"Missing sprite "+name);
   Assert(!image.raycastTarget,"Decorative lamp intercepts input");
   if(image.sprite==UiSkin.Sprite("lamp_v3_"+shape+"_on")) lit++;
   else Assert(image.sprite==UiSkin.Sprite("lamp_v3_"+shape+"_off"),"Unexpected sprite");
  }
  Assert(count==7,"Expected 7 "+name+", found "+count);
  Assert(lit==Mathf.Clamp(value,0,7),name+" expected "+value+" got "+lit);
  var total=bank.Find(name+"Total").GetComponent<TMP_Text>();
  Assert(total.text==(value>7?value.ToString():""),"Incorrect overflow "+name+"="+total.text);
 }
 public void Execute(ExecutionResult result) {
  var gm=GameManager.Instance; Assert(gm!=null,"Missing GameManager");
  var batch=Object.FindAnyObjectByType<SlotBatchManager>();
  Assert(batch==null||!batch.IsRolling,"Cannot sample during reel animation");
  var panels=Object.FindObjectsByType<MedallionInstruments>(FindObjectsSortMode.None);
  Assert(panels.Length==3,"Expected 3 instrument panels");
  var report=new StringBuilder();
  foreach(var panel in panels) {
   var slot=gm.GetEnemySlotAtLane(panel.lane); Assert(slot!=null,"Expected live slot "+panel.lane);
   var spec=slot.def; var side=slot.side; int hp=slot.health;
   int atk=slot.tempAtkBonus,block=slot.tempBlockBonus;
   var originalAtk=new System.Collections.Generic.List<BonusLedger.Entry>(slot.AtkBonuses.Entries);
   var originalBlock=new System.Collections.Generic.List<BonusLedger.Entry>(slot.BlockBonuses.Entries);
   var card=gm.GetPlayerCardAtLane(panel.lane); var faction=slot.def.faction;
   try {
    slot.side=Side.Fronte; slot.health=9;
    if(card!=null)slot.def.faction=card.def.faction==Faction.A?Faction.B:Faction.A;
    // Keep gameplay data assets untouched: Spec is a runtime value type.
    slot.def.atkDamage=-atk;slot.def.blockFront=-block;
    int previous=0;
    foreach(int value in new int[]{0,1,6,7,9,2,0}) {
     slot.AddAtkBonus(value-previous,"lamp-review");slot.AddBlockBonus(value-previous,"lamp-review");previous=value;
     panel.SendMessage("LateUpdate");
     Check(panel,"Attack","spear",value);Check(panel,"Guard","shield",value);
    }
    foreach(int value in new int[]{1,6,7,9,2,0}) {
     slot.health=value;panel.SendMessage("LateUpdate");Check(panel,"Health","round",value);
     if(value==0){Check(panel,"Attack","spear",0);Check(panel,"Guard","shield",0);}
    }
    slot.health=9;slot.AddAtkBonus(9,"lamp-review");slot.AddBlockBonus(9,"lamp-review");
    slot.side=Side.Retro;slot.def.blockRetro=-block;
    panel.SendMessage("LateUpdate");Check(panel,"Attack","spear",0);Check(panel,"Guard","shield",9);
    if(card!=null) {slot.def.faction=card.def.faction;panel.SendMessage("LateUpdate");Check(panel,"Guard","shield",0);}
    // An out-of-range lane is the same null-slot path used by an empty reel.
    int lane=panel.lane;try{panel.lane=99;panel.SendMessage("LateUpdate");Check(panel,"Health","round",0);Check(panel,"Attack","spear",0);Check(panel,"Guard","shield",0);}finally{panel.lane=lane;}
    report.AppendLine("lane="+panel.lane+" stats=0,1,6,7,9,2,0; hold, dead, absent PASS; resonance="+(card!=null?"PASS":"not-present"));
   } finally {
    slot.ClearAtkBonus();slot.ClearBlockBonus();
    foreach(var entry in originalAtk)slot.AddAtkBonus(entry.amount,entry.reason);
    foreach(var entry in originalBlock)slot.AddBlockBonus(entry.amount,entry.reason);
    slot.def=spec;slot.side=side;slot.health=hp;panel.SendMessage("LateUpdate");
   }
  }
  report.AppendLine("PASS assertions="+checks+" camera="+Camera.main.gameObject.GetInstanceID()+" frame="+Time.frameCount);
  System.IO.File.WriteAllText("Logs/lamps-v3-independent-review.txt",report.ToString());
  result.Log(report.ToString());
 }
}
