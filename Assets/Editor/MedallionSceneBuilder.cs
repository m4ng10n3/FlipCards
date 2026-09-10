using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Composition measured from ArtReferences/2026-09-10_original_medallion_full.png.</summary>
public static class MedallionSceneBuilder
{
    const float W = 1920, H = 1080;
    const float MachineX = 300, MachineY = 48, MachineW = 1330, MachineH = 665;

    // Centri delle finestre dei rulli, misurati sull'alpha di cabinet_illustrated:
    // 460-754, 811-1117, 1175-1470 per y 263-501. Le corsie del giocatore stanno
    // sugli stessi centri.
    const float LaneCenter = 607, LanePitch = 357.5f;

    // La faccia del rullo riempie la finestra da 294x238 e passa di pochi pixel
    // sotto gli anelli d'ottone: scala uniforme, cosi il simbolo non si stira.
    const float EnemyScale = .856f, EnemyTop = 259;

    // Il panno e' un piano inclinato visto da una camera prospettica: carte,
    // mazzo e libretto ci stanno sopra, ruotati di TableTilt attorno al proprio
    // centro. Il centro resta dove lo mette il layout — e i centri delle corsie
    // restano quelli dei rulli — mentre i bordi convergono verso un punto di
    // fuga comune. Il FOV stretto tiene la prospettiva debole come sulle carte
    // del riferimento (lato lontano ~0.9 del vicino); il medaglione dipinto sul
    // panno scorcia molto di piu', ma e' pittura.
    const float TableTilt = 60f, CameraFov = 17f;
    const float CardScale = 1.34f, PlayerRowCenterY = 868f;

    // Il mazzo sta tutto dentro lo schermo e un po' piu' piccolo delle carte in
    // campo; lo spessore lo si vede sul fianco rivolto al centro del tavolo.
    const float DeckCenterX = 178, DeckCenterY = 800, DeckSpin = -10f, DeckCardScale = .98f, DeckEdgeShift = .8f;

    // Perno della leva: il fianco sinistro del tamburo tocca la guancia destra
    // della cassa, che all'altezza del perno sta a x 1582.
    const float LeverPivotX = 1612, LeverPivotY = 460, LeverScale = .19f;

    static readonly Color Gold = new Color(.80f,.62f,.30f);
    // Inchiostro rosso del boss: lo stesso delle lastre ferite in SlotOverlay.
    static readonly Color BossInk = new Color(.55f,.14f,.10f);

    static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        => UiBuild.Band(UiBuild.Rect(name, parent), x, y, w, h);
    static Image Art(string name, Transform parent, string key, float x, float y, float w, float h)
    {
        var image = UiBuild.Fill(Rect(name,parent,x,y,w,h), Color.white);
        image.sprite = UiSkin.Sprite(key);
        return image;
    }
    static TMP_Text Text(string name, Transform parent, string value, float x, float y, float w, float h, float size, Color color)
    {
        var text = UiBuild.Text(name,parent,value,size,color);
        text.font = UiBuild.Font; text.fontSize = size;
        UiBuild.Band(text.rectTransform,x,y,w,h);
        return text;
    }
    static void Outline(RectTransform rt)
    {
        UiBuild.Fill(Rect("Top",rt,0,0,rt.rect.width,2),Gold);
        UiBuild.Fill(Rect("Bottom",rt,0,rt.rect.height-2,rt.rect.width,2),Gold);
        UiBuild.Fill(Rect("Left",rt,0,0,2,rt.rect.height),Gold);
        UiBuild.Fill(Rect("Right",rt,rt.rect.width-2,0,2,rt.rect.height),Gold);
    }
    static Button Button(string name, Transform parent, string label, float x, float y, float w, float h)
    {
        var rt = Rect(name,parent,x,y,w,h);
        var image = UiBuild.Fill(rt,new Color(.025f,.065f,.075f,.92f),true);
        Outline(rt);
        var button = rt.gameObject.AddComponent<Button>(); button.targetGraphic=image;
        var text=Text("Label",rt,label,6,4,w-12,h-8,21,GamePalette.Paper);text.alignment=TextAlignmentOptions.Center;
        return button;
    }
    static void Unframe(Button button)
    {
        button.GetComponent<Image>().color=Color.clear;
        foreach(var name in new[]{"Top","Bottom","Left","Right"})
            Object.DestroyImmediate(button.transform.Find(name).gameObject);
    }
    static void Bind(Button button, UnityEngine.Events.UnityAction action)
        => UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, action);

    public static void Build()
    {
        var gm=Object.FindAnyObjectByType<GameManager>();var hand=Object.FindAnyObjectByType<HandManager>();
        if(gm==null || hand==null) throw new System.InvalidOperationException("GameManager/HandManager missing.");
        var go=GameObject.Find("Canvas") ?? new GameObject("Canvas",typeof(Canvas));

        // Prospettiva per il piano del tavolo. Cio' che sta sul piano del canvas
        // si vede identico a prima: la differenza la fanno solo i rect inclinati.
        var camera=Camera.main;
        camera.orthographic=false;camera.fieldOfView=CameraFov;
        EditorUtility.SetDirty(camera);

        var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;
        canvas.planeDistance=100;canvas.sortingOrder=0;canvas.referencePixelsPerUnit=1;
        var scaler=go.GetComponent<CanvasScaler>() ?? go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(W,H);
        scaler.referencePixelsPerUnit=1;scaler.matchWidthOrHeight=.5f;
        if(go.GetComponent<GraphicRaycaster>()==null)go.AddComponent<GraphicRaycaster>();
        UiBuild.ClearChildren(go.transform);
        var root=(RectTransform)go.transform;
        var hud=go.GetComponent<HudController>() ?? go.AddComponent<HudController>();
        var oldOverlay=go.GetComponent<TableOverlayController>();if(oldOverlay!=null)Object.DestroyImmediate(oldOverlay);
        var overlay=go.AddComponent<TableOverlayController>();
        Art("StarryVelvetTable",root,"scene_table",0,0,W,H);
        var under=Rect("MachineUnder",root,MachineX,MachineY,MachineW,MachineH);
        var enemy=Board(root,"EnemyLanes","AIBoardRoot",LaneCenter,EnemyTop,SlotOverlay.CellW,SlotOverlay.CellH,EnemyScale,0f);
        var cabinet=Art("Cabinet",root,"scene_cabinet",MachineX,MachineY,MachineW,MachineH);
        cabinet.material=MedallionSceneSkin.CabinetMaterial();
        var over=Rect("MachineOver",root,MachineX,MachineY,MachineW,MachineH);
        var chrome=under.gameObject.AddComponent<ReelChrome>();chrome.underLayer=over;chrome.overLayer=over;
        chrome.laneReferenceRoot=enemy;chrome.cellTop=EnemyTop-MachineY;chrome.cellWidth=SlotOverlay.CellW*EnemyScale;chrome.cellHeight=SlotOverlay.CellH*EnemyScale;chrome.sliverHeight=0;chrome.highlightBleed=0;

        var player=Board(root,"PlayerLanes","PlayerBoardRoot",LaneCenter,PlayerRowCenterY-CardOverlay.CardH*CardScale*.5f,
                         CardOverlay.CardW,CardOverlay.CardH,CardScale,TableTilt);
        // Il pronostico sta fra i piedi della cassa e il bordo lontano delle
        // carte (y ~745): piu' in basso le sue due righe coprono la cornice.
        var axis=Rect("LaneAxis",root,0,694,W,40).gameObject.AddComponent<LaneAxisView>();axis.laneReferenceRoot=player;axis.columnWidth=285;
        var (handRoot,spawn)=Hand(root,hud);
        Deck(root,hud);
        Status(root,hud);
        var attack=MushroomControl(root,1620,700,245);
        var lever=LeverControl(root,LeverPivotX,LeverPivotY);
        hud.endTurnLabel=null;
        // Due oggetti sul tavolo, non due scritte. Il libretto sta oltre il
        // mazzo — piu' lontano dal giocatore — perche' e' roba da consultare,
        // non da giocare; la pergamena resta in alto a destra dov'era.
        Bind(BookletTag(root,58,520,230,120),overlay.OpenDetail);
        Bind(ScrollTag(root,1646,24,222,88),overlay.OpenLegend);
        var log=Overlay(root,overlay);
        FlipCardsLayoutBuilder.BuildEndPanel(root,hud);
        var endCanvas=hud.endPanel.AddComponent<Canvas>();endCanvas.overrideSorting=true;endCanvas.sortingOrder=120;
        hud.endPanel.AddComponent<GraphicRaycaster>();
        FlipCardsLayoutBuilder.WireGameManager(gm,player,enemy,attack,lever,log);
        FlipCardsLayoutBuilder.WireHandManager(hand,handRoot,spawn);
        EditorUtility.SetDirty(go);EditorUtility.SetDirty(gm);EditorUtility.SetDirty(hand);
    }

    static RectTransform Board(Transform parent,string zoneName,string name,float firstCenter,float y,float cellW,float cellH,float scale,float tilt)
    {
        float pitch=LanePitch/scale, width=cellW+2*pitch;
        var zone=Rect(zoneName,parent,firstCenter-cellW*scale*.5f,y,width*scale,cellH*scale);
        if(tilt!=0f)LayFlat(zone,tilt);
        var board=Rect(name,zone,0,0,width,cellH);board.localScale=new Vector3(scale,scale,1);
        var layout=board.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=pitch-cellW;
        layout.childAlignment=TextAnchor.MiddleCenter;layout.childControlHeight=false;layout.childControlWidth=false;
        layout.childForceExpandHeight=false;layout.childForceExpandWidth=false;
        return board;
    }

    /// <summary>
    /// Posa sul tavolo un rect gia' impaginato: pivot al centro senza spostarlo,
    /// poi rotazione attorno all'asse orizzontale. Il centro resta dov'era sullo
    /// schermo, quindi i centri delle corsie restano quelli dei rulli.
    /// </summary>
    static void LayFlat(RectTransform rt,float degrees)
    {
        var size=rt.sizeDelta;
        rt.pivot=new Vector2(.5f,.5f);
        rt.anchoredPosition+=new Vector2(size.x*.5f,-size.y*.5f);
        rt.localRotation=Quaternion.Euler(degrees,0f,0f);
    }

    /// <summary>
    /// Un oggetto posato sul tavolo e girato sul panno: rect centrato nel parent,
    /// inclinato come il tavolo, e dentro un secondo rect ruotato nel piano. Due
    /// transform perche' la rotazione nel piano va applicata prima
    /// dell'inclinazione: con lo schiacciamento al posto dell'inclinazione, come
    /// prima, il risultato era uno shear e la carta sembrava storta.
    /// </summary>
    static RectTransform OnTable(RectTransform parent,string name,float w,float h,float spin)
    {
        var plane=UiBuild.Rect(name,parent);UiBuild.Centered(plane,w,h);plane.localRotation=Quaternion.Euler(TableTilt,0f,0f);
        var turned=UiBuild.Rect("Turned",plane);UiBuild.Centered(turned,w,h);turned.localRotation=Quaternion.Euler(0f,0f,spin);
        return turned;
    }

    static (Transform,Transform) Hand(RectTransform root,HudController hud)
    {
        var zone=Rect("HandZone",root,355,994,1210,86);zone.pivot=Vector2.zero;zone.anchorMin=zone.anchorMax=Vector2.zero;zone.anchoredPosition=new Vector2(355,0);
        UiBuild.Fill(zone,Color.clear,true);
        var hand=UiBuild.Rect("PlayerHand",zone);hand.anchorMin=hand.anchorMax=new Vector2(.5f,0);hand.pivot=new Vector2(.5f,.5f);hand.sizeDelta=new Vector2(1148,336);hand.anchoredPosition=new Vector2(0,-52);
        var spawn=UiBuild.Rect("spawnPoint",zone);spawn.anchorMin=spawn.anchorMax=new Vector2(.5f,0);spawn.anchoredPosition=new Vector2(-420,-52);
        var tray=zone.gameObject.AddComponent<HandTray>();tray.handRoot=hand;tray.restY=-52;tray.raisedY=235;tray.restHeight=86;
        hud.handText=Text("HandCount",root,"MANO",780,1052,350,23,15,GamePalette.Paper);hud.handText.alignment=TextAlignmentOptions.Center;
        return(hand,spawn);
    }
    static void Deck(RectTransform root,HudController hud)
    {
        // L'area di clic resta piatta, in spazio schermo. La pila sta sul piano
        // del tavolo come le carte, girata sul panno: lo spessore cresce lungo la
        // normale del tavolo e cade dritto verso chi guarda.
        var deck=Rect("Deck",root,DeckCenterX-130,DeckCenterY-115,260,230);
        UiBuild.Fill(deck,Color.clear,true);var view=deck.gameObject.AddComponent<DeckView>();
        view.stackRoot=UiBuild.Rect("Stack",OnTable(deck,"Plane",260,230,DeckSpin));UiBuild.Stretch(view.stackRoot);
        view.cardScale=DeckCardScale;view.maxLayers=1;view.maxEdges=18;view.edgeThickness=2.6f;
        // Verso sinistra sul panno, riportato nello spazio della pila girata.
        view.edgeShift=Quaternion.Euler(0f,0f,-DeckSpin)*new Vector3(-DeckEdgeShift,0f,0f);
        hud.deckText=Text("DeckCount",root,"0",DeckCenterX+126,DeckCenterY+70,60,30,19,GamePalette.Paper);
        view.hintText=null;
    }
    static void Status(RectTransform root,HudController hud)
    {
        // Il riquadro d'avorio del kit: le proporzioni sono le sue (1448x913),
        // altrimenti la doppia cornice stampata si deforma. Su carta chiara il
        // testo va in inchiostro, non in Paper, o non si legge.
        var plaque=Rect("PlayerStatus",root,36,28,336,212);
        var sheet=UiBuild.Fill(plaque,Color.white);
        sheet.sprite=UiSkin.Sprite("scene_paper");
        sheet.raycastTarget=false;

        // La vita del boss sta sopra la nostra, nello stesso riquadro: sono le
        // due vite che decidono la partita, e si confrontano con un'occhiata.
        Text("BossLabel",plaque,"BOSS",30,22,120,32,23,BossInk);
        hud.bossHpText=Value("BossValue",plaque,"24 / 24",172,22);hud.bossHpText.color=BossInk;
        Text("LifeLabel",plaque,"VITA",30,60,120,32,23,GamePalette.Ink);
        hud.playerHpText=Value("LifeValue",plaque,"20 / 20",172,60);
        Text("ApLabel",plaque,"AP",30,98,120,32,23,GamePalette.Ink);
        hud.apText=Value("ApValue",plaque,"3 / 3",172,98);
        UiBuild.Fill(Rect("Rule",plaque,30,140,276,2),new Color(GamePalette.Ink.r,GamePalette.Ink.g,GamePalette.Ink.b,.55f));
        hud.turnText=Text("Turn",plaque,"TURNO 1 / 12",30,152,276,32,22,GamePalette.Ink);
        hud.phaseText=Text("Phase",root,"FASE AZIONI",690,14,600,28,17,GamePalette.Paper);hud.phaseText.alignment=TextAlignmentOptions.Center;
    }
    /// <summary>
    /// Il libretto chiuso posato sul tavolo: copertina d'avorio, dorso scuro a
    /// sinistra, linguette che sporgono a destra. Sullo stesso piano del mazzo.
    /// </summary>
    static Button BookletTag(RectTransform root,float x,float y,float w,float h)
    {
        const float PW=208,PH=170;
        var rt=Rect("DetailButton",root,x,y,w,h);
        var hit=UiBuild.Fill(rt,Color.clear,true);
        var page=OnTable(rt,"Plane",PW,PH,-7f);

        var cover=Art("Cover",page,"scene_paper",0,0,PW,PH);cover.raycastTarget=false;
        UiBuild.Fill(Rect("Spine",page,0,0,16,PH),new Color(.16f,.12f,.07f,.92f)).raycastTarget=false;
        for(int i=0;i<3;i++)
            UiBuild.Fill(Rect("Tab"+i,page,PW-6,26+i*40,20,30),TabInk(i)).raycastTarget=false;
        var label=Text("Label",page,"DETTAGLIO",30,PH*.5f-22,PW-58,44,28,GamePalette.InkStrong);
        label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;

        var button=rt.gameObject.AddComponent<Button>();button.targetGraphic=hit;
        return button;
    }

    /// <summary>La pergamena arrotolata: foglio d'avorio stretto fra due rulli d'ottone.</summary>
    static Button ScrollTag(RectTransform root,float x,float y,float w,float h)
    {
        var rt=Rect("LegendButton",root,x,y,w,h);
        var hit=UiBuild.Fill(rt,Color.clear,true);
        var sheet=Art("Sheet",rt,"scene_paper",14,18,w-28,h-36);sheet.raycastTarget=false;
        Roller(rt,"RollerTop",0,4,w,18);
        Roller(rt,"RollerBottom",0,h-22,w,18);
        var label=Text("Label",rt,"LEGENDA",16,h*.5f-15,w-32,30,20,GamePalette.InkStrong);
        label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
        var button=rt.gameObject.AddComponent<Button>();button.targetGraphic=hit;
        return button;
    }

    /// <summary>
    /// Rullo d'ottone: e' l'asta della leva coricata. Il tondino non riempie
    /// tutta la tela dello sprite, quindi la tela va larga quanto serve perche'
    /// l'ottone venga dello spessore voluto.
    /// </summary>
    static void Roller(RectTransform parent,string name,float x,float y,float w,float thickness)
    {
        var slot=Rect(name,parent,x,y,w,thickness);
        var bar=UiBuild.Rect("Bar",slot);
        bar.anchorMin=bar.anchorMax=bar.pivot=new Vector2(.5f,.5f);
        bar.sizeDelta=new Vector2(thickness/MedallionSceneSkin.LeverShaftFill,w);
        bar.anchoredPosition=Vector2.zero;
        bar.localRotation=Quaternion.Euler(0,0,90f);
        var image=UiBuild.Fill(bar,Color.white);
        image.sprite=UiSkin.Sprite("scene_lever_shaft");
        image.raycastTarget=false;
    }

    static Color TabInk(int index)
        => index switch { 0=>new Color(.62f,.24f,.18f,.95f), 1=>new Color(.20f,.40f,.42f,.95f), _=>new Color(.55f,.44f,.16f,.95f) };

    /// <summary>
    /// Il fungo in due pezzi tagliati dallo stesso disegno: solo il cappello si
    /// muove. Il basamento sta davanti, cosi' la corsa sparisce dietro la ghiera.
    /// </summary>
    static Button MushroomControl(RectTransform root,float x,float y,float size)
    {
        float capH=size*MedallionSceneSkin.MushroomCut/1254f, baseH=size-capH;
        var rt=Rect("BtnAttack",root,x,y,size,size);
        UiBuild.Fill(rt,Color.clear,true);
        var cap=Art("Cap",rt,"scene_attack_cap",0,0,size,capH);cap.raycastTarget=false;
        var plinth=Art("Base",rt,"scene_attack_base",0,capH,size,baseH);plinth.raycastTarget=false;
        var button=rt.gameObject.AddComponent<Button>();button.targetGraphic=plinth;
        var colors=button.colors;colors.disabledColor=new Color(.55f,.55f,.55f,1);button.colors=colors;
        var feedback=rt.gameObject.AddComponent<TableControlFeedback>();feedback.cap=cap.rectTransform;
        return button;
    }

    /// <summary>
    /// Leva in tre pezzi dello stesso disegno a inchiostro della cassa, attorno
    /// all'asse del tamburo. L'area di presa copre tutta la corsa della manopola,
    /// non solo la posizione di riposo: a meta' tirata il puntatore resta dentro
    /// il bottone e il clic non si perde.
    /// </summary>
    static Button LeverControl(RectTransform root,float pivotX,float pivotY)
    {
        const float halfW=110f, above=290f, below=230f;
        var rt=Rect("BtnEndTurn",root,pivotX-halfW,pivotY-above,halfW*2f,above+below);
        UiBuild.Fill(rt,Color.clear,true);
        var button=rt.gameObject.AddComponent<Button>();
        var hinge=new Vector2(halfW,-above);
        float k=LeverScale;

        // Ordine di creazione = ordine di disegno a riposo: la bocca scura della
        // fessura, poi l'asta che ne esce da dietro il tamburo, il tamburo, la
        // manopola davanti. Quando la tirata porta l'asta verso la camera,
        // MedallionLever scambia asta e tamburo; la bocca resta sempre in fondo.
        LeverPart(rt,"Socket","scene_lever_socket",hinge,MedallionSceneSkin.LeverHubSize*k,MedallionSceneSkin.LeverHubPivot);
        var shaft=LeverPart(rt,"Shaft","scene_lever_shaft",hinge,
                            new Vector2(MedallionSceneSkin.LeverShaftSize.x*k,MedallionSceneSkin.LeverAttach*k),new Vector2(.5f,0f));
        var hub=LeverPart(rt,"Hub","scene_lever_hub",hinge,MedallionSceneSkin.LeverHubSize*k,MedallionSceneSkin.LeverHubPivot);
        var knob=LeverPart(rt,"Knob","scene_lever_knob",hinge,MedallionSceneSkin.LeverKnobSize*k,MedallionSceneSkin.LeverKnobPivot);
        button.targetGraphic=knob.GetComponent<Image>();
        var colors=button.colors;colors.disabledColor=new Color(.55f,.55f,.55f,1);button.colors=colors;

        var rig=rt.gameObject.AddComponent<MedallionLever>();
        rig.hub=hub;rig.shaft=shaft;rig.knob=knob;rig.hinge=hinge;
        // A riposo la proiezione accorcia l'asta di cos(pitch): la lunghezza
        // fisica e' quella che, proiettata, ridà il disegno.
        rig.length=MedallionSceneSkin.LeverAttach*k/Mathf.Cos(rig.cameraPitch*Mathf.Deg2Rad);
        rig.shaftWidth=MedallionSceneSkin.LeverShaftSize.x*k;
        rig.knobSize=MedallionSceneSkin.LeverKnobSize*k;
        rig.screenLean=MedallionSceneSkin.LeverLean;
        rig.ApplyRest();
        Bind(button,rig.Pull);
        return button;
    }

    static RectTransform LeverPart(RectTransform parent,string name,string key,Vector2 anchored,Vector2 size,Vector2 pivot)
    {
        var rt=UiBuild.Rect(name,parent);
        rt.anchorMin=rt.anchorMax=new Vector2(0f,1f);
        rt.pivot=pivot;
        rt.sizeDelta=size;
        rt.anchoredPosition=anchored;
        var image=UiBuild.Fill(rt,Color.white);
        image.sprite=UiSkin.Sprite(key);
        image.raycastTarget=false;
        // L'asta viene allungata dal componente: deformarla e' voluto, il resto no.
        image.preserveAspect=name!="Shaft";
        return rt;
    }

    /// <summary>Numero della targhetta: allineato a destra, come sul riferimento.</summary>
    static TMP_Text Value(string name,RectTransform plaque,string value,float x,float y)
    {
        var text=Text(name,plaque,value,x,y,134,32,24,GamePalette.Ink);
        text.alignment=TextAlignmentOptions.Right;
        return text;
    }

    /// <summary>
    /// Le due superfici di lettura, ognuna con la sua forma fisica: il libretto
    /// per il dettaglio, la pergamena per la legenda. Non sono piu' due stati
    /// dello stesso rettangolo trasparente.
    /// </summary>
    static TMP_Text Overlay(RectTransform root,TableOverlayController controller)
    {
        var modal=Rect("ReadingOverlay",root,0,0,W,H);controller.modal=modal.gameObject;
        var canvas=modal.gameObject.AddComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=100;
        modal.gameObject.AddComponent<GraphicRaycaster>();
        // Lo scrim serve ancora a intercettare i clic e a staccare la lettura dal
        // tavolo, ma le pagine sopra sono opache: il velo non e' piu' la superficie.
        var scrim=UiBuild.Fill(Rect("Scrim",modal,0,0,W,H),new Color(0,.02f,.025f,.58f),true);
        var dismiss=scrim.gameObject.AddComponent<Button>();dismiss.targetGraphic=scrim;Bind(dismiss,controller.Close);

        var log=Booklet(modal,controller);
        Parchment(modal,controller);
        modal.gameObject.SetActive(false);
        return log;
    }

    /// <summary>Libretto aperto: due pagine, dorso cucito, linguette che sporgono.</summary>
    static TMP_Text Booklet(RectTransform modal,TableOverlayController controller)
    {
        const float PW=1560,PH=900,Page=762,Gap=36;
        var panel=Rect("Booklet",modal,180,80,PW,PH);controller.detail=panel.gameObject;
        Art("PageLeft",panel,"scene_paper",0,0,Page,PH).raycastTarget=false;
        Art("PageRight",panel,"scene_paper",Page+Gap,0,Page,PH).raycastTarget=false;
        // Il dorso in pelle. Opaco: fra le due pagine c'e' un vuoto di 36px e
        // con un velo semitrasparente ci si vedeva attraverso il tavolo.
        UiBuild.Fill(Rect("Spine",panel,Page-6,0,Gap+12,PH),new Color(.17f,.12f,.07f,1f)).raycastTarget=false;
        for(int i=0;i<9;i++)
            UiBuild.Fill(Rect("Stitch"+i,panel,Page+Gap*.5f-2,60+i*92,5,44),new Color(.16f,.12f,.07f,.85f)).raycastTarget=false;

        controller.heading=Text("Title",panel,"DETTAGLIO",46,26,620,48,32,GamePalette.InkStrong);
        Bind(PaperButton("Close",panel,"CHIUDI  x",1300,26,214,48),controller.Close);

        // Linguette sul taglio esterno, come gli indici di un manuale.
        Bind(PaperTab(panel,"FieldTab","CAMPO",0),controller.ShowField);
        Bind(PaperTab(panel,"HandTab","MANO",1),controller.ShowHand);
        Bind(PaperTab(panel,"ReelTab","RULLO",2),controller.ShowReels);
        Bind(PaperTab(panel,"LogTab","REGISTRO",3),controller.ShowLog);

        controller.choices=Rect("Choices",panel,46,108,672,742);
        controller.inspector=Inspector(Rect("Inspector",panel,Page+Gap+46,108,672,742));

        var logBox=Rect("Log",panel,46,108,PW-92,742);controller.logPanel=logBox.gameObject;
        var log=ScrollText(logBox,"CombatLog",0,0,PW-92,742,19);
        log.GetComponentInParent<ScrollRect>().gameObject.AddComponent<LogPanel>();
        logBox.gameObject.SetActive(false);
        return log;
    }

    /// <summary>
    /// La pergamena: i rulli stanno DENTRO il contenuto scorrevole. Da qui la
    /// regola chiesta — se il testo supera la finestra si vede un rullo per
    /// volta, se ci sta si vedono tutti e due.
    /// </summary>
    static void Parchment(RectTransform modal,TableOverlayController controller)
    {
        const float PW=1180,PH=880;
        var panel=Rect("Parchment",modal,(W-PW)*.5f,90,PW,PH);controller.legend=panel.gameObject;

        // Il viewport segue il pannello: e' la sua maschera che scopre il foglio
        // mentre il rotolo si apre. Se restasse di misura fissa, l'animazione
        // sposterebbe una finestra gia' piena invece di srotolare qualcosa.
        var view=UiBuild.Rect("Viewport",panel);UiBuild.Stretch(view);
        UiBuild.Fill(view,Color.clear,true);
        view.gameObject.AddComponent<RectMask2D>();
        var scroll=panel.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal=false;scroll.scrollSensitivity=42;scroll.movementType=ScrollRect.MovementType.Clamped;

        var content=UiBuild.Rect("Content",view);
        content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);
        content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;
        var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth=true;layout.childControlHeight=true;
        layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport=view;scroll.content=content;

        Bar(content,"RollerTop",44,PW);
        var sheet=UiBuild.Rect("Sheet",content);
        var paper=UiBuild.Fill(sheet,Color.white);paper.sprite=UiSkin.Sprite("scene_paper");paper.raycastTarget=false;
        var sheetLayout=sheet.gameObject.AddComponent<VerticalLayoutGroup>();
        sheetLayout.padding=new RectOffset(70,70,44,54);sheetLayout.spacing=26;
        sheetLayout.childControlWidth=true;sheetLayout.childControlHeight=true;
        sheetLayout.childForceExpandWidth=true;sheetLayout.childForceExpandHeight=false;
        sheet.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        Legend(sheet);
        Bar(content,"RollerBottom",44,PW);

        Bind(PaperButton("Close",panel,"CHIUDI  x",PW-232,18,206,46),controller.Close);

        var unroll=panel.gameObject.AddComponent<MedallionScroll>();
        unroll.openSize=new Vector2(PW,PH);
        unroll.openPosition=panel.anchoredPosition;
        unroll.closedPosition=new Vector2(1646f,-24f);
        unroll.closedSize=new Vector2(222f,88f);
        panel.gameObject.SetActive(false);
    }

    /// <summary>Un rullo come voce del layout verticale: il tondino ruotato gli sta dentro.</summary>
    static void Bar(RectTransform content,string name,float thickness,float span)
    {
        var slot=UiBuild.Rect(name,content);
        var element=slot.gameObject.AddComponent<LayoutElement>();
        element.minHeight=element.preferredHeight=thickness;
        var bar=UiBuild.Rect("Bar",slot);
        bar.anchorMin=bar.anchorMax=bar.pivot=new Vector2(.5f,.5f);
        bar.sizeDelta=new Vector2(thickness/MedallionSceneSkin.LeverShaftFill,span);
        bar.anchoredPosition=Vector2.zero;bar.localRotation=Quaternion.Euler(0,0,90f);
        var image=UiBuild.Fill(bar,Color.white);
        image.sprite=UiSkin.Sprite("scene_lever_shaft");image.raycastTarget=false;
    }

    static Button PaperButton(string name,Transform parent,string label,float x,float y,float w,float h)
    {
        var rt=Rect(name,parent,x,y,w,h);
        var background=UiBuild.Fill(rt,new Color(.86f,.81f,.68f,1f),true);
        UiBuild.Fill(Rect("Rule",rt,0,h-2,w,2),new Color(.16f,.12f,.07f,.5f)).raycastTarget=false;
        var button=rt.gameObject.AddComponent<Button>();button.targetGraphic=background;
        var text=Text("Label",rt,label,6,4,w-12,h-8,21,GamePalette.InkStrong);
        text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
        return button;
    }

    /// <summary>Linguetta d'indice sul taglio esterno del libretto.</summary>
    static Button PaperTab(RectTransform panel,string name,string label,int index)
    {
        var rt=Rect(name,panel,1560,132+index*128,152,112);
        var background=UiBuild.Fill(rt,Color.white,true);
        background.sprite=UiSkin.Sprite("scene_paper");
        UiBuild.Fill(Rect("Ink",rt,0,0,12,112),TabInk(index%3)).raycastTarget=false;
        var button=rt.gameObject.AddComponent<Button>();button.targetGraphic=background;
        var text=Text("Label",rt,label,18,40,124,32,19,GamePalette.InkStrong);
        text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
        return button;
    }

    static InspectorPanel Inspector(RectTransform box)
    {
        float w=box.rect.width,h=box.rect.height;
        var inspector=box.gameObject.AddComponent<InspectorPanel>();
        inspector.titleText=Text("Title",box,"SCEGLI UN ELEMENTO",8,0,w-16,42,28,GamePalette.InkStrong);
        inspector.subtitleText=Text("Subtitle",box,"",8,45,w-16,30,18,GamePalette.InkMuted);
        inspector.sideText=Text("Side",box,"",8,79,w-16,25,17,GamePalette.InkMuted);
        inspector.bodyText=ScrollText(box,"Body",8,117,w-16,h-192,19);
        inspector.hintText=Text("Hint",box,"",8,h-68,w-16,42,16,GamePalette.InkMuted);
        inspector.hintText.textWrappingMode=TextWrappingModes.Normal;
        return inspector;
    }

    static TMP_Text ScrollText(RectTransform parent,string name,float x,float y,float w,float h,float size)
    {
        var rt=Rect(name+"Scroll",parent,x,y,w,h);UiBuild.Fill(rt,Color.clear,true);
        var scroll=rt.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.scrollSensitivity=35;
        var viewport=UiBuild.Rect("Viewport",rt);UiBuild.Stretch(viewport);viewport.gameObject.AddComponent<RectMask2D>();
        var text=Text(name,viewport,"",0,0,w,h,size,GamePalette.InkBody);text.alignment=TextAlignmentOptions.TopLeft;text.textWrappingMode=TextWrappingModes.Normal;
        text.overflowMode=TextOverflowModes.Overflow;text.lineSpacing=6;
        var content=text.rectTransform;content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
        text.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport=viewport;scroll.content=content;scroll.movementType=ScrollRect.MovementType.Clamped;
        return text;
    }

    /// <summary>Il testo della legenda, in colonna dentro il foglio della pergamena.</summary>
    static void Legend(RectTransform sheet)
    {
        string[] titles={"LE DUE FACCE","I SEGNI SULLE CARTE","LA CASSA","INSEGNE E RISONANZA","LE AZIONI","IL FINE TURNO"};
        string[] bodies={
            "FRONTE - il ritratto attacca.\nRETRO - il sigillo para, offre l'insegna e accumula cariche.",
            "GOCCE = vita, dall'alto.\nLANCE = attacco, dal basso.\nSCUDI = difesa, dall'alto.\nOgni segno pieno vale 1. I cerchi sono cariche; oltre 7 compare il totale.",
            "Luci rosse tonde = vita della lastra.\nLampade ambra alte = attacco: le sedi sono tre, quante ne servono davvero.\nTubi azzurri bassi = guardia, cinque sedi.\nIl colpo che sfonda la lastra arriva al boss; quello che sfonda la carta arriva a te.",
            "L'insegna del retro da' il suo numero alle carte adiacenti della stessa famiglia.\nStessa famiglia fra carta e casella: risonanza, nessuno dei due para.\nSOLE - Braci    LUNA - Abissi    SATURNO - Rovi",
            "Clic sul mazzo: pesca.\nCasella libera + carta, oppure trascina: gioca.\nDoppio clic: gira una carta.\nTrascina fra corsie: scambia.\nFungo rosso: attacca. Leva: difendi e gira / chiudi turno.",
            "La leva avvia risposta nemica e rullo. Carte e posizioni possono cambiare.\nLe ferite delle lastre restano; una lastra distrutta esce dal pool.\nLeggi il pronostico fra cassa e carte prima di agire."};
        for(int i=0;i<titles.Length;i++)
        {
            var heading=UiBuild.Text("Heading"+i,sheet,titles[i],25,GamePalette.InkGold);
            heading.font=UiBuild.Font;heading.fontSize=25;heading.raycastTarget=false;
            heading.gameObject.AddComponent<LayoutElement>().minHeight=36;

            var body=UiBuild.Text("Explanation"+i,sheet,bodies[i],20,GamePalette.InkBody);
            body.font=UiBuild.Font;body.fontSize=20;body.raycastTarget=false;
            body.textWrappingMode=TextWrappingModes.Normal;body.alignment=TextAlignmentOptions.TopLeft;
            body.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        }
    }
}
