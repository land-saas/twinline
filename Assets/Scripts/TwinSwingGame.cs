using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Rope-swing collectathon: Space attaches to the nearest anchor, Space again releases.</summary>
public sealed class TwinSwingGame : MonoBehaviour
{
    public enum Mode { Ready, Countdown, Flying, Crashed, Paused }

    public Mode State { get; private set; }
    public int Score { get; private set; }
    public int Best { get; private set; }
    public bool Attached => swingJoint!=null;
    public Rigidbody2D player;
    public Camera sharedCamera;
    public LineRenderer rope;
    public float gravity=9.4f;
    public float attachRange=7f;
    public float scrollSpeed=2.1f;
    public float corridorHalfHeight=4.9f;
    public float anchorSpacing=6.2f;
    public float gemSpacing=4.6f;
    public float minRopeLength=.75f;
    public float maxRopeLength=6.8f;
    public float reelSpeed=3.6f;
    public float swingBoost=0.7f;
    public const int Seed=4173;
    public static readonly Color PlayerRed=new Color(1f,.28f,.22f);
    public static readonly Color NodeYellow=new Color(1f,.88f,.18f);
    public static readonly Color GemGold=new Color(1f,.68f,.08f);
    public static readonly Color RopeTint=new Color(1f,.92f,.42f);
    private static readonly Color Dark=Color.black;
    private static readonly Color Muted=new Color(.58f,.58f,.58f);
    private static readonly Color HudPaper=new Color(.95f,.95f,.95f);

    public sealed class GemPickup : MonoBehaviour
    {
        public TwinSwingGame game;
        public int index;
        public bool collected;
        public Transform visual;
        public CircleCollider2D pickupCollider;
    }

    private sealed class Anchor
    {
        public Rigidbody2D body;
        public Transform ring;
    }

    private readonly List<Anchor> anchors=new List<Anchor>();
    private readonly List<GemPickup> gems=new List<GemPickup>();
    private readonly List<UnityEngine.Object> resources=new List<UnityEngine.Object>();
    private DistanceJoint2D swingJoint;
    private Anchor activeAnchor;
    private Mesh disc;
    private Material ink;
    private AudioSource audioSource;
    private AudioClip attachSound,releaseSound,gemSound,crashSound;
    private System.Random rng;
    private Vector3 cameraCenter;
    private float countdown,crashAge,flash,shake,runTime,hintTime,attachPulse,attachRopeLength;
    private int nextAnchor,nextGem;
    private bool built;
    private Mode beforePause;
    private string contextHint="";
    private GUIStyle labelStyle,hintStyle,countStyle;

    private void Awake() { BuildWorld();ResetRun(); }

    public void BuildWorld()
    {
        if(built) return;
        built=true;
        Time.fixedDeltaTime=.01f;
        Physics2D.gravity=new Vector2(0,-9.81f);
        ink=new Material(Resources.Load<Shader>("FlatInk"));resources.Add(ink);
        disc=DiscMesh();resources.Add(disc);

        var playerObject=new GameObject("Swinger");playerObject.transform.SetParent(transform);
        player=playerObject.AddComponent<Rigidbody2D>();
        player.constraints=RigidbodyConstraints2D.FreezeRotation;
        player.mass=1;player.linearDamping=.02f;player.interpolation=RigidbodyInterpolation2D.Interpolate;
        player.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
        playerObject.AddComponent<CircleCollider2D>().radius=.205f;
        var avatar=playerObject.AddComponent<SwingPlayer>();avatar.game=this;
        Draw("Player body",disc,Vector2.zero,new Vector2(.235f,.235f),PlayerRed,12,playerObject.transform);

        sharedCamera=new GameObject("Shared camera").AddComponent<Camera>();
        sharedCamera.transform.SetParent(transform);sharedCamera.tag="MainCamera";
        sharedCamera.orthographic=true;sharedCamera.orthographicSize=corridorHalfHeight;
        sharedCamera.clearFlags=CameraClearFlags.SolidColor;sharedCamera.backgroundColor=Dark;
        sharedCamera.rect=new Rect(0,0,1,1);sharedCamera.cullingMask=1<<0;
        playerObject.AddComponent<AudioListener>();

        rope=Stroke("Swing rope",new Vector3[17],.024f,RopeTint,8,transform);
        rope.enabled=false;

        for(int sign=-1;sign<=1;sign+=2)
        {
            var border=new GameObject("Boundary "+sign);border.transform.SetParent(transform);
            border.transform.position=new Vector2(0,sign*(corridorHalfHeight+.5f));
            border.AddComponent<BoxCollider2D>().size=new Vector2(400,1);
        }

        for(int n=0;n<8;n++) anchors.Add(CreateAnchor(n));
        for(int n=0;n<10;n++) gems.Add(CreateGem(n));

        audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=0;
        attachSound=Tone(420,520,.08f);releaseSound=Tone(680,420,.1f);gemSound=Tone(880,1180,.12f);crashSound=Tone(150,42,.25f);
    }

    public void ResetRun()
    {
        State=Mode.Ready;Score=0;runTime=0;nextAnchor=0;nextGem=0;
        Best=PlayerPrefs.GetInt("TwinSwingBest",0);
        rng=new System.Random(Seed);
        countdown=crashAge=flash=shake=hintTime=attachPulse=0;
        contextHint="";
        ReleaseSwing();
        player.simulated=true;
        player.transform.position=player.position=new Vector2(0,1.2f);
        player.linearVelocity=Vector2.zero;player.angularVelocity=0;
        player.gravityScale=0;
        cameraCenter=CameraTarget();sharedCamera.transform.position=cameraCenter;
        LayoutCourse(0f);
        Physics2D.SyncTransforms();
    }

    public void StartRun(bool skipCountdown=false)
    {
        ResetRun();
        player.gravityScale=gravity/9.81f;
        player.simulated=skipCountdown;
        if(skipCountdown) State=Mode.Flying;
        else { State=Mode.Countdown;countdown=1.5f; }
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            if(State==Mode.Flying || State==Mode.Countdown) SetPaused(true);
            else if(State==Mode.Paused) SetPaused(false);
        }
        if(Input.GetKeyDown(KeyCode.Space)) HandleSpace();
        if(Input.GetKeyDown(KeyCode.Tab) && State==Mode.Ready) SwitchToCoopFlight();
        if(State==Mode.Crashed) crashAge+=Time.unscaledDeltaTime;
        flash=Mathf.MoveTowards(flash,0,Time.unscaledDeltaTime*2.8f);
        shake=Mathf.MoveTowards(shake,0,Time.unscaledDeltaTime*1.6f);
        attachPulse=Mathf.Max(0,attachPulse-Time.deltaTime);
    }

    private void FixedUpdate() => Step(Time.fixedDeltaTime);

    private void HandleSpace()
    {
        if(State==Mode.Ready) StartRun();
        else if(State==Mode.Crashed && crashAge>.2f) { ResetRun();StartRun(); }
        else if(State==Mode.Paused) SetPaused(false);
        else if(State==Mode.Flying) ToggleSwing();
    }

    public void ToggleSwing()
    {
        if(Attached) ReleaseSwing();
        else TryAttach();
    }

    public void TryAttach()
    {
        Anchor nearest=null;
        float nearestDistance=attachRange;
        foreach(var anchor in anchors)
        {
            float distance=Vector2.Distance(player.position,anchor.body.position);
            if(distance<=nearestDistance) { nearest=anchor;nearestDistance=distance; }
        }
        if(nearest==null) return;
        activeAnchor=nearest;
        attachRopeLength=Mathf.Clamp(nearestDistance,minRopeLength,maxRopeLength);
        ApplyAttachMomentum(nearest);
        swingJoint=player.gameObject.AddComponent<DistanceJoint2D>();
        swingJoint.connectedBody=nearest.body;
        swingJoint.autoConfigureConnectedAnchor=false;
        swingJoint.autoConfigureDistance=false;
        swingJoint.anchor=Vector2.zero;
        swingJoint.connectedAnchor=Vector2.zero;
        swingJoint.distance=attachRopeLength;
        swingJoint.maxDistanceOnly=false;
        swingJoint.enableCollision=false;
        swingJoint.breakForce=Mathf.Infinity;
        rope.enabled=true;
        attachPulse=.35f;
        Play(attachSound,.22f);
    }

    private void ApplyAttachMomentum(Anchor anchor)
    {
        Vector2 offset=player.position-(Vector2)anchor.body.position;
        if(offset.sqrMagnitude<.0001f) return;
        Vector2 radial=offset.normalized;
        Vector2 velocity=player.linearVelocity;
        float radialSpeed=Vector2.Dot(velocity,radial);
        Vector2 tangent=velocity-radial*radialSpeed;
        float speed=velocity.magnitude;
        float speedBoost=1f+Mathf.Clamp(speed/6.5f,0f,swingBoost);
        float sideEntry=1f-Mathf.Clamp01(Mathf.Abs(Vector2.Dot(speed>.01f?velocity/speed:Vector2.zero,radial)));
        player.linearVelocity=tangent*speedBoost*(1f+sideEntry*.35f);
    }

    private void StepRopeLength(float dt)
    {
        if(!Attached || swingJoint==null) return;
        if(!Input.GetKey(KeyCode.W)) return;
        attachRopeLength=Mathf.Max(minRopeLength,attachRopeLength-reelSpeed*dt);
        swingJoint.distance=attachRopeLength;
    }

    public void ReleaseSwing()
    {
        activeAnchor=null;
        if(swingJoint!=null)
        {
            Destroy(swingJoint);
            swingJoint=null;
            Play(releaseSound,.18f);
        }
        rope.enabled=false;
    }

    public void CollectGem(GemPickup gem)
    {
        if(gem.collected || State!=Mode.Flying) return;
        gem.collected=true;
        gem.visual.gameObject.SetActive(false);
        gem.pickupCollider.enabled=false;
        Score++;
        flash=.08f;
        Play(gemSound,.24f);
    }

    public void Crash(string reason)
    {
        if(State!=Mode.Flying) return;
        State=Mode.Crashed;crashAge=0;
        ReleaseSwing();
        player.simulated=false;
        Best=Mathf.Max(Best,Score);
        PlayerPrefs.SetInt("TwinSwingBest",Best);PlayerPrefs.Save();
        flash=.22f;shake=.14f;
        Play(crashSound,.35f);
    }

    public void SetPaused(bool paused)
    {
        if(paused && (State==Mode.Flying || State==Mode.Countdown))
        {
            beforePause=State;State=Mode.Paused;player.simulated=false;
        }
        else if(!paused && State==Mode.Paused)
        {
            State=beforePause;player.simulated=State==Mode.Flying;
        }
    }

    private void Step(float dt)
    {
        if(State==Mode.Ready)
        {
            player.AddForce(Vector2.up*(-player.position.y*5-player.linearVelocity.y*2)*player.mass);
            return;
        }
        if(State==Mode.Countdown)
        {
            countdown-=dt;
            if(countdown<=0)
            {
                State=Mode.Flying;player.simulated=true;
                contextHint="SPACE NEAR A GLOWING NODE TO ATTACH";hintTime=4f;
            }
            return;
        }
        if(State!=Mode.Flying) return;
        runTime+=dt;
        hintTime=Mathf.Max(0,hintTime-dt);
        StepRopeLength(dt);

        if(!Attached)
        {
            float scroll=scrollSpeed*(runTime<5f?.5f:1f)*dt;
            foreach(var anchor in anchors)
                anchor.body.MovePosition(anchor.body.position+Vector2.left*scroll);
            foreach(var gem in gems)
            {
                if(gem.collected) continue;
                gem.transform.position=gem.transform.position+Vector3.left*scroll;
            }
            RecycleAnchors();
            RecycleGems();
            if(player.position.x<sharedCamera.transform.position.x-9f) Crash("Fell behind");
        }

        if(hintTime<=0 && runTime>2.5f && !Attached)
        {
            contextHint="SPACE NEAR A NODE TO ATTACH";
            hintTime=3f;
        }
    }

    private void RecycleAnchors()
    {
        foreach(var anchor in anchors)
        {
            if(anchor.body.position.x<-8)
            {
                float furthest=0;
                foreach(var other in anchors) furthest=Mathf.Max(furthest,other.body.position.x);
                PlaceAnchor(anchor,furthest+anchorSpacing);
            }
        }
    }

    private void RecycleGems()
    {
        foreach(var gem in gems)
        {
            if(gem.transform.position.x<-8)
            {
                float furthest=0;
                foreach(var other in gems) furthest=Mathf.Max(furthest,other.transform.position.x);
                PlaceGem(gem,furthest+gemSpacing);
            }
        }
    }

    private void LayoutCourse(float startX)
    {
        float anchorX=startX+2.4f;
        for(int i=0;i<anchors.Count;i++,anchorX+=anchorSpacing) PlaceAnchor(anchors[i],anchorX);
        float gemX=startX+2f;
        for(int i=0;i<gems.Count;i++,gemX+=gemSpacing) PlaceGem(gems[i],gemX);
    }

    private void PlaceAnchor(Anchor anchor,float x)
    {
        float y=nextAnchor switch
        {
            0=>2.4f,
            1=>1.1f,
            2=>2.8f,
            3=>0.4f,
            _=>Mathf.Lerp(-1.2f,3.8f,(float)rng.NextDouble())
        };
        anchor.body.position=new Vector2(x,y);
        anchor.body.transform.position=new Vector3(x,y,0);
        nextAnchor++;
    }

    private void PlaceGem(GemPickup gem,float x)
    {
        gem.collected=false;
        gem.pickupCollider.enabled=true;
        gem.visual.gameObject.SetActive(true);
        float y=Mathf.Lerp(-2.8f,2.8f,(float)rng.NextDouble());
        gem.transform.position=new Vector3(x,y,0);
        gem.index=nextGem++;
    }

    private Anchor CreateAnchor(int index)
    {
        var root=new GameObject("Anchor "+index);root.transform.SetParent(transform);
        var body=root.AddComponent<Rigidbody2D>();
        body.bodyType=RigidbodyType2D.Kinematic;
        root.AddComponent<CircleCollider2D>().radius=.16f;
        var ring=Draw("Anchor ring",disc,Vector2.zero,new Vector2(.19f,.19f),NodeYellow,3,root.transform);
        Draw("Anchor core",disc,Vector2.zero,new Vector2(.08f,.08f),PlayerRed,4,root.transform);
        return new Anchor{body=body,ring=ring};
    }

    private GemPickup CreateGem(int index)
    {
        var root=new GameObject("Gem "+index);root.transform.SetParent(transform);
        var collider=root.AddComponent<CircleCollider2D>();
        collider.radius=.14f;collider.isTrigger=true;
        var visual=Draw("Gem ink",disc,Vector2.zero,new Vector2(.16f,.16f),GemGold,5,root.transform);
        var gem=root.AddComponent<GemPickup>();
        gem.game=this;gem.visual=visual;gem.pickupCollider=collider;
        return gem;
    }

    private void LateUpdate()
    {
        if(!built) return;
        if(Attached && activeAnchor!=null)
        {
            Vector3 a=player.transform.position,b=activeAnchor.body.transform.position;
            for(int n=0;n<rope.positionCount;n++)
            {
                float t=n/(float)(rope.positionCount-1);
                rope.SetPosition(n,Vector3.Lerp(a,b,t));
            }
        }
        float distance=attachRange;
        Anchor highlight=Attached?activeAnchor:FindNearestAnchor(out distance);
        if(Attached && activeAnchor!=null)
            distance=Vector2.Distance(player.position,activeAnchor.body.position);
        foreach(var anchor in anchors)
        {
            bool inRange=!Attached && anchor==highlight && distance<=attachRange;
            anchor.ring.localScale=Vector3.one*(inRange?1.35f:1f);
        }
        cameraCenter=Vector3.Lerp(cameraCenter,CameraTarget(),1-Mathf.Exp(-6*Time.unscaledDeltaTime));
        sharedCamera.transform.position=cameraCenter+
            new Vector3(Mathf.Sin(Time.unscaledTime*67),Mathf.Cos(Time.unscaledTime*73),0)*shake;
    }

    private Anchor FindNearestAnchor(out float distance)
    {
        Anchor best=null;
        distance=attachRange;
        foreach(var anchor in anchors)
        {
            float d=Vector2.Distance(player.position,anchor.body.position);
            if(d<=distance) { best=anchor;distance=d; }
        }
        return best;
    }

    private Vector3 CameraTarget()
    {
        if(Attached && activeAnchor!=null)
            return new Vector3(activeAnchor.body.position.x,0,-10);
        float halfWidth=sharedCamera.orthographicSize*sharedCamera.aspect;
        float lead=Mathf.Clamp(halfWidth-2.2f,0,2.8f);
        return new Vector3(player.position.x+lead,0,-10);
    }

    private void OnGUI()
    {
        float scale=Screen.height/900f;
        float guiWidth=Screen.width/scale;
        GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(scale,scale,1));
        if(labelStyle==null) labelStyle=new GUIStyle(GUI.skin.label){fontSize=42,alignment=TextAnchor.MiddleCenter};
        if(hintStyle==null) hintStyle=new GUIStyle(GUI.skin.label){fontSize=21,alignment=TextAnchor.MiddleCenter};
        if(countStyle==null) countStyle=new GUIStyle(GUI.skin.label){fontSize=112,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
        float center=guiWidth*.5f;
        FloatingText(new Rect(center-70,20,140,65),Score.ToString("00"),labelStyle,new Color(.85f,.85f,.85f));
        FloatingText(new Rect(center-90,78,180,32),"GEMS",hintStyle,Muted);

        if(State==Mode.Ready)
        {
            SmallHint(0,760,guiWidth,"SPACE · ATTACH / RELEASE · HOLD W TO REEL SHORTER",1);
            SmallHint(0,792,guiWidth,"SWING THROUGH GEMS · TAB · CO-OP FLIGHT",.82f);
        }
        else if(State==Mode.Flying && hintTime>0)
            SmallHint(0,836,guiWidth,contextHint,Mathf.Min(1,hintTime));

        if(State==Mode.Ready || State==Mode.Crashed || State==Mode.Paused)
        {
            string action=State==Mode.Ready?"SPACE · BEGIN":State==Mode.Crashed?"SPACE · RETRY":"SPACE · RESUME";
            hintStyle.normal.textColor=HudPaper;
            if(GUI.Button(new Rect(center-160,830,320,48),action,hintStyle)) HandleSpace();
        }

        if(State==Mode.Flying || State==Mode.Ready)
        {
            FindNearestAnchor(out float nearestDistance);
            string ropeHint=Attached?"SPACE · RELEASE · HOLD W TO REEL":
                nearestDistance<=attachRange?$"SPACE · ATTACH ({nearestDistance:0.0}m)":"SPACE · FIND A NODE";
            SmallHint(center-170,120,340,ropeHint,.92f);
        }

        if(State==Mode.Countdown)
        {
            FloatingText(new Rect(center-260,111,520,43),"SWING",hintStyle,HudPaper);
            FloatingText(new Rect(center-125,145,250,157),Mathf.CeilToInt(countdown).ToString(),countStyle,HudPaper);
        }

        if(State==Mode.Paused)
            FloatingText(new Rect(center-160,140,320,48),"PAUSED",hintStyle,Muted);

        if(flash>0) Box(0,0,guiWidth,900,new Color(1,1,1,flash*.35f));
    }

    private void OnDestroy() => DisposeWorld();
    public void DisposeWorld()
    {
        foreach(var resource in resources)
            if(resource!=null)
            {
                if(Application.isPlaying) Destroy(resource);
                else DestroyImmediate(resource);
            }
        resources.Clear();
    }

    private static void Box(float x,float y,float w,float h,Color c)
    {
        GUI.color=c;GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture);GUI.color=Color.white;
    }

    private static void FloatingText(Rect rect,string text,GUIStyle style,Color color)
    {
        style.normal.textColor=new Color(0,0,0,color.a);
        GUI.Label(new Rect(rect.x+2,rect.y+2,rect.width,rect.height),text,style);
        style.normal.textColor=color;GUI.Label(rect,text,style);
    }

    private void SmallHint(float x,float y,float width,string text,float alpha)
    {
        FloatingText(new Rect(x,y,width,32),text,hintStyle,new Color(.7f,.7f,.7f,alpha));
    }

    private Transform Draw(string name,Mesh mesh,Vector2 position,Vector2 scale,Color color,int order,Transform parent)
    {
        var obj=new GameObject(name);obj.transform.SetParent(parent,false);
        obj.transform.localPosition=position;obj.transform.localScale=new Vector3(scale.x,scale.y,1);
        obj.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=ink;renderer.sortingOrder=order;
        var props=new MaterialPropertyBlock();props.SetColor("_Color",color);renderer.SetPropertyBlock(props);
        return obj.transform;
    }

    private LineRenderer Stroke(string name,Vector3[] positions,float width,Color color,int order,Transform parent)
    {
        var obj=new GameObject(name);obj.transform.SetParent(parent,false);
        var line=obj.AddComponent<LineRenderer>();line.sharedMaterial=ink;line.useWorldSpace=true;
        line.positionCount=positions.Length;line.SetPositions(positions);
        line.startWidth=line.endWidth=width;line.startColor=line.endColor=color;line.sortingOrder=order;
        line.numCapVertices=3;return line;
    }

    private static Mesh DiscMesh()
    {
        const int count=32;
        var vertices=new Vector3[count+1];var triangles=new int[count*3];var colors=new Color[count+1];
        colors[0]=Color.white;
        for(int n=0;n<count;n++)
        {
            float a=n/(float)count*Mathf.PI*2;
            vertices[n+1]=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
            colors[n+1]=Color.white;
            triangles[n*3]=0;triangles[n*3+1]=n+1;triangles[n*3+2]=(n+1)%count+1;
        }
        var mesh=new Mesh{name="Unit disc",vertices=vertices,triangles=triangles,colors=colors};
        mesh.RecalculateBounds();return mesh;
    }

    private AudioClip Tone(float from,float to,float duration)
    {
        const int rate=22050;
        var samples=new float[(int)(rate*duration)];float phase=0;
        for(int n=0;n<samples.Length;n++)
        {
            float t=n/(float)samples.Length;
            phase+=Mathf.Lerp(from,to,t)*2*Mathf.PI/rate;
            samples[n]=Mathf.Sin(phase)*Mathf.Sin(t*Mathf.PI)*Mathf.Exp(-t*3)*.2f;
        }
        var clip=AudioClip.Create("Swing cue",samples.Length,1,rate,false);
        clip.SetData(samples,0);resources.Add(clip);return clip;
    }

    private void Play(AudioClip clip,float volume)
    {
        if(Application.isPlaying && audioSource!=null && clip!=null) audioSource.PlayOneShot(clip,volume);
    }

    private void SwitchToCoopFlight()
    {
        DisposeWorld();built=false;
        enabled=false;
        var flightObject=new GameObject("Twinline co-op");
        var flight=flightObject.AddComponent<TwinFlightGame>();
        flight.singlePlayer=false;
        Destroy(gameObject);
    }
}
