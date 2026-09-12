using UnityEngine;
internal class CommandScript : IRunCommand {
 public void Execute(ExecutionResult result) {
  var gm=GameManager.Instance;
  var source=gm.HandManager.HandRoot.GetComponentInChildren<CardView>();
  var definition=source.GetComponentInParent<CardDefinition>();
  if(definition==null)throw new System.Exception("Missing source card definition");
  int samples=0;
  foreach(var panel in Object.FindObjectsByType<MedallionInstruments>(FindObjectsSortMode.None)) {
   var slot=gm.GetEnemySlotAtLane(panel.lane);
   var spec=definition.BuildSpec();spec.faction=slot.def.faction;
   var instance=new CardInstance(spec,new System.Random(7));
   var clone=Object.Instantiate(definition.gameObject,gm.playerBoardRoot.GetChild(panel.lane));
   result.RegisterObjectCreation(clone);
   try {
    clone.GetComponentInChildren<CardView>().Init(gm,gm.player,instance);
    if(!SynergyResolver.Resonates(gm,panel.lane))throw new System.Exception("Resonance did not activate");
    panel.SendMessage("LateUpdate");
    for(int i=0;i<7;i++)if(panel.transform.Find("Guard/Lamp"+i).GetComponent<UnityEngine.UI.Image>().sprite!=UiSkin.Sprite("lamp_v3_shield_off"))throw new System.Exception("Resonance must extinguish guard");
    instance.def.faction=slot.def.faction==Faction.A?Faction.B:Faction.A;
    panel.SendMessage("LateUpdate");
    int lit=0;
    for(int i=0;i<7;i++)if(panel.transform.Find("Guard/Lamp"+i).GetComponent<UnityEngine.UI.Image>().sprite==UiSkin.Sprite("lamp_v3_shield_on"))lit++;
    if(lit!=Mathf.Clamp(slot.ComputeSelfBlock(),0,7))throw new System.Exception("Guard did not recover");
    samples+=2;
   } finally { clone.transform.SetParent(null); result.DestroyObject(clone);instance.Dispose();panel.SendMessage("LateUpdate"); }
  }
  System.IO.File.WriteAllText("Logs/lamps-v3-resonance-review.txt","PASS resonance/recovery samples="+samples);
  result.Log("PASS resonance/recovery samples="+samples);
 }
}
