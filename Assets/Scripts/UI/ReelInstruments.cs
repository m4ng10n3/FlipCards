using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Fixed cabinet instruments, one bulb per point. Never travel with a reel face.</summary>
public class ReelInstruments : MonoBehaviour
{
    public int lane;
    public RectTransform laneRoot;
    public float cellTop = 56f, cellHeight = 288f;
    public int HealthValue { get; private set; }
    public int AttackValue { get; private set; }
    public int GuardValue { get; private set; }
    public bool Loading { get; private set; }
    public int LitHealth => Count(_health);
    public int LitAttack => Count(_attack);
    public int LitGuard => Count(_guard);
    readonly List<SlotLamp> _health = new(), _attack = new(), _guard = new();
    RectTransform _hpRoot, _atkRoot, _defRoot;
    SlotBatchManager _batch;
    bool _wasRolling;
    float _settledAt = -10f, _hurtAt = -10f;
    int _lastId = -1, _lastHealth = -1;

    void Awake()
    {
        _hpRoot = Bank("Life", 80f, 13f, 192f, "+", GamePalette.Danger);
        _atkRoot = Bank("Attack", 18f, cellTop + cellHeight + 13f, 146f, "ATK", GamePalette.Fronte);
        _defRoot = Bank("Guard", 188f, cellTop + cellHeight + 13f, 146f, "DEF", GamePalette.Retro);
    }
    RectTransform Bank(string name, float x, float y, float width, string label, Color color)
    {
        var rt=UiBuild.Rect(name,transform); UiBuild.Band(rt,x,y,width,32f);
        var text=UiBuild.Text("Engraving",rt,label, label=="+"?22f:12f,color,TextAlignmentOptions.Center,FontStyles.Bold);
        UiBuild.Band(text.rectTransform,0f,3f,26f,24f);
        return rt;
    }
    static int Count(List<SlotLamp> lamps) { int n=0; foreach(var lamp in lamps) if(lamp.gameObject.activeSelf && lamp.Lit)n++; return n; }
    void LateUpdate()
    {
        var gm=GameManager.Instance;
        if(gm==null)return;
        if(laneRoot!=null && lane<laneRoot.childCount)
        {
            var laneRect=laneRoot.GetChild(lane) as RectTransform;
            var parent=(RectTransform)transform.parent;
            var center=parent.InverseTransformPoint(laneRect.TransformPoint(laneRect.rect.center));
            ((RectTransform)transform).anchoredPosition=new Vector2(center.x-parent.rect.xMin-176f,0f);
        }
        if(_batch==null)_batch=Object.FindAnyObjectByType<SlotBatchManager>();
        bool rolling=_batch!=null && _batch.IsRolling;
        if(_wasRolling && !rolling)_settledAt=Time.unscaledTime;
        _wasRolling=rolling;
        var slot=gm.GetEnemySlotAtLane(lane);
        bool alive=slot!=null && slot.alive;
        HealthValue=alive?slot.health:0;
        AttackValue=alive && slot.side==Side.Fronte?Mathf.Max(0,slot.def.atkDamage+slot.tempAtkBonus):0;
        GuardValue=alive && !SynergyResolver.Resonates(gm,lane)?slot.ComputeSelfBlock():0;
        if(alive && slot.id==_lastId && HealthValue<_lastHealth)_hurtAt=Time.unscaledTime;
        _lastId=alive?slot.id:-1; _lastHealth=HealthValue;
        Loading=rolling;
        UpdateBank(_hpRoot,_health,HealthValue,alive?slot.def.maxHealth:5,SlotLamp.Lens.Round,GamePalette.Danger,rolling,0);
        UpdateBank(_atkRoot,_attack,AttackValue,5,SlotLamp.Lens.Spear,GamePalette.Fronte,rolling,1);
        UpdateBank(_defRoot,_guard,GuardValue,5,SlotLamp.Lens.Shield,GamePalette.Retro,rolling,2);
    }
    void UpdateBank(RectTransform root,List<SlotLamp> lamps,int value,int capacity,SlotLamp.Lens shape,Color tint,bool rolling,int offset)
    {
        int count=Mathf.Max(shape == SlotLamp.Lens.Round ? 10 : 5,Mathf.CeilToInt(Mathf.Max(value,capacity)/5f)*5);
        while(lamps.Count<count)
        {
            var rt=UiBuild.Rect("Bulb"+lamps.Count,root);
            var lamp=rt.gameObject.AddComponent<SlotLamp>(); lamp.raycastTarget=false; lamp.shape=shape;
            lamps.Add(lamp);
        }
        int columns=Mathf.Min(10,count), rows=Mathf.CeilToInt(count/(float)columns);
        float pitch=Mathf.Min(19f,(root.rect.width-30f)/columns);
        float size=Mathf.Min(14f,Mathf.Min(pitch-2f,30f/rows));
        int chase=Mathf.FloorToInt(Time.unscaledTime*13f)+lane*2+offset*3;
        for(int i=0;i<lamps.Count;i++)
        {
            var lamp=lamps[i]; lamp.gameObject.SetActive(i<count); if(i>=count)continue;
            UiBuild.Band(lamp.rectTransform,30f+(i%columns)*pitch,(32f-rows*size)*.5f+(i/columns)*size,size,size);
            bool lit=rolling?((chase-i)%count+count)%count<3 : i<value && Time.unscaledTime-_settledAt>i*.045f;
            Color ink=offset==0 && Time.unscaledTime-_hurtAt<.24f?Color.Lerp(tint,GamePalette.Paper,.55f):tint;
            lamp.SetState(lit,ink);
        }
    }
}
