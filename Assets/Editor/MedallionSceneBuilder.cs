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
    const float CardScale = 1.38f, PlayerRowCenterY = 844f;

    // Il mazzo sta tutto dentro lo schermo e un po' piu' piccolo delle carte in
    // campo; lo spessore lo si vede sul fianco rivolto al centro del tavolo.
    // Misure a schermo delle carte a terra nelle corsie esterne (canvas 1920x1080,
    // bordo vicino al giocatore): sinistra 423, destra 1507. Mazzo e fungo ne
    // stanno alla stessa distanza, uno per lato. Il mazzo, girato di 8 gradi,
    // sporge di 157 px a destra del suo centro.
    const float LaneOuterLeft = 423f, LaneOuterRight = 1507f, TableObjectGap = 40f, DeckHalfWidth = 157f;
    const float ButtonSize = 285f;
    const float DeckCenterX = LaneOuterLeft - TableObjectGap - DeckHalfWidth, DeckCenterY = 842, DeckSpin = 8f, DeckCardScale = CardScale, DeckEdgeShift = .35f, DeckFullHeight = 52f;

    // Costellazione degli AP nel cielo, dove prima stava la targhetta.
    const float ApX = 30, ApY = 34, ApSize = 284;

    // Il libretto chiuso sta oltre il mazzo, piu' lontano dal giocatore: e' roba
    // da consultare. Grande abbastanza da leggerne l'etichetta, girato al
    // contrario del mazzo perche' i due oggetti non sembrino allineati a righello.
    // Il libretto sta dietro il mazzo e tutto a sinistra della cassa: e' piu'
    // lontano del fronte della cassa, quindi non deve sovrapporsi alla sua sagoma.
    const float BookletCenterX = 170, BookletCenterY = 580, BookletSpin = -7f;

    // La legenda arrotolata sta specchiata al libretto, dietro il fungo e fuori
    // dalla corsa della leva.
    const float ScrollCenterX = W - BookletCenterX - 20f, ScrollCenterY = 590f, ScrollWidth = 290f, ScrollSpin = 5f;

    // Perno della leva: il fianco sinistro del tamburo tocca la guancia destra
    // della cassa, che all'altezza del perno sta a x 1582.
    const float LeverPivotX = 1612, LeverPivotY = 460, LeverScale = .19f;

    static readonly Color Gold = new Color(.80f,.62f,.30f);

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
        Art("StarryVelvetTable",root,"scene_table",0,0,W,H).material=MedallionSceneSkin.AnimatedSurface(false);
        var under=Rect("MachineUnder",root,MachineX,MachineY,MachineW,MachineH);
        var integratedArt=Resources.Load<CabinetArtDefinition>("ActiveCabinetArt");
        if(integratedArt!=null) {
            // Dark mechanical wells remain behind the reel faces during entry
            // and around their rounded edges; the aperture never exposes the table.
            for(int i=0;i<integratedArt.windowRegions.Length;i++) {
                var r=integratedArt.windowRegions[i];
                UiBuild.Fill(Rect("ReelWell"+i,under,r.x/integratedArt.sourceSize.x*MachineW,
                    r.y/integratedArt.sourceSize.y*MachineH,r.width/integratedArt.sourceSize.x*MachineW,
                    r.height/integratedArt.sourceSize.y*MachineH),new Color(.018f,.024f,.023f));
            }
        }
        var enemy=Board(root,"EnemyLanes","AIBoardRoot",LaneCenter,EnemyTop,SlotOverlay.CellW,SlotOverlay.CellH,EnemyScale,0f);
        var cabinet=Art("Cabinet",root,"scene_cabinet",MachineX,MachineY,MachineW,MachineH);
        cabinet.material=MedallionSceneSkin.CabinetMaterial();
        if(integratedArt!=null)cabinet.gameObject.AddComponent<CabinetLampController>().definition=integratedArt;
        BuildBossHealth(cabinet.rectTransform);
        var over=Rect("MachineOver",root,MachineX,MachineY,MachineW,MachineH);
        var chrome=under.gameObject.AddComponent<ReelChrome>();chrome.underLayer=over;chrome.overLayer=over;
        chrome.laneReferenceRoot=enemy;chrome.cellTop=EnemyTop-MachineY;chrome.cellWidth=SlotOverlay.CellW*EnemyScale;chrome.cellHeight=SlotOverlay.CellH*EnemyScale;chrome.sliverHeight=0;chrome.highlightBleed=0;

        var player=Board(root,"PlayerLanes","PlayerBoardRoot",LaneCenter,PlayerRowCenterY-CardOverlay.CardH*CardScale*.5f,
                         CardOverlay.CardW,CardOverlay.CardH,CardScale,TableTilt);
        // Only resonance and banners remain in this compact gap. Damage is previewed on the cabinet.
        // Niente asse delle corsie: risonanza e insegne stanno accanto al nome di
        // chi le porta (NameMarks), invece di galleggiare sul panno fra cassa e carte.
        var (handRoot,spawn)=Hand(root,hud);
        Deck(root,hud);
        Status(root,hud);
        // Il fungo e' il gemello del mazzo dall'altro lato delle corsie: lascia dal
        // bordo della carta a terra la stessa distanza, e il cerchio su cui poggia
        // il basamento (x 42..1212, centro y 880 sulla tela da 1254) sta alla stessa
        // profondita' del centro del mazzo.
        float buttonK=ButtonSize/1254f;
        var attack=MushroomControl(root,LaneOuterRight+TableObjectGap-42f*buttonK,DeckCenterY-880f*buttonK,ButtonSize);
        var lever=LeverControl(root,LeverPivotX,LeverPivotY);
        hud.endTurnLabel=null;
        // Due oggetti sul tavolo, non due scritte. Il libretto sta oltre il
        // mazzo — piu' lontano dal giocatore — perche' e' roba da consultare,
        // non da giocare; la pergamena resta in alto a destra dov'era.
        Bind(BookletTag(root,BookletCenterX,BookletCenterY,BookletSpin),overlay.OpenDetail);
        Bind(LegendScroll(root),overlay.OpenLegend);
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
        // Nessuna scritta: quante carte hai lo dice la mano stessa.
        hud.handText=null;
        return(hand,spawn);
    }
    static void Deck(RectTransform root,HudController hud)
    {
        // L'area di clic resta piatta, in spazio schermo. La pila sta sul piano
        // del tavolo come le carte, girata sul panno: lo spessore cresce lungo la
        // normale del tavolo e cade dritto verso chi guarda.
        // La carta in cima e' grande come una carta a terra (stessa scala delle
        // corsie): i due piani hanno il centro alla stessa profondita', quindi la
        // prospettiva la scorcia allo stesso modo. Il rettangolo di clic copre
        // l'impronta a schermo della pila inclinata e girata.
        float cw=CardOverlay.CardW*DeckCardScale,ch=CardOverlay.CardH*DeckCardScale;
        float hitW=cw*1.12f,hitH=ch*.62f+DeckFullHeight;
        var deck=Rect("Deck",root,DeckCenterX-hitW*.5f,DeckCenterY-hitH*.5f,hitW,hitH);
        UiBuild.Fill(deck,Color.clear,true);var view=deck.gameObject.AddComponent<DeckView>();
        var plane=OnTable(deck,"Plane",cw*1.4f,ch*1.4f,DeckSpin);
        float px=cw*.7f,py=ch*.7f;
        // L'ombra di contatto ancora la pila al panno: senza, il mazzo sembrava
        // un adesivo rosso appoggiato sopra il disegno.
        ContactShadow(plane,px-cw*.5f,py-ch*.5f,cw,ch,.72f);
        view.stackRoot=UiBuild.Rect("Stack",plane);UiBuild.Stretch(view.stackRoot);
        view.cardScale=DeckCardScale;view.maxLayers=1;view.maxEdges=40;
        // Niente numero: quante carte restano lo dice l'altezza della pila, un
        // taglio per carta. A mazzo pieno la pila e' alta DeckFullHeight.
        view.fullDeckHeight=DeckFullHeight;
        // Verso sinistra sul panno, riportato nello spazio della pila girata.
        view.edgeShift=Quaternion.Euler(0f,0f,-DeckSpin)*new Vector3(-DeckEdgeShift,0f,0f);
        hud.deckText=null;
        view.hintText=null;
    }
    static void Status(RectTransform root,HudController hud)
    {
        // Niente targhetta ne' scritte: le vite stanno sui display LED, gli AP
        // nella costellazione, il turno non si conta piu' (la partita finisce a
        // zero vita) e la fase la dicono i comandi che si accendono e si spengono.
        hud.bossHpText=null;hud.playerHpText=null;hud.apText=null;hud.turnText=null;
        ApConstellation(root,ApX,ApY,ApSize);
        hud.phaseText=null;
    }

    /// <summary>
    /// Gli AP come costellazione nel cielo in alto a sinistra: il simbolo del
    /// dorso (rombo e tre occhi) con una stella per vertice. Tela di
    /// 12_TableProps/Tools/build_constellation.py: 512 px, vertici a 42 px dal
    /// bordo; ogni stella e' uno sprite da 160.
    /// </summary>
    static void ApConstellation(RectTransform root,float x,float y,float size)
    {
        float k=size/512f;
        var rt=Rect("ActionPoints",root,x,y,size,size);
        var view=rt.gameObject.AddComponent<ActionPointConstellation>();
        view.figure=Art("Figure",rt,"ap_constellation",0,0,size,size);view.figure.raycastTarget=false;
        var vertices=new[]{new Vector2(256,42),new Vector2(470,256),new Vector2(256,470),new Vector2(42,256)};
        string[] names={"Top","Right","Bottom","Left"};
        for(int i=0;i<4;i++)
        {
            float s=160*k;
            var star=Rect("Star"+names[i],rt,vertices[i].x*k-s*.5f,vertices[i].y*k-s*.5f,s,s);
            star.pivot=new Vector2(.5f,.5f);star.anchoredPosition+=new Vector2(s*.5f,-s*.5f);
            view.stars[i]=star;
            view.unlit[i]=Art("Unlit",star,"ap_star_off",0,0,s,s);view.unlit[i].raycastTarget=false;
            view.lit[i]=Art("Lit",star,"ap_star_on",0,0,s,s);view.lit[i].raycastTarget=false;
        }
    }
    /// <summary>
    /// Il libretto chiuso posato sul tavolo: copertina d'avorio, dorso scuro a
    /// sinistra, linguette che sporgono a destra. Sullo stesso piano del mazzo.
    /// </summary>
    static Button BookletTag(RectTransform root,float cx,float cy,float spin)
    {
        // Proporzioni di book_closed.png (1388x1133). Il corpo del libro dentro la
        // tela: x 20..1350, y 86..1070; l'etichetta d'avorio x 208..1187, y 433..708.
        const float PW=250,PH=PW*1133f/1388f,Thickness=11f;
        var rt=Rect("DetailButton",root,cx-PW*.62f,cy-PH*.42f,PW*1.24f,PH*.84f);
        var hit=UiBuild.Fill(rt,Color.clear,true);
        var page=OnTable(rt,"Plane",PW,PH,spin);

        // Ombra di contatto sul panno, poi lo spessore: piatto di sotto e blocco
        // delle pagine impilati lungo la normale del tavolo, come i tagli del mazzo.
        float bx=PW*20f/1388f,by=PH*86f/1133f,bw=PW*1330f/1388f,bh=PH*984f/1133f;
        ContactShadow(page,bx,by,bw,bh,.62f);
        var block=UiBuild.Rect("Thickness",page);UiBuild.Stretch(block);
        int slices=7;
        for(int i=0;i<slices;i++)
        {
            bool board=i==0;
            var slice=UiBuild.Fill(Rect(board?"BackBoard":"Pages"+i,block,bx+(board?0:3),by+(board?0:2),bw-(board?0:10),bh-(board?0:6)),
                                   board?new Color(.07f,.17f,.17f):new Color(.86f,.81f,.68f));
            slice.raycastTarget=false;
            slice.rectTransform.localPosition+=new Vector3(0,0,-Thickness*i/slices);
            if(!board)UiBuild.Fill(Rect("Edge",slice.rectTransform,0,bh-7.5f,bw-10,1.2f),new Color(.45f,.36f,.24f,.7f)).raycastTarget=false;
        }
        var cover=Art("Cover",page,"upgrade_book_closed",0,0,PW,PH);cover.raycastTarget=false;
        cover.rectTransform.localPosition+=new Vector3(0,0,-Thickness);
        var label=Text("Label",cover.rectTransform,"DETTAGLIO",PW*208f/1388f,PH*433f/1133f,PW*979f/1388f,PH*275f/1133f,21,GamePalette.InkStrong);
        label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;label.characterSpacing=8;

        var button=rt.gameObject.AddComponent<Button>();button.targetGraphic=hit;
        return button;
    }

    /// <summary>
    /// Ombra morbida sotto un oggetto posato sul panno. La luce viene da in alto
    /// a sinistra come sui disegni, quindi l'ombra scivola in basso a destra.
    /// </summary>
    static void ContactShadow(RectTransform plane,float x,float y,float w,float h,float strength)
    {
        float grow=Mathf.Max(w,h)*.22f;
        var shadow=Art("ContactShadow",plane,"table_shadow",x-grow+w*.03f,y-grow+h*.05f,w+grow*2,h+grow*2);
        shadow.color=new Color(0f,0f,0f,strength);shadow.raycastTarget=false;
    }

    /// <summary>
    /// La legenda arrotolata e posata sul panno, gemella del libretto dall'altra
    /// parte delle corsie. Un cilindro coricato si vede uguale da qualunque
    /// altezza, quindi il rotolo sta sullo schermo come il fungo e la leva; sul
    /// piano inclinato del tavolo va solo la sua ombra, lungo la linea d'appoggio
    /// (a 69% dell'altezza della tela, 12_TableProps/Tools/build_scroll.py).
    /// </summary>
    static Button LegendScroll(RectTransform root)
    {
        float w=ScrollWidth,h=w*300f/1000f;
        var rt=Rect("LegendButton",root,ScrollCenterX-w*.5f,ScrollCenterY-h*.5f,w,h);
        var hit=UiBuild.Fill(rt,Color.clear,true);
        var plane=OnTable(rt,"Plane",w,h*2f,-ScrollSpin);
        ContactShadow(plane,w*.16f,h*1.18f,w*.68f,h*.34f,.6f);
        var art=UiBuild.Rect("Scroll",rt);UiBuild.Centered(art,w,h);
        art.localRotation=Quaternion.Euler(0f,0f,-ScrollSpin);
        var image=UiBuild.Fill(art,Color.white);image.sprite=UiSkin.Sprite("scroll_closed");image.raycastTarget=false;
        var button=rt.gameObject.AddComponent<Button>();button.targetGraphic=hit;
        return button;
    }


    /// <summary>
    /// Vita del boss: display a matrice nella striscia frontale della guancia
    /// sinistra. Tre strati sugli stessi pixel della tela: la luce dei LED, la
    /// guancia ridipinta con la griglia forata, il bagliore additivo. Niente
    /// scritte: il valore e' quanti LED restano accesi.
    /// </summary>
    static void BuildBossHealth(RectTransform cabinet)
    {
        float sx=MachineW/1774f,sy=MachineH/887f;
        var (ox,oy,ow,oh)=MedallionSceneSkin.BossLedRect;
        var matrix=LedLayer("BossLed",cabinet,ox*sx,oy*sy,ow*sx,oh*sy,false);
        MedallionSceneSkin.ConfigureBossLed(matrix,sx,sy);
        Art("BossLedCheek",cabinet,"led_boss_cheek",ox*sx,oy*sy,ow*sx,oh*sy).raycastTarget=false;
        var glow=LedLayer("BossLedGlow",cabinet,ox*sx,oy*sy,ow*sx,oh*sy,true);
        MedallionSceneSkin.ConfigureBossLed(glow,sx,sy);glow.source=matrix;
        var strip=matrix.gameObject.AddComponent<LedHealthStrip>();
        strip.boss=true;strip.fill=LedHealthStrip.Fill.BottomUp;
    }

    static LedMatrix LedLayer(string name,RectTransform parent,float x,float y,float w,float h,bool glow)
    {
        var matrix=Rect(name,parent,x,y,w,h).gameObject.AddComponent<LedMatrix>();
        matrix.material=MedallionSceneSkin.LedMaterial(glow);
        matrix.raycastTarget=false;
        return matrix;
    }

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
        // Vita del giocatore: un arco di LED sulla gonna del basamento, sotto il
        // basamento forato e con il bagliore sopra. Si svuota dai capi al centro.
        float k=size/1254f;
        var led=LedLayer("PlayerLed",rt,0,capH,size,baseH,false);
        MedallionSceneSkin.ConfigurePlayerLed(led,k);
        var plinth=Art("Base",rt,"led_button_base",0,capH,size,baseH);plinth.raycastTarget=false;
        var glow=LedLayer("PlayerLedGlow",rt,0,capH,size,baseH,true);
        MedallionSceneSkin.ConfigurePlayerLed(glow,k);glow.source=led;
        var strip=led.gameObject.AddComponent<LedHealthStrip>();
        strip.fill=LedHealthStrip.Fill.FromCenter;
        strip.lit=new Color(.20f,.95f,.84f);strip.ember=new Color(.012f,.05f,.048f);
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

    /// <summary>
    /// Le due superfici di lettura, ognuna con la sua forma fisica: il libretto
    /// per il dettaglio, la pergamena per la legenda. Non sono piu' due stati
    /// dello stesso rettangolo trasparente.
    /// </summary>
    static TMP_Text Overlay(RectTransform root,TableOverlayController controller)
    {
        var modal=Rect("ReadingOverlay",root,0,0,W,H);controller.modal=modal.gameObject;
        var canvas=modal.gameObject.AddComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=100;
        // I segnalibri sul lato sinistro sono girati di 180 gradi insieme al
        // foglio: con il filtro di default il raycaster li salterebbe.
        modal.gameObject.AddComponent<GraphicRaycaster>().ignoreReversedGraphics=false;
        // Lo scrim serve ancora a intercettare i clic e a staccare la lettura dal
        // tavolo, ma le pagine sopra sono opache: il velo non e' piu' la superficie.
        var scrim=UiBuild.Fill(Rect("Scrim",modal,0,0,W,H),new Color(0,.02f,.025f,.58f),true);
        var dismiss=scrim.gameObject.AddComponent<Button>();dismiss.targetGraphic=scrim;Bind(dismiss,controller.Close);

        var log=Booklet(modal,controller);
        Parchment(modal,controller);
        modal.gameObject.SetActive(false);
        return log;
    }

    // Tela dei disegni del libretto (12_TableProps/Tools/build_book.py): piega a
    // 826 su 1651, facce delle pagine da x 92 a 1560 e da y 24 a 906 su 953.
    const float BookSrcW=1651,BookSrcH=953,BookSrcGutter=826,BookSrcPageRight=1560,BookSrcFaceTop=24,BookSrcFaceBottom=906;
    // Largo quanto basta a leggere due pagine senza coprire tutto il tavolo, con
    // spazio ai lati per i segnalibri: prima era 1560 e usciva dallo schermo.
    const float BookW=1240;
    static readonly string[] BookSections={"CAMPO","MANO","RULLO","REGISTRO"};

    /// <summary>
    /// Libretto aperto: doppia pagina con la piega al centro (niente dorso: da
    /// aperto non si vede), copertina che gira all'apertura, fogli che si
    /// sfogliano cambiando sezione e segnalibri di pelle fra le pagine.
    /// L'ordine dei figli e' l'ordine di disegno: pagine, segnalibri a riposo,
    /// testo, poi lo strato dei fogli che girano.
    /// </summary>
    static TMP_Text Booklet(RectTransform modal,TableOverlayController controller)
    {
        // k: le posizioni del testo sono state misurate sul libro largo 1380.
        float s=BookW/BookSrcW, PH=BookSrcH*s, gutter=BookSrcGutter*s, k=BookW/1380f;
        var panel=Rect("Booklet",modal,(W-BookW)*.5f,(H-PH)*.5f,BookW,PH);controller.detail=panel.gameObject;
        var view=panel.gameObject.AddComponent<BookletView>();
        view.controller=controller;controller.booklet=view;
        view.book=panel.gameObject.AddComponent<CanvasGroup>();

        var left=Art("SpreadLeft",panel,"book_spread_left",0,0,gutter,PH);left.raycastTarget=false;
        Art("SpreadRight",panel,"book_spread_right",gutter,0,BookW-gutter,PH).raycastTarget=false;
        view.spreadLeft=left;
        view.tabsUnder=Hinge("Bookmarks",panel,gutter,PH);

        var leftPage=Page("LeftPage",panel,BookW,PH);var rightPage=Page("RightPage",panel,BookW,PH);
        view.leftContent=leftPage.GetComponent<CanvasGroup>();view.rightContent=rightPage.GetComponent<CanvasGroup>();
        controller.heading=Text("Title",leftPage,"IL CAMPO",150*k,92*k,450*k,46,29,GamePalette.InkStrong);
        UiBuild.Fill(Rect("TitleRule",leftPage,150*k,92*k+50,450*k,2),new Color(GamePalette.Ink.r,GamePalette.Ink.g,GamePalette.Ink.b,.45f)).raycastTarget=false;
        controller.choices=Rect("Choices",leftPage,150*k,92*k+72,450*k,540*k);
        controller.inspector=Inspector(Rect("Inspector",rightPage,gutter+56*k,92*k,500*k,610*k));
        var logBox=Rect("Log",rightPage,gutter+56*k,92*k,500*k,610*k);controller.logPanel=logBox.gameObject;
        var log=ScrollText(logBox,"CombatLog",0,0,500*k,610*k,18);
        log.GetComponentInParent<ScrollRect>().gameObject.AddComponent<LogPanel>();
        logBox.gameObject.SetActive(false);
        Bind(InkButton("Close",rightPage,"CHIUDI",BookW-300*k,28*k,126,34),controller.Close);

        // Fogli e copertina ruotano attorno alla piega: stanno in un rect a
        // dimensione zero centrato sulla piega, e il foglio ne e' l'integrale.
        view.flipLayer=Hinge("Turning",panel,gutter,PH);
        float faceTop=BookSrcFaceTop*s,faceBottom=BookSrcFaceBottom*s;
        float leafW=(BookSrcPageRight-BookSrcGutter)*s,leafH=faceBottom-faceTop,leafY=PH*.5f-(faceTop+faceBottom)*.5f;
        view.pageWidth=leafW;
        view.leaves=new BookLeaf[3];
        for(int i=0;i<3;i++)view.leaves[i]=Leaf("Leaf"+i,view.flipLayer,leafW,leafH,leafY,"book_leaf_right","book_leaf_left");
        view.cover=Leaf("Cover",view.flipLayer,BookW-gutter,PH,0,"book_cover_front","book_spread_left");

        // Segnalibri: uno per sezione, sul taglio esterno. Il componente li mette
        // a destra o a sinistra secondo la sezione aperta.
        view.tabCarriers=new RectTransform[BookSections.Length];view.tabs=new BookmarkTab[BookSections.Length];
        view.tabHeights=new float[BookSections.Length];
        for(int i=0;i<BookSections.Length;i++)
        {
            view.tabHeights[i]=PH*.5f-(196+i*112)*k;
            var (carrier,tab,button)=Bookmark(view.tabsUnder,i);
            view.tabCarriers[i]=carrier;view.tabs[i]=tab;
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(button.onClick,view.Select,i);
        }
        return log;
    }

    /// <summary>Rect a dimensione zero sulla piega: lo spazio del perno di fogli e segnalibri.</summary>
    static RectTransform Hinge(string name,RectTransform panel,float gutter,float height)
    {
        var rt=Rect(name,panel,gutter,height*.5f,0,0);
        rt.pivot=new Vector2(.5f,.5f);
        return rt;
    }

    static RectTransform Page(string name,RectTransform panel,float w,float h)
    {
        var rt=Rect(name,panel,0,0,w,h);
        var group=rt.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=true;
        return rt;
    }

    static BookLeaf Leaf(string name,RectTransform hinge,float w,float h,float centerY,string front,string back)
    {
        var rt=UiBuild.Rect(name,hinge);rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);rt.sizeDelta=Vector2.zero;
        var leaf=rt.gameObject.AddComponent<BookLeaf>();
        leaf.material=MedallionSceneSkin.BookLeafMaterial();
        leaf.front=UiSkin.Sprite(front).texture;leaf.back=UiSkin.Sprite(back).texture;
        leaf.size=new Vector2(w,h);leaf.centerY=centerY;leaf.raycastTarget=false;
        rt.gameObject.SetActive(false);
        return leaf;
    }

    /// <summary>
    /// Un segnalibro: portatore sul taglio della pagina, linguetta di pelle che
    /// ne esce, scritta e bottone. La linguetta parte un poco dentro la pagina,
    /// come se fosse infilata fra i fogli.
    /// </summary>
    static (RectTransform,BookmarkTab,Button) Bookmark(RectTransform layer,int index)
    {
        const float TabW=196,TabH=84,Tuck=8;
        var carrier=UiBuild.Rect("Bookmark_"+BookSections[index],layer);
        carrier.anchorMin=carrier.anchorMax=carrier.pivot=new Vector2(.5f,.5f);carrier.sizeDelta=Vector2.zero;
        var body=UiBuild.Rect("Tab",carrier);
        body.anchorMin=body.anchorMax=new Vector2(.5f,.5f);body.pivot=new Vector2(0f,.5f);body.sizeDelta=new Vector2(TabW,TabH);
        body.anchoredPosition=new Vector2(-Tuck,0f);
        var image=UiBuild.Fill(body,Color.white,true);image.sprite=UiSkin.Sprite("bookmark_"+BookSections[index].ToLowerInvariant());
        var button=body.gameObject.AddComponent<Button>();button.targetGraphic=image;
        var colors=button.colors;colors.highlightedColor=new Color(1f,.96f,.88f);colors.pressedColor=new Color(.85f,.8f,.72f);button.colors=colors;
        var label=UiBuild.Text("Label",body,BookSections[index],16,new Color(.95f,.89f,.74f));
        label.font=UiBuild.Font;label.fontSize=16;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
        label.characterSpacing=6;
        var lrt=label.rectTransform;lrt.anchorMin=lrt.anchorMax=new Vector2(0f,.5f);lrt.pivot=new Vector2(.5f,.5f);
        lrt.sizeDelta=new Vector2(118,30);lrt.anchoredPosition=new Vector2(84,0);
        var tab=carrier.gameObject.AddComponent<BookmarkTab>();tab.body=body;tab.label=label;tab.tuck=Tuck;
        return (carrier,tab,button);
    }

    /// <summary>Bottone d'inchiostro sulla pagina: niente riquadro, solo la parola sottolineata.</summary>
    static Button InkButton(string name,Transform parent,string label,float x,float y,float w,float h)
    {
        var rt=Rect(name,parent,x,y,w,h);
        var hit=UiBuild.Fill(rt,new Color(0,0,0,0),true);
        var text=Text("Label",rt,label+"  ×",0,0,w,h-6,19,GamePalette.InkStrong);
        text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;text.characterSpacing=4;
        UiBuild.Fill(Rect("Rule",rt,10,h-5,w-20,1.5f),new Color(GamePalette.Ink.r,GamePalette.Ink.g,GamePalette.Ink.b,.5f)).raycastTarget=false;
        var button=rt.gameObject.AddComponent<Button>();button.targetGraphic=text;
        var colors=button.colors;colors.highlightedColor=new Color(.72f,.36f,.22f);button.colors=colors;
        return button;
    }

    /// <summary>
    /// Fixed rollers frame a masked paper viewport; only the text travels.
    /// </summary>
    // Il corpo avorio del rullo va dal 12,4% all'87,5% della texture (misurato
    // sulla riga centrale di roller.png): fuori ci sono i tappi.
    const float RollerBodyStart=.124f, RollerBodyEnd=.875f;

    /// <summary>Quanto il rullo deve sporgere per lato, in frazione della larghezza del foglio.</summary>
    static float RollerOverhang(float paperWidth,float bleed=10f)
        => (RollerBodyStart+bleed/paperWidth)/(RollerBodyEnd-RollerBodyStart);

    static void Parchment(RectTransform modal,TableOverlayController controller)
    {
        const float PW=1080,PH=880;
        var panel=Rect("Parchment",modal,(W-PW)*.5f,90,PW,PH);controller.legend=panel.gameObject;

        // Il viewport segue il pannello: e' la sua maschera che scopre il foglio
        // mentre il rotolo si apre. Se restasse di misura fissa, l'animazione
        // sposterebbe una finestra gia' piena invece di srotolare qualcosa.
        var paperCore=UiBuild.Fill(Rect("PaperCore",panel,20,36,PW-40,PH-72),new Color(.89f,.85f,.73f));
        UiBuild.Stretch(paperCore.rectTransform,20,36,20,36);
        var paperBackdrop=Art("Paper",panel,"upgrade_parchment",0,36,PW,PH-72);
        UiBuild.Stretch(paperBackdrop.rectTransform,0,36,0,36);
        var view=UiBuild.Rect("Viewport",panel);UiBuild.Stretch(view,48,92,48,76);
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

        var sheet=UiBuild.Rect("Sheet",content);
        var sheetLayout=sheet.gameObject.AddComponent<VerticalLayoutGroup>();
        sheetLayout.padding=new RectOffset(32,32,12,32);sheetLayout.spacing=26;
        sheetLayout.childControlWidth=true;sheetLayout.childControlHeight=true;
        sheetLayout.childForceExpandWidth=true;sheetLayout.childForceExpandHeight=false;
        sheet.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        Legend(sheet);
        // I rulli sporgono dal pannello: il foglio deve stare tutto sotto il corpo
        // avorio, e i tappi d'ottone restano fuori dal suo taglio. Le ancore oltre
        // 0..1 tengono la proporzione anche mentre MedallionScroll apre il rotolo.
        float overhang=RollerOverhang(PW);
        var top=Art("RollerTop",panel,"upgrade_roller",0,0,PW,76);
        top.rectTransform.anchorMin=new Vector2(-overhang,1);top.rectTransform.anchorMax=new Vector2(1+overhang,1);
        top.rectTransform.pivot=new Vector2(.5f,1);top.rectTransform.anchoredPosition=Vector2.zero;top.rectTransform.sizeDelta=new Vector2(0,76);
        var bottom=Art("RollerBottom",panel,"upgrade_roller",0,0,PW,76);
        bottom.rectTransform.anchorMin=new Vector2(-overhang,0);bottom.rectTransform.anchorMax=new Vector2(1+overhang,0);
        bottom.rectTransform.pivot=new Vector2(.5f,0);bottom.rectTransform.anchoredPosition=Vector2.zero;bottom.rectTransform.sizeDelta=new Vector2(0,76);
        top.material=bottom.material=MedallionSceneSkin.AnimatedSurface(true);
        var motion=panel.gameObject.AddComponent<ParchmentRollerMotion>();motion.scroll=scroll;motion.top=top;motion.bottom=bottom;

        Bind(PaperButton("Close",panel,"CHIUDI  x",PW-250,78,190,38),controller.Close);

        var unroll=panel.gameObject.AddComponent<MedallionScroll>();
        unroll.openSize=new Vector2(PW,PH);
        unroll.openPosition=panel.anchoredPosition;
        // Si srotola partendo dal rotolo posato sul tavolo.
        float scrollH=ScrollWidth*300f/1000f;
        unroll.closedPosition=new Vector2(ScrollCenterX-ScrollWidth*.5f,-(ScrollCenterY-scrollH*.5f));
        unroll.closedSize=new Vector2(ScrollWidth,scrollH);
        panel.gameObject.SetActive(false);
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

    static InspectorPanel Inspector(RectTransform box)
    {
        float w=box.rect.width,h=box.rect.height;
        var inspector=box.gameObject.AddComponent<InspectorPanel>();
        // L'immagine vera accanto al nome: ritratto della carta o simbolo della casella.
        const float Pic=96;
        var frame=UiBuild.Fill(Rect("PortraitFrame",box,8,0,Pic,Pic*1.25f),new Color(GamePalette.Ink.r,GamePalette.Ink.g,GamePalette.Ink.b,.08f));
        frame.raycastTarget=false;
        var pic=Rect("Portrait",box,12,4,Pic-8,Pic*1.25f-8);
        inspector.portrait=UiBuild.Fill(pic,Color.white);inspector.portrait.preserveAspect=true;inspector.portrait.raycastTarget=false;
        float tx=Pic+20;
        inspector.titleText=Text("Title",box,"ISPETTORE",tx,0,w-tx-8,42,28,GamePalette.InkStrong);
        inspector.subtitleText=Icons(Text("Subtitle",box,"",tx,45,w-tx-8,30,19,GamePalette.InkMuted));
        inspector.sideText=Icons(Text("Side",box,"",tx,80,w-tx-8,30,18,GamePalette.InkMuted));
        inspector.bodyText=Icons(ScrollText(box,"Body",8,Pic*1.25f+12,w-16,h-Pic*1.25f-80,20));
        inspector.bodyText.lineSpacing=10;
        inspector.hintText=Icons(Text("Hint",box,"",8,h-58,w-16,40,17,GamePalette.InkMuted));
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

    static TMP_SpriteAsset _icons;

    /// <summary>I testi con simboli leggono lo sprite asset delle icone del tavolo.</summary>
    static TMP_Text Icons(TMP_Text text)
    {
        if(_icons==null)_icons=MedallionSceneSkin.IconSprites();
        text.spriteAsset=_icons;
        return text;
    }

    /// <summary>
    /// La legenda: per ogni cosa del tavolo il suo simbolo e una riga. Le icone
    /// sono i disegni veri (sprite asset ui_icons), cosi' si impara a riconoscere
    /// quello che poi si vede sulle carte e sulla cassa, non una parola che lo
    /// descrive.
    /// </summary>
    static void Legend(RectTransform sheet)
    {
        LegendSection(sheet,"LE CARTE",2,
            ("card_front","fronte: attacca"),("card_back","retro: para ed e' un'insegna"),
            ("flip","doppio clic: gira · 1STAR"),("swap","trascina fra corsie: scambia · 1STAR"));
        LegendSection(sheet,"I SEGNI",2,
            ("drop","vita"),("atk","attacco"),("def","difesa"),("charge","carica: +1ATK al prossimo colpo"));
        LegendSection(sheet,"LE FAZIONI",3,("sun","Braci"),("moon","Abissi"),("saturn","Rovi"));
        LegendSection(sheet,"INSEGNE E RISONANZA",1,
            ("spade","da coperta: +ATK alle vicine della stessa fazione"),
            ("club","da coperta: +DEF alle vicine della stessa fazione"),
            ("broken","carta e casella della stessa fazione: nessuno para"));
        LegendSection(sheet,"LA CASSA",2,
            ("lamp_hp","vita della casella"),("lamp_atk","attacco della casella"),
            ("lamp_def","difesa della casella"),("led_boss","vita del boss"));
        LegendSection(sheet,"IL TAVOLO",2,
            ("star","un AP: si spegne quando lo spendi"),("deck","pesca · 1STAR"),
            ("button","attacca"),("lever","chiudi il turno"),
            ("led_player","la tua vita"),("book","dettaglio e registro"));
        LegendSection(sheet,"IL COLPO",1,
            ("atk","ARROW oltre la LAMPHP ARROW LEDBOSS  lo paga il boss"),
            ("lamp_atk","ARROW oltre la DROP ARROW LEDPLAYER  lo paghi tu"),
            ("led_player","a zero: perde chi ci arriva"));
    }

    /// <summary>Titolo in oro e una griglia di voci: icona grande e una riga d'inchiostro.</summary>
    static void LegendSection(RectTransform sheet,string title,int columns,params (string icon,string caption)[] entries)
    {
        var heading=UiBuild.Text("Heading_"+title,sheet,title,22,GamePalette.InkGold);
        heading.font=UiBuild.Font;heading.fontSize=22;heading.raycastTarget=false;heading.characterSpacing=6;
        heading.gameObject.AddComponent<LayoutElement>().minHeight=30;

        var grid=UiBuild.Rect("Grid_"+title,sheet);
        var layout=grid.gameObject.AddComponent<GridLayoutGroup>();
        const float Width=920,RowH=56;
        layout.constraint=GridLayoutGroup.Constraint.FixedColumnCount;layout.constraintCount=columns;
        layout.spacing=new Vector2(18,4);
        layout.cellSize=new Vector2((Width-18*(columns-1))/columns,RowH);
        int rows=(entries.Length+columns-1)/columns;
        grid.gameObject.AddComponent<LayoutElement>().preferredHeight=rows*RowH+(rows-1)*4;
        foreach(var (icon,caption) in entries)
        {
            var cell=UiBuild.Rect("Entry_"+icon,grid);
            var text=Icons(UiBuild.Text("Text",cell,"<size=190%><voffset=-.1em>"+Glyph(icon)+"</voffset></size>  "+Symbols(caption),20,GamePalette.InkBody));
            text.font=UiBuild.Font;text.fontSize=20;text.raycastTarget=false;
            text.alignment=TextAlignmentOptions.MidlineLeft;text.textWrappingMode=TextWrappingModes.NoWrap;
            UiBuild.Stretch(text.rectTransform);
        }
    }

    static string Glyph(string name) => "<sprite name=\""+name+"\">";

    /// <summary>Segnaposti in maiuscolo nelle didascalie: si leggono meglio delle etichette TMP.</summary>
    static string Symbols(string caption) => caption
        .Replace("STAR",Glyph("star")).Replace("ATK",Glyph("atk")).Replace("DEF",Glyph("def"))
        .Replace("ARROW",Glyph("arrow")).Replace("LAMPHP",Glyph("lamp_hp")).Replace("LEDBOSS",Glyph("led_boss"))
        .Replace("LEDPLAYER",Glyph("led_player")).Replace("DROP",Glyph("drop"));
}
