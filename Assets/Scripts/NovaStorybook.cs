using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Six story screens share one predictable navigation system.
// Coordinates use a 720 x 1280 portrait canvas and scale uniformly to fit.
public class NovaStorybook : MonoBehaviour
{
    public static NovaStorybook Instance;
    public int Page { get; private set; }
    public bool[] Complete = new bool[4];
    public bool Muted { get; private set; }
    public bool ReducedMotion { get; private set; }
    public int Energy { get; private set; }
    public int Alignment { get; private set; }
    public int Sequence { get; private set; }
    readonly bool[] cells = new bool[3];
    readonly int[] code = { 0, 2, 1 };
    readonly string[] titles = { "", "A tiny spark", "A little patience", "Listen closely", "You are not alone" };
    readonly string[] locations = { "", "01   THE MOON", "02   MARS", "03   SATURN", "04   EARTH" };
    readonly string[] story = {
        "A small explorer. A very big universe. Help Nova follow a quiet signal all the way home.",
        "Nova hears a tiny beep beyond the Moon. Someone is calling! But the little ship needs energy. Three glowing cells drift nearby.",
        "The signal leads Nova to Mars. The antenna points the wrong way. Nova takes a breath. A small turn might make a big difference.",
        "Near Saturn, the signal becomes a pattern. Nova listens: one, three, two. The message stays on the screen. There is no need to hurry.",
        "The call came from Earth. A small lighthouse has lost its light. Nova has found the message. One kind reply can help a friend feel less alone."
    };
    Color navy = Hex("101B35"), panel = Hex("1C2B49"), cream = Hex("FFF2D5"), teal = Hex("89E4D0"), dim = Hex("BAC8DF"), gold = Hex("F9C778");
    Canvas canvas;
    RectTransform root, pageRoot, modal, planet, dish;
    Font font;
    Sprite round;
    AudioSource effects;
    AudioClip click, success;
    float volume = .55f, phase;
    Text feedback;
    GameObject modalOpener;
    string feedbackOverride;
    readonly List<RectTransform> twinkles = new List<RectTransform>();
    readonly List<float> twinkleY = new List<float>();
    readonly Dictionary<string,Sprite> sprites = new Dictionary<string,Sprite>();
    bool qaMode;

    public static Color Hex(string v) { ColorUtility.TryParseHtmlString("#"+v, out var c); return c; }
    void Awake()
    {
        Instance = this;
        Application.targetFrameRate = 60;
        font = Resources.Load<Font>("Nova/LiberationSans");
        round = MakeRound();
        effects = gameObject.AddComponent<AudioSource>();
        if(FindAnyObjectByType<AudioListener>()==null)gameObject.AddComponent<AudioListener>();
        click = Tone(520, .08f); success = Tone(880, .25f);
        canvas = new GameObject("Canvas",typeof(Canvas),typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var bg = Box(canvas.transform,"Letterbox",0,0,720,1280,Hex("080E1D"),false);
        var bgRect = bg.rectTransform; bgRect.anchorMin=Vector2.zero; bgRect.anchorMax=Vector2.one; bgRect.offsetMin=Vector2.zero; bgRect.offsetMax=Vector2.zero;
        root = new GameObject("Portrait page",typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(canvas.transform,false); root.anchorMin=root.anchorMax=root.pivot=new Vector2(.5f,.5f); root.sizeDelta=new Vector2(720,1280);
        if (FindAnyObjectByType<EventSystem>() == null) new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
        Muted=PlayerPrefs.GetInt("nova.muted",0)==1; ReducedMotion=PlayerPrefs.GetInt("nova.motion",0)==1;
        volume=PlayerPrefs.GetFloat("nova.volume",.55f); SetVolumes();
        qaMode=Array.Exists(Environment.GetCommandLineArgs(),a=>a=="-novaQA");
        if(!qaMode) {
            for(int i=0;i<4;i++) Complete[i]=PlayerPrefs.GetInt("nova.done"+i,0)==1;
            for(int i=0;i<3;i++) cells[i]=PlayerPrefs.GetInt("nova.cell"+i,0)==1;
            Energy=Array.FindAll(cells,x=>x).Length;
            Alignment=PlayerPrefs.GetInt("nova.align",0); Sequence=PlayerPrefs.GetInt("nova.sequence",0);
        }
        ShowPage(0);
        if(qaMode) StartCoroutine(QaRun());
    }
    void Update()
    {
        canvas.scaleFactor=Mathf.Min(Screen.width/720f,Screen.height/1280f);
        phase+=Time.unscaledDeltaTime;
        if(planet!=null) planet.localRotation=Quaternion.Euler(0,0,ReducedMotion?0:Mathf.Sin(phase*.32f)*3f);
        for(int i=0;i<twinkles.Count;i++) if(twinkles[i]!=null) {
            var p=twinkles[i].anchoredPosition; p.y=twinkleY[i]+(ReducedMotion?0:Mathf.Sin(phase*.7f+i)*5); twinkles[i].anchoredPosition=p;
        }
        if(Input.GetKeyDown(KeyCode.Tab)) {
            var scope=modal!=null?modal:pageRoot;
            var choices=Array.FindAll(scope.GetComponentsInChildren<Button>(),b=>b.IsInteractable());
            int current=Array.FindIndex(choices,b=>b.gameObject==EventSystem.current.currentSelectedGameObject);
            int step=Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)?-1:1;
            if(choices.Length>0)EventSystem.current.SetSelectedGameObject(choices[(current+step+choices.Length)%choices.Length].gameObject);
        }
        if(Input.GetKeyDown(KeyCode.Escape)) { if(modal!=null) CloseModal(); else ShowPage(0); }
    }
    RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent,false);
        r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1); r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
    }
    Image Box(Transform parent,string name,float x,float y,float w,float h,Color color,bool rounded=true)
    {
        var i=Rect(parent,name,x,y,w,h).gameObject.AddComponent<Image>();i.color=color;i.raycastTarget=false;
        if(rounded) {i.sprite=round;i.type=Image.Type.Sliced;} return i;
    }
    Text Label(Transform parent,string value,float x,float y,float w,float h,int size,Color color,bool bold=false,TextAnchor align=TextAnchor.UpperLeft)
    {
        var t=Rect(parent,value.Length>25?value.Substring(0,25):value,x,y,w,h).gameObject.AddComponent<Text>();
        t.font=font;t.text=value;t.fontSize=size;t.color=color;t.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;
        t.alignment=align;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;t.raycastTarget=false;return t;
    }
    Button Button(Transform parent,string value,float x,float y,float w,float h,Action action,bool primary=false)
    {
        var img=Box(parent,value,x,y,w,h,primary?teal:panel);img.raycastTarget=true;
        var b=img.gameObject.AddComponent<Button>();b.targetGraphic=img;
        var cs=b.colors;cs.normalColor=Color.white;cs.highlightedColor=Hex("D8E9EF");cs.pressedColor=Hex("A6B6D1");cs.selectedColor=Hex("D2DFFF");cs.disabledColor=Hex("63738A");b.colors=cs;
        Label(img.transform,value,10,0,w-20,h,27,primary?navy:cream,true,TextAnchor.MiddleCenter);
        b.onClick.AddListener(()=>{Play(false);action();});return b;
    }
    Image Art(Transform parent,string name,float x,float y,float w,float h)
    {
        var img=Box(parent,name,x,y,w,h,Color.white,false);img.sprite=LoadSprite(name);img.preserveAspect=true;return img;
    }
    Sprite LoadSprite(string name)
    {
        if(sprites.TryGetValue(name,out var s))return s;
        var texture=Resources.Load<Texture2D>("Nova/"+name);
        if(texture==null)return null;
        s=Sprite.Create(texture,new UnityEngine.Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100);sprites[name]=s;return s;
    }
    void Stars(Transform parent)
    {
        twinkles.Clear();twinkleY.Clear();
        for(int i=0;i<24;i++) {
            float x=35+(i*127)%650,y=330+(i*71)%430,sz=i%3==0?7:4;
            var a=Box(parent,"Star",x,y,sz,sz,i%4==0?gold:dim);twinkles.Add(a.rectTransform);twinkleY.Add(-y);
        }
    }
    void Nova(Transform parent,float x,float y)
    {
        var r=Rect(parent,"Nova spacecraft",x,y,150,110);
        Box(r,"Engine",57,82,38,25,gold);Box(r,"Left wing",0,54,40,32,teal);Box(r,"Right wing",110,54,40,32,teal);
        Box(r,"Body",35,15,80,78,cream);Box(r,"Window",52,30,46,40,navy);
        Box(r,"Eye one",62,42,6,9,teal);Box(r,"Eye two",81,42,6,9,teal);
        Box(r,"Aerial",73,0,5,19,gold);Box(r,"Aerial light",68,0,15,10,gold);
    }
    public void ShowPage(int page)
    {
        Page=Mathf.Clamp(page,0,5);CloseModal();feedbackOverride=null;planet=null;
        if(pageRoot!=null) {pageRoot.gameObject.SetActive(false);Destroy(pageRoot.gameObject);}
        pageRoot=Rect(root,"Screen "+Page,0,0,720,1280);Box(pageRoot,"Background",0,0,720,1280,navy,false);
        Label(pageRoot,"NOVA / STORY EXPLORER",42,40,460,42,22,teal,true);
        Button(pageRoot,"Settings",525,26,155,88,Settings);
        if(Page==0) Home();else if(Page==5) Credits();else StoryPage();
        if(Page>0&&Page<5) {
            Button(pageRoot,"< Back",40,1136,195,96,()=>ShowPage(Page-1));
            Button(pageRoot,"Map",260,1136,195,96,Map);
            Button(pageRoot,Page==4?"Credits >":"Next >",485,1136,195,96,()=>ShowPage(Page+1),true);
            Label(pageRoot,"STORY "+Page+" OF 4",225,1244,270,25,18,dim,false,TextAnchor.MiddleCenter);
        }
        if(!qaMode) {PlayerPrefs.SetInt("nova.page",Page);PlayerPrefs.Save();}
        var buttons=pageRoot.GetComponentsInChildren<Button>();if(buttons.Length>0)EventSystem.current.SetSelectedGameObject(buttons[0].gameObject);
    }
    void Home()
    {
        Label(pageRoot,"Nova and the\nlost signal",42,143,640,180,72,cream,true);
        Label(pageRoot,story[0],44,345,575,120,32,dim);
        Stars(pageRoot);planet=Art(pageRoot,"Earth",255,500,380,380).rectTransform;Nova(pageRoot,88,650);
        Label(pageRoot,"A story about listening and helping",44,904,620,52,28,cream);
        Button(pageRoot,"Start the story",40,976,640,96,()=>{ResetProgress();ShowPage(1);},true);
        Button(pageRoot,"Explore pages",40,1094,310,96,Map);
        Button(pageRoot,"Credits",370,1094,310,96,()=>ShowPage(5));
        Label(pageRoot,"By hanchenxi  /  Ages 6-9",42,1230,638,32,22,dim);
    }
    void StoryPage()
    {
        Label(pageRoot,locations[Page],42,135,636,32,23,teal,true);
        Label(pageRoot,titles[Page],40,179,640,76,52,cream,true);
        Label(pageRoot,story[Page],42,275,632,165,32,cream);
        Stars(pageRoot);
        var names=new[]{"","Moon","Mars","Saturn_00","Earth"};
        planet=Art(pageRoot,names[Page],205,440,360,350).rectTransform;
        Nova(pageRoot,58,649);
        if(Page==1) {
            for(int i=0;i<3;i++) {int n=i; var b=Button(pageRoot,cells[i]?"Ready":"Cell "+(i+1),new[]{73,301,529}[i],new[]{483,455,570}[i],118,92,()=>Collect(n),!cells[i]);b.interactable=!cells[i];}
        }
        if(Page==2) {
            dish=Rect(pageRoot,"Antenna",480,645,130,135);dish.pivot=new Vector2(.5f,.5f);
            Box(dish,"Mast",55,38,12,96,gold);Box(dish,"Receiver",12,0,100,38,teal);
            dish.localRotation=Quaternion.Euler(0,0,60-Alignment*20);
        }
        if(Page==4&&Complete[3]) {
            Label(pageRoot,"HELLO, FRIEND",290,770,380,48,32,gold,true,TextAnchor.MiddleCenter);
            for(int i=0;i<5;i++)Box(pageRoot,"Reply beam",315+i*45,714-i*16,18,18,teal);
        }
        Box(pageRoot,"Feedback banner",40,825,640,98,panel);
        feedback=Label(pageRoot,Status(),58,838,604,72,27,teal,true,TextAnchor.MiddleCenter);
        if(Page==1) Button(pageRoot,"Try this page again",40,935,640,88,ResetCurrent);
        if(Page==2) Button(pageRoot,Complete[1]?"Try this page again":"Turn the antenna",40,935,640,88,()=>{if(Complete[1])ResetCurrent();else Turn();},true);
        if(Page==3) {
            for(int i=0;i<3;i++){int n=i;Button(pageRoot,(i+1).ToString(),40+i*220,935,200,88,()=>Signal(n),true);}
        }
        if(Page==4) Button(pageRoot,Complete[3]?"Send another hello":"Send a kind reply",40,935,640,88,Transmit,true);
        Button(pageRoot,Muted?"Sound: off":"Sound: on",40,1034,310,88,()=>{Muted=!Muted;SetVolumes();Prefs();ShowPage(Page);});
        Button(pageRoot,"Need a hint?",370,1034,310,88,Hint);
    }
    string Status()
    {
        if(feedbackOverride!=null)return feedbackOverride;
        if(Page==1)return Complete[0]?"Ship charged! Nova is ready to go.":"Tap the three cells. Energy: "+Energy+" / 3";
        if(Page==2)return Complete[1]?"Signal found! Small steps worked.":"Turn the antenna. Alignment: "+Alignment+" / 3";
        if(Page==3)return Complete[2]?"Message decoded: a friend needs help!":"Follow 1 > 3 > 2. Matched: "+Sequence+" / 3";
        return Complete[3]?"The lighthouse shines. Someone heard.":"Let the lighthouse know you are here.";
    }
    public void Collect(int n)
    {
        if(n<0||n>2||cells[n])return;cells[n]=true;Energy++;Complete[0]=Energy==3;Save();Play(Complete[0]);ShowPage(1);
    }
    public void Turn() {Alignment=Mathf.Min(3,Alignment+1);Complete[1]=Alignment==3;Save();Play(Complete[1]);ShowPage(2);}
    public void Signal(int n)
    {
        if(Complete[2]) {Sequence=0;Complete[2]=false;}
        if(n==code[Sequence]) {Sequence++;Complete[2]=Sequence==3;feedbackOverride=Complete[2]?"Message decoded: a friend needs help!":null;Play(Complete[2]);}
        else {Sequence=0;feedbackOverride="Let's try again. Follow 1 > 3 > 2.";}
        Save();feedback.text=Status();
    }
    public void Transmit() {Complete[3]=true;Save();Play(true);ShowPage(4);if(!ReducedMotion)StartCoroutine(ReplyPulse());}
    IEnumerator ReplyPulse()
    {
        RectTransform p=planet;float t=0;while(t<.65f&&p!=null){t+=Time.unscaledDeltaTime;p.localScale=Vector3.one*(1+Mathf.Sin(t/.65f*Mathf.PI)*.07f);yield return null;}if(p!=null)p.localScale=Vector3.one;
    }
    void Save()
    {
        if(qaMode)return;
        for(int i=0;i<4;i++)PlayerPrefs.SetInt("nova.done"+i,Complete[i]?1:0);
        for(int i=0;i<3;i++)PlayerPrefs.SetInt("nova.cell"+i,cells[i]?1:0);
        PlayerPrefs.SetInt("nova.align",Alignment);PlayerPrefs.SetInt("nova.sequence",Sequence);PlayerPrefs.Save();
    }
    public void ResetProgress() {Array.Clear(cells,0,3);Array.Clear(Complete,0,4);Energy=Alignment=Sequence=0;Save();}
    void ResetCurrent()
    {
        if(Page==1){Array.Clear(cells,0,3);Energy=0;}if(Page==2)Alignment=0;if(Page==3)Sequence=0;
        Complete[Page-1]=false;Save();ShowPage(Page);
    }
    void Credits()
    {
        Label(pageRoot,"THE END",42,150,630,42,24,teal,true);
        Label(pageRoot,"A little kindness\ntravels far",40,215,640,150,56,cream,true);
        Art(pageRoot,"Earth",445,390,215,215);Nova(pageRoot,130,460);
        Label(pageRoot,"Story and project",42,675,620,36,25,teal,true);
        Label(pageRoot,"hanchenxi",42,717,620,52,38,cream,true);
        Label(pageRoot,"Planet images and Liberation Sans font:\nprovided 2DStoryStarter_2022 course project.\nSound effects: generated tones.\nFictional space journey; not a science simulation.",42,789,635,193,25,dim);
        Button(pageRoot,"Read again",40,1017,640,96,()=>{ResetProgress();ShowPage(1);},true);
        Button(pageRoot,"< Last page",40,1137,310,96,()=>ShowPage(4));Button(pageRoot,"Home",370,1137,310,96,()=>ShowPage(0));
    }
    void Modal(string heading)
    {
        CloseModal();modalOpener=EventSystem.current.currentSelectedGameObject;
        var group=pageRoot.GetComponent<CanvasGroup>();if(group==null)group=pageRoot.gameObject.AddComponent<CanvasGroup>();
        group.interactable=false;group.blocksRaycasts=false;
        modal=Rect(root,"Dialog",0,0,720,1280);var shade=Box(modal,"Shade",0,0,720,1280,new Color(.015f,.035f,.08f,.94f),false);shade.raycastTarget=true;
        Box(modal,"Dialog background",30,150,660,1000,navy);Label(modal,heading,58,200,600,75,48,cream,true);
        var close=Button(modal,"Close",478,1024,176,96,CloseModal);
        EventSystem.current.SetSelectedGameObject(close.gameObject);
    }
    void CloseModal(){if(modal!=null){modal.gameObject.SetActive(false);Destroy(modal.gameObject);modal=null;
        var group=pageRoot.GetComponent<CanvasGroup>();if(group!=null){group.interactable=true;group.blocksRaycasts=true;}
        if(modalOpener!=null)EventSystem.current.SetSelectedGameObject(modalOpener);modalOpener=null;
    }}
    void Map()
    {
        Modal("Choose a page");Label(modal,"Explore in any order. Your progress stays.",58,290,598,68,28,dim);
        string[] names={"Home","1   The Moon","2   Mars","3   Saturn","4   Earth","Credits"};
        for(int i=0;i<6;i++){int n=i;string label=names[i];if(i>0&&i<5&&Complete[i-1])label+="  /  done";Button(modal,label,58,380+i*99,596,87,()=>ShowPage(n),i==Page);}
    }
    void Settings()
    {
        Modal("Make it comfortable");Label(modal,"Change these any time. Reading still works\nwith sound off and motion reduced.",58,300,595,105,30,dim);
        Button(modal,Muted?"Sound: off":"Sound: on",58,443,596,96,()=>{Muted=!Muted;SetVolumes();Prefs();Settings();},!Muted);
        Button(modal,ReducedMotion?"Motion: reduced":"Motion: gentle",58,568,596,96,()=>{ReducedMotion=!ReducedMotion;Prefs();Settings();},ReducedMotion);
        Label(modal,"Volume",58,720,400,48,30,cream,true);
        Button(modal,"Quieter",58,791,238,96,()=>{volume=Mathf.Clamp01(volume-.2f);SetVolumes();Prefs();Settings();});
        Button(modal,"Louder",416,791,238,96,()=>{volume=Mathf.Clamp01(volume+.2f);SetVolumes();Prefs();Settings();});
        Label(modal,Mathf.RoundToInt(volume*100)+"%",303,797,106,82,27,teal,true,TextAnchor.MiddleCenter);
        Label(modal,"Keyboard: Tab or arrows, Enter to choose.\nEscape closes a panel or returns home.",58,925,585,74,24,dim);
    }
    void Prefs(){if(qaMode)return;PlayerPrefs.SetInt("nova.muted",Muted?1:0);PlayerPrefs.SetInt("nova.motion",ReducedMotion?1:0);PlayerPrefs.SetFloat("nova.volume",volume);PlayerPrefs.Save();}
    void Hint()
    {
        string[] hints={"","Tap each labelled cell above the Moon.\nThe number increases after each tap.","Tap Turn the antenna three times.\nWatch the receiver turn towards the signal.","Tap 1, then 3, then 2.\nThe pattern stays visible. You can retry.","Tap Send a kind reply.\nWatch for the light and the written response."};
        Modal("A small hint");Label(modal,hints[Page]+"\n\nYou can also use Next to keep reading.\nThere is no timer and no score to lose.",58,350,590,400,34,cream);
    }
    void SetVolumes(){effects.volume=Muted?0:volume*.45f;}
    void Play(bool done){if(!Muted)effects.PlayOneShot(done?success:click);}
    AudioClip Tone(float hz,float duration)
    {
        int count=(int)(22050*duration);float[] a=new float[count];for(int i=0;i<count;i++){float t=i/22050f;a[i]=Mathf.Sin(2*Mathf.PI*hz*t)*Mathf.Sin(Mathf.PI*i/count)*.3f;}
        var c=AudioClip.Create("Synthesized feedback",count,1,22050,false);c.SetData(a,0);return c;
    }
    Sprite MakeRound()
    {
        int s=64;var t=new Texture2D(s,s,TextureFormat.RGBA32,false);var colors=new Color[s*s];
        for(int y=0;y<s;y++)for(int x=0;x<s;x++){float dx=Mathf.Max(16-x,x-47),dy=Mathf.Max(16-y,y-47);float d=Mathf.Sqrt(Mathf.Max(0,dx)*Mathf.Max(0,dx)+Mathf.Max(0,dy)*Mathf.Max(0,dy));colors[y*s+x]=new Color(1,1,1,Mathf.Clamp01(17-d));}
        t.SetPixels(colors);t.Apply();return Sprite.Create(t,new UnityEngine.Rect(0,0,s,s),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(20,20,20,20));
    }
    IEnumerator QaRun()
    {
        string dir=System.IO.Path.Combine(Application.dataPath,"../../qa");System.IO.Directory.CreateDirectory(dir);
        yield return null;yield return new WaitForEndOfFrame();
        for(int p=0;p<6;p++){ShowPage(p);yield return new WaitForSeconds(.12f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir,"screen"+p+".png"));yield return new WaitForSeconds(.15f);}
        ResetProgress();ShowPage(1);Collect(0);Collect(0);Collect(1);Collect(2);bool energy=Energy==3&&Complete[0];
        ShowPage(2);Turn();Turn();Turn();bool antenna=Alignment==3&&Complete[1];
        ShowPage(3);Signal(2);bool recovery=Sequence==0&&!Complete[2];Signal(0);Signal(2);Signal(1);bool seq=Complete[2];
        ShowPage(4);Transmit();bool reply=Complete[3];
        yield return new WaitForSeconds(.75f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir,"completed.png"));
        Settings();yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir,"settings.png"));yield return new WaitForSeconds(.2f);
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir,"checks.json"),"{\"energyUnique\":"+energy.ToString().ToLower()+",\"antenna\":"+antenna.ToString().ToLower()+",\"sequenceRecovery\":"+recovery.ToString().ToLower()+",\"sequence\":"+seq.ToString().ToLower()+",\"reply\":"+reply.ToString().ToLower()+"}");
        yield return new WaitForSeconds(.3f);Application.Quit(energy&&antenna&&recovery&&seq&&reply?0:2);
    }
}
