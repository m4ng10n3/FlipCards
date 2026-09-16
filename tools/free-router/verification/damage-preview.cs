using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// Run via Unity_RunCommand in a fresh Play session. The fixture is discarded
// when exiting Play; no assets or scenes are saved by this probe.
internal class CommandScript : IRunCommand
{
    GameManager gm;
    CabinetLampController lamps;
    HudController hud;
    CardView card;
    SlotInstance slot;
    readonly StringBuilder report = new StringBuilder();
    int assertions;

    public void Execute(ExecutionResult result)
    {
        gm = GameManager.Instance;
        Require(gm != null && gm.CanAct, "Start on a fresh, ready board");
        lamps = UnityEngine.Object.FindAnyObjectByType<CabinetLampController>();
        hud = UnityEngine.Object.FindAnyObjectByType<HudController>();
        Require(lamps != null && lamps.LampMaterial != null && hud.bossHpText != null, "Presentation initialized");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/damage-preview-acceptance.txt", "RUNNING\n");
        gm.StartCoroutine(Guard(Run()));
        result.Log("Preview acceptance started; read Logs/damage-preview-acceptance.txt after completion.");
    }

    IEnumerator Guard(IEnumerator routine)
    {
        while (true)
        {
            bool more;
            try { more = routine.MoveNext(); }
            catch (Exception ex)
            {
                report.AppendLine("FAIL " + ex.Message);
                File.WriteAllText("Logs/damage-preview-acceptance.txt", report.ToString());
                yield break;
            }
            if (!more) break;
            yield return routine.Current;
        }
        report.AppendLine("PASS assertions=" + assertions);
        File.WriteAllText("Logs/damage-preview-acceptance.txt", report.ToString());
    }

    IEnumerator Run()
    {
        int initialFrame = Time.frameCount;
        for (int i = 0; i < 25; i++) yield return null;
        Require(Time.frameCount > initialFrame + 20, "Actual player frames advance");
        Require(gm.GetPlayerCardAtLane(0) == null, "Fresh empty lane");
        var source = gm.HandManager.HandRoot.GetComponentInChildren<CardView>();
        Require(source != null, "Card available in hand");
        gm.player.actionPoints = 20;
        gm.OnEmptySpotClicked(gm.playerBoardRoot.GetChild(0));
        gm.OnCardClicked(source);
        for (int i = 0; i < 25; i++) yield return null;
        card = gm.GetPlayerCardViewAtLane(0);
        slot = gm.GetEnemySlotAtLane(0);
        Require(card != null && slot != null, "Real card played through normal handlers");
        foreach(var ability in card.GetComponents<AbilityBase>()) ability.Unbind();
        foreach(var ability in gm.GetEnemySlotViewAtLane(0).GetComponents<AbilityBase>()) ability.Unbind();
        // Each case starts from explicit temporary runtime data. Invariants are
        // captured only after setup, before invoking the actual click handler.
        foreach (var item in new[] {
            new Case("guard-only", 2, 5, 5, false, false, false),
            new Case("guard-and-health", 5, 2, 5, false, false, false),
            new Case("overflow", 8, 2, 3, false, false, false),
            new Case("resonance", 5, 4, 3, true, false, false),
            new Case("empty-lane", 4, 0, 0, false, false, true),
            new Case("retro", 8, 2, 5, false, true, false),
            new Case("zero", 0, 2, 5, false, false, false)
        })
        {
            Setup(item);
            for (int i = 0; i < 3; i++) yield return null;
            string logical = Logic();
            string boss = hud.bossHpText.text;
            Color color = hud.bossHpText.color;
            float[] baseline = Pixels(null);
            float[] low = (float[])baseline.Clone(), high = (float[])baseline.Clone();
            int attack = item.retro ? 0 : item.attack;
            int guard = item.resonant || item.hole ? 0 : item.guard;
            int absorbed = Mathf.Min(attack, guard);
            int lost = item.hole ? 0 : Mathf.Min(Mathf.Max(0, attack - guard), item.health);
            int overflow = item.hole ? attack : Mathf.Max(0, attack - guard - item.health);
            bool bossReached = overflow == 0;
            gm.OnCardClicked(card);
            int frame = Time.frameCount;
            for (int sample = 0; sample < 14; sample++)
            {
                yield return new WaitForSecondsRealtime(.12f);
                Require(Logic() == logical, item.name + ": preview mutated logical state");
                var pixels = Pixels(sample == 6 && item.name == "overflow" ? "Logs/damage-preview-overflow.png" : null);
                for (int i = 0; i < pixels.Length; i++) { low[i] = Mathf.Min(low[i], pixels[i]); high[i] = Mathf.Max(high[i], pixels[i]); }
                int displayed = BossNumber();
                if (overflow > 0 && displayed == Mathf.Max(0, gm.ai.hp - overflow))
                {
                    var c = hud.bossHpText.color;
                    Require(c.r > .5f && c.g > .4f && c.b < c.g * .7f, item.name + ": boss preview is yellow");
                    bossReached = true;
                }
            }
            Require(Time.frameCount > frame + 10, item.name + ": multiple actual frames sampled");
            Require(bossReached, item.name + ": boss reached predicted value");
            int index = 0;
            foreach (var bank in lamps.definition.banks)
                for (int n = 0; n < 7; n++, index++)
                {
                    bool expected = bank.lane == 0 && !item.hole &&
                        ((bank.stat == 0 && n >= item.health - lost && n < item.health) ||
                         (bank.stat == 2 && n >= guard - absorbed && n < guard));
                    float variation = high[index] - low[index];
                    Require(expected ? variation > .09f : variation < .08f,
                        item.name + ": bank=" + bank.lane + "/" + bank.stat + " lamp=" + n + " variation=" + variation + " expectedBlink=" + expected);
                }
            yield return new WaitForSecondsRealtime(3.1f);
            Require(Logic() == logical, item.name + ": state unchanged after preview");
            Require(hud.bossHpText.text == boss && hud.bossHpText.color == color, item.name + ": boss restored");
            var restored = Pixels(null);
            for (int i = 0; i < baseline.Length; i++) Require(Mathf.Abs(restored[i] - baseline[i]) < .08f, item.name + ": lamps restored");
            report.AppendLine(item.name + " PASS frame=" + Time.frameCount);
            File.WriteAllText("Logs/damage-preview-acceptance.txt", "RUNNING\n" + report);
        }
        Setup(new Case("restart", 8, 2, 3, false, false, false));
        for(int i=0;i<3;i++)yield return null;
        Color realColor=hud.bossHpText.color;
        string unchanged=Logic();
        gm.OnCardClicked(card);
        yield return new WaitForSecondsRealtime(1.7f);
        gm.OnCardClicked(card);
        yield return new WaitForSecondsRealtime(1.5f);
        Require(BossNumber()==21,"Repeated click restarts readable duration past original expiry");
        Require(Logic()==unchanged,"Repeated clicks do not mutate gameplay");
        card.instance.side=Side.Retro;
        for(int i=0;i<3;i++)yield return null;
        Require(BossNumber()==gm.ai.hp && hud.bossHpText.color==realColor,"Flip cancels and restores immediately");
        report.AppendLine("repeat-click and flip-cancellation PASS");
        Setup(new Case("attack-cancel", 8, 2, 3, false, false, false));
        for(int i=0;i<3;i++)yield return null;
        gm.OnCardClicked(card);
        yield return new WaitForSecondsRealtime(.8f);
        gm.btnAttack.onClick.Invoke();
        for(int i=0;i<3;i++)yield return null;
        Require(BossNumber()==gm.ai.hp && hud.bossHpText.color==realColor,"Real attack cancels presentation override");
        report.AppendLine("real attack cancellation PASS");
    }

    sealed class Case
    {
        public string name; public int attack, guard, health; public bool resonant, retro, hole;
        public Case(string n, int a, int g, int h, bool r, bool b, bool empty) { name=n; attack=a; guard=g; health=h; resonant=r; retro=b; hole=empty; }
    }
    void Setup(Case item)
    {
        card.instance.def.frontDamage = item.attack;
        card.instance.flipCharge = 0;
        card.instance.ClearAtkBonus(); card.instance.ClearBlockBonus();
        card.instance.side = item.retro ? Side.Retro : Side.Fronte;
        card.instance.def.faction = Faction.A;
        slot.def.faction = item.resonant ? Faction.A : Faction.B;
        slot.health = item.health; slot.side = Side.Retro;
        slot.def.blockRetro = item.guard; slot.ClearBlockBonus(); slot.ClearAtkBonus();
        slot.incomingDamageOverride = null;
        gm.ai.hp = 24;
        lamps.RefreshIndicators();
    }
    int BossNumber()
    {
        string text = hud.bossHpText.text;
        var digits = new StringBuilder();
        foreach (char ch in text) { if(char.IsDigit(ch)) digits.Append(ch); else if(digits.Length > 0) break; }
        int value; return int.TryParse(digits.ToString(), out value) ? value : -1;
    }
    string Logic()
    {
        var text = new StringBuilder();
        text.Append(gm.ai.hp).Append('/').Append(gm.player.hp).Append('/').Append(gm.player.actionPoints).Append('/').Append(gm.CurrentTurn).Append('/').Append(gm.CanAct);
        for(int lane=0;lane<gm.playerBoardRoot.childCount;lane++) {
            var c=gm.GetPlayerCardAtLane(lane); var s=gm.GetEnemySlotAtLane(lane);
            if(c!=null) text.Append(" C").Append(c.health).Append(':').Append(c.side).Append(':').Append(c.flipCharge).Append(':').Append(c.tempAtkBonus).Append(':').Append(c.tempBlockBonus);
            if(s!=null) text.Append(" S").Append(s.health).Append(':').Append(s.side).Append(':').Append(s.tempAtkBonus).Append(':').Append(s.tempBlockBonus).Append(':').Append(s.origin!=null?s.origin.health:-1);
        }
        return text.ToString();
    }
    float[] Pixels(string path)
    {
        var camera=Camera.main; var previous=camera.targetTexture; var active=RenderTexture.active;
        var target=RenderTexture.GetTemporary(1920,1080,24); var texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        var values=new float[63]; var rt=(RectTransform)lamps.transform;
        try {
            camera.targetTexture=target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,1920,1080),0,0); texture.Apply();
            int index=0;
            foreach(var bank in lamps.definition.banks) for(int n=0;n<7;n++) {
                var p=bank.first+n*bank.step;
                var local=new Vector3(rt.rect.xMin+p.x/lamps.definition.sourceSize.x*rt.rect.width,rt.rect.yMax-p.y/lamps.definition.sourceSize.y*rt.rect.height,0);
                var point=camera.WorldToViewportPoint(rt.TransformPoint(local));
                var pixel=texture.GetPixel(Mathf.Clamp((int)(point.x*1920),0,1919),Mathf.Clamp((int)(point.y*1080),0,1079));
                values[index++]=Mathf.Max(pixel.r,Mathf.Max(pixel.g,pixel.b));
            }
            if(path!=null)File.WriteAllBytes(path,texture.EncodeToPNG());
        } finally {camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.Destroy(texture);}
        return values;
    }
    void Require(bool value,string message) { if(!value)throw new InvalidOperationException(message); assertions++; }
}
