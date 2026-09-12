using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CabinetArtVerification
{
    [Serializable] public sealed class Report { public bool success; public int lamps, valueSamples, pixelSamples, resonanceSamples, rectangleSamples, apertureSamples, cameraId, frame; public string limitation; }
    static void Require(bool value,string message) { if(!value)throw new InvalidOperationException(message); }
    public static string Run()
    {
        Require(EditorApplication.isPlaying,"Verification requires Play");
        var controllers=UnityEngine.Object.FindObjectsByType<CabinetLampController>(FindObjectsSortMode.None);
        Require(controllers.Length==1,"Expected one cabinet controller");
        var controller=controllers[0]; var gm=GameManager.Instance;
        Require(controller.LampMaterial!=null && controller.LampCount==63,"Integrated lamps not initialized");
        Require(controller.GetComponentsInChildren<UnityEngine.UI.Image>().Length==1,"Unexpected overlay lamp images");
        var batch=UnityEngine.Object.FindAnyObjectByType<SlotBatchManager>();
        Require(batch==null || !batch.IsRolling,"Wait until reel stops");
        var report=new Report {lamps=63,cameraId=Camera.main.gameObject.GetInstanceID(),frame=Time.frameCount,
            limitation="Deterministic samples and actual GPU rendering; timed reel animation is not certified by this test."};
        var manifest=CabinetArtInstaller.Inspect();
        var windows=controller.LampMaterial.GetVectorArray("_WindowRegions");
        for(int i=0;i<3;i++) {
            var r=manifest.windowRegions[i];
            Require(windows[i]==new Vector4(r.x,r.y,r.width,r.height),"Aperture shader rectangle differs from manifest");
            report.rectangleSamples++;
        }
        foreach(var bank in controller.definition.banks) {
            var actual=controller.transform.Find("Total"+bank.lane+"_"+bank.stat).GetComponent<RectTransform>();
            var parent=((RectTransform)controller.transform).rect;
            var expected=Array.Find(manifest.banks,b=>b.lane==bank.lane&&b.stat==bank.stat).totalRect;
            Require(Mathf.Abs(actual.rect.width-expected.width/controller.definition.sourceSize.x*parent.width)<.1f && Mathf.Abs(actual.rect.height-expected.height/controller.definition.sourceSize.y*parent.height)<.1f && actual.rect.width>0,"Total has an invalid display rectangle");
            report.rectangleSamples++;
            var slot=gm.GetEnemySlotAtLane(bank.lane);Require(slot!=null,"Missing live slot");
            var spec=slot.def;int health=slot.health;var side=slot.side;
            var atk=new List<BonusLedger.Entry>(slot.AtkBonuses.Entries);var block=new List<BonusLedger.Entry>(slot.BlockBonuses.Entries);
            try {
                slot.ClearAtkBonus();slot.ClearBlockBonus();slot.side=Side.Fronte;slot.health=9;
                slot.def.atkDamage=0;slot.def.blockFront=0;
                var card=gm.GetPlayerCardAtLane(bank.lane);
                if(card!=null)slot.def.faction=card.def.faction==Faction.A?Faction.B:Faction.A;
                foreach(int value in new[]{0,1,6,7,9,2,0}) {
                    if(bank.stat==0)slot.health=value;
                    else if(bank.stat==1){slot.ClearAtkBonus();slot.AddAtkBonus(value,"verification");}
                    else{slot.ClearBlockBonus();slot.AddBlockBonus(value,"verification");}
                    controller.RefreshIndicators();
                    var states=controller.LampMaterial.GetVectorArray("_BankStates");
                    int index=Array.FindIndex(controller.definition.banks,b=>b.lane==bank.lane&&b.stat==bank.stat);
                    Require(Mathf.RoundToInt(states[index].x)==value,"Wrong runtime statistic");
                    var total=controller.transform.Find("Total"+bank.lane+"_"+bank.stat).GetComponent<TMPro.TMP_Text>();
                    Require(total.text==(value>7?value.ToString():""),"Wrong total above seven");
                    report.valueSamples++;
                }
                slot.health=0;controller.RefreshIndicators();
                foreach(var state in LaneStates(controller,bank.lane))Require(state.x==0,"Dead slot still illuminated");
                slot.health=9;slot.side=Side.Retro;controller.RefreshIndicators();
                Require(LaneStates(controller,bank.lane)[1].x==0,"Attack lit on Retro");
            } finally {
                slot.def=spec;slot.health=health;slot.side=side;slot.ClearAtkBonus();slot.ClearBlockBonus();
                foreach(var e in atk)slot.AddAtkBonus(e.amount,e.reason);
                foreach(var e in block)slot.AddBlockBonus(e.amount,e.reason);
                controller.RefreshIndicators();
            }
        }
        VerifyPixels(controller,report);
        VerifyResonance(controller,report);
        Require(!ShaderUtil.ShaderHasError(controller.LampMaterial.shader),"Cabinet shader compilation failed");
        report.success=true;
        string json=JsonUtility.ToJson(report);
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/cabinet-v4-verification.json",json);
        return json;
    }
    static Vector4[] LaneStates(CabinetLampController c,int lane) {
        var all=c.LampMaterial.GetVectorArray("_BankStates");var result=new Vector4[3];
        for(int i=0;i<9;i++)if(c.definition.banks[i].lane==lane)result[c.definition.banks[i].stat]=all[i];
        return result;
    }
    static void VerifyPixels(CabinetLampController controller,Report report)
    {
        var camera=Camera.main;var previous=camera.targetTexture;var previousActive=RenderTexture.active;
        var target=RenderTexture.GetTemporary(1920,1080,24);
        var texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        var states=new Vector4[9];var on=new float[63];var rt=(RectTransform)controller.transform;
        try {
            camera.targetTexture=target;
            for(int pass=0;pass<2;pass++) {
                for(int i=0;i<9;i++)states[i]=new Vector4(pass==0?7:0,-1,0,0);
                controller.LampMaterial.SetVectorArray("_BankStates",states);
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,1920,1080),0,0);texture.Apply();
                if(pass==0)foreach(var window in controller.definition.windowRegions) {
                    var p=new Vector2(window.x+window.width*.5f,window.y+14);
                    var local=new Vector3(rt.rect.xMin+p.x/controller.definition.sourceSize.x*rt.rect.width,rt.rect.yMax-p.y/controller.definition.sourceSize.y*rt.rect.height,0);
                    var point=camera.WorldToViewportPoint(rt.TransformPoint(local));
                    var pixel=texture.GetPixel((int)(point.x*1920),(int)(point.y*1080));
                    Require(Mathf.Max(pixel.r,Mathf.Max(pixel.g,pixel.b))<.9f,"White artwork cutout leaks above reel");
                    report.apertureSamples++;
                }
                int index=0;
                foreach(var bank in controller.definition.banks)for(int lamp=0;lamp<7;lamp++) {
                    var p=bank.first+lamp*bank.step;
                    var local=new Vector3(rt.rect.xMin+p.x/controller.definition.sourceSize.x*rt.rect.width,rt.rect.yMax-p.y/controller.definition.sourceSize.y*rt.rect.height,0);
                    var point=camera.WorldToViewportPoint(rt.TransformPoint(local));
                    var pixel=texture.GetPixel(Mathf.Clamp((int)(point.x*1920),0,1919),Mathf.Clamp((int)(point.y*1080),0,1079));
                    float light=Mathf.Max(pixel.r,Mathf.Max(pixel.g,pixel.b));
                    if(pass==0)on[index]=light;
                    else {Require(on[index]>.2f && light<on[index]*.6f,"Glass pixel did not dim: bank/lamp="+index+" on="+on[index]+" off="+light);report.pixelSamples++;}
                    index++;
                }
                Directory.CreateDirectory("Logs");File.WriteAllBytes(pass==0?"Logs/cabinet-v4-all-on.png":"Logs/cabinet-v4-all-off.png",texture.EncodeToPNG());
            }
        } finally {camera.targetTexture=previous;RenderTexture.active=previousActive;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(texture);controller.RefreshIndicators();}
    }
    static void VerifyResonance(CabinetLampController controller,Report report)
    {
        var gm=GameManager.Instance;var source=gm.HandManager.HandRoot.GetComponentInChildren<CardView>();
        Require(source!=null,"Need a card in hand for resonance probe");
        var definition=source.GetComponentInParent<CardDefinition>();
        for(int lane=0;lane<3;lane++) {
            Require(gm.GetPlayerCardAtLane(lane)==null,"Run acceptance on a fresh board");
            var slot=gm.GetEnemySlotAtLane(lane);var spec=definition.BuildSpec();spec.faction=slot.def.faction;
            var instance=new CardInstance(spec,new System.Random(7));
            var clone=UnityEngine.Object.Instantiate(definition.gameObject,gm.playerBoardRoot.GetChild(lane));
            try {
                clone.GetComponentInChildren<CardView>().Init(gm,gm.player,instance);
                Require(SynergyResolver.Resonates(gm,lane),"Resonance did not activate");
                controller.RefreshIndicators();Require(LaneStates(controller,lane)[2].x==0,"Resonance guard remains lit");
                instance.def.faction=slot.def.faction==Faction.A?Faction.B:Faction.A;
                controller.RefreshIndicators();Require(LaneStates(controller,lane)[2].x==slot.ComputeSelfBlock(),"Guard did not recover");
                report.resonanceSamples+=2;
            } finally {clone.transform.SetParent(null);UnityEngine.Object.DestroyImmediate(clone);instance.Dispose();controller.RefreshIndicators();}
        }
    }
}
