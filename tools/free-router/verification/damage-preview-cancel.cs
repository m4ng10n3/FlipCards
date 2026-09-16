using System;
using System.IO;
using UnityEngine;
internal class CommandScript : IRunCommand
{
    int checks;
    void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
    public void Execute(ExecutionResult result)
    {
        var gm=GameManager.Instance;var a=gm.GetPlayerCardViewAtLane(0);var b=gm.GetPlayerCardViewAtLane(1);
        var slot=gm.GetEnemySlotViewAtLane(0).instance;
        Check(gm.CanAct && a!=null && b!=null,"Requires ready two-card fixture from forecast test");
        a.instance.side=Side.Fronte;a.instance.health=99;slot.health=5;gm.player.actionPoints=20;
        DamagePreviewController.Show(a);Check(DamagePreviewController.Active!=null,"Preview starts");
        gm.SwapCardPositions(a,b);Check(DamagePreviewController.Active==null,"Swap invalidates old lane");gm.SwapCardPositions(a,b);
        DamagePreviewController.Show(a);slot.health--;Check(DamagePreviewController.Active==null,"Wound invalidates old value");
        DamagePreviewController.Show(a);slot.health=0;Check(DamagePreviewController.Active==null,"Slot death invalidates target");
        DamagePreviewController.Show(a);slot.health=5;Check(DamagePreviewController.Active==null,"New armor invalidates empty lane");
        DamagePreviewController.Show(a);a.instance.flipCharge++;Check(DamagePreviewController.Active==null,"Charge change invalidates damage");
        DamagePreviewController.Show(a);b.instance.side=b.instance.side==Side.Fronte?Side.Retro:Side.Fronte;Check(DamagePreviewController.Active==null,"Adjacent banner flip invalidates damage");
        DamagePreviewController.Show(a);a.IsDragging=true;Check(DamagePreviewController.Active==null,"Drag cancels");a.IsDragging=false;
        DamagePreviewController.Show(a);a.gameObject.SetActive(false);Check(DamagePreviewController.Active==null,"Disabled source cancels");a.gameObject.SetActive(true);
        DamagePreviewController.Show(a);gm.ai.hp--;Check(DamagePreviewController.Active==null,"Boss damage invalidates old HP");
        DamagePreviewController.Show(a);gm.btnEndTurn.onClick.Invoke();Check(DamagePreviewController.Active==null,"End turn cancels before roll");
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/damage-preview-cancel.txt","PASS cancellation assertions="+checks);
        result.Log("Cancellation assertions passed: "+checks);
    }
}
