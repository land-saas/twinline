using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Local flight prototype: solo tap-only or two-player co-op with a physical tether.</summary>
public sealed class TwinFlightGame : MonoBehaviour
{
    public enum Mode { Ready, Countdown, Flying, Crashed, Paused }
    public Mode State { get; private set; }
    public int Score { get; private set; }
    public int Best { get; private set; }
    public int Faults { get; private set; }
    public int GravitySign { get; private set; } = 1;
    public bool ValidationMode { get; set; }
    public bool singlePlayer=true;
    private bool IsSolo => singlePlayer && !ValidationMode;
    public float RunTime { get; private set; }
    public float FaultWarning { get; private set; } = -1;
    public Rigidbody2D[] birds;
    public DistanceJoint2D tether;
    public Camera sharedCamera;
    public LineRenderer rope;
    public float ropeLength=2.8f;
    public float gravity=9.4f;
    public float flapImpulse=4.2f;
    public float catchUpLift=3.2f;
    public float maxLiftSpeed=5.8f;
    public float flapCooldown=.14f;
    public float inputBuffer=.12f;
    public float baseSpeed=2.55f;
    public float pipeSpacing=10.4f;
    public float initialGap=3.65f;
    public float corridorHalfHeight=4.9f;
    public float reeledLength=.65f;
    public float reelSpeed=4.2f;
    public float closeReelSpeed=.65f;
    public float payoutSpeed=3.2f;
    public float holdThreshold=.18f;
    public float laneSpring=3.2f;
    public float laneDamping=.95f;
    public const float GravityWarningDuration=2f;
    public const float FirstFlipWarningDuration=3f;
    public const float WarningScrollFactor=.3f;
    public const float FlipSettleScrollFactor=.12f;
    public const float FirstFlipSettleDuration=2.8f;
    public const float FlipSettleDuration=1.2f;
    public const float FirstFlipGap=4.5f;
    public float FlipNotice { get; private set; }
    public bool GravityPractice { get; private set; }
    public bool InGravityTransition => FaultWarning>0 || FlipNotice>0;
    public string FlipWarningLabel => "GRAVITY "+(GravitySign==1?"↑":"↓")+" IN "+Mathf.CeilToInt(Mathf.Max(0,FaultWarning));
    public string TapDirectionLabel => IsSolo
        ?(GravitySign==1?"W WILL PUSH ↓":"W WILL PUSH ↑")
        :(GravitySign==1?"W / ↑ WILL PUSH ↓":"W / ↑ WILL PUSH ↑");
    public float Speed => Mathf.Min(3.45f,baseSpeed+Score*.025f);
    public float Tension => Mathf.InverseLerp(tether.distance-.5f,tether.distance,Vector2.Distance(birds[0].position,birds[1].position));
    public float ReelAmount => Mathf.InverseLerp(ropeLength,reeledLength,tether.distance);
    public bool FlipCoopReady { get; private set; } = true;
    public float FlipSettle { get; private set; }
    public float TubeScrollFactor => FaultWarning>0?WarningScrollFactor:FlipSettle>0?FlipSettleScrollFactor:1f;
    public bool IsReeling(int player) => held[player] && heldTime[player]>=holdThreshold && !InGravityTransition && FlipCoopReady;
    public int ReelUses { get; private set; }
    public Vector2[] Velocities => new[]{birds[0].linearVelocity,birds[1].linearVelocity};
    public const int Seed=4173;
    public static readonly Color Paper=Color.white;
    private static readonly Color Dark=Color.black;
    private static readonly Color Muted=new Color(.58f,.58f,.58f);
    private sealed class Gate { public Rigidbody2D body; public BoxCollider2D upper,lower; public Transform top,bottom; public bool scored; public float center,gap; public int number; }
    private readonly List<Gate> gates=new List<Gate>();
    private readonly List<UnityEngine.Object> resources=new List<UnityEngine.Object>();
    private readonly float[] cooldown=new float[2],buffer=new float[2],pulse=new float[2];
    private readonly float[] heldTime=new float[2];
    private readonly bool[] held=new bool[2],postFlipTap=new bool[2];
    private readonly LineRenderer[] birdRing=new LineRenderer[2],flapRing=new LineRenderer[2];
    private Vector3 cameraCenter;
    private System.Random rng;
    private Mesh disc,quad;
    private Material ink;
    private AudioSource audioSource;
    private AudioClip flapSound,passSound,faultSound,crashSound,warningSound;
    private float countdown,crashAge,flash,shake,lastCenter;
    private int nextGate,nextFaultScore;
    private GUIStyle labelStyle;
    private GUIStyle hintStyle;
    private GUIStyle warningStyle;
    private GUIStyle countStyle;
    private float guiWidth;
    private Mode beforePause;
    private bool built;
    private bool reelLessonSeen,releaseLessonSeen,postFlipLessonPending;
    private float hintTime;
    private string contextHint="";
    private float pluck;

    private void Awake() { BuildWorld();ResetRun(); }
    public void BuildWorld()
    {
        if(built) return;built=true;
        Time.fixedDeltaTime=.01f;
        Physics2D.gravity=new Vector2(0,-9.81f);
        ink=new Material(Resources.Load<Shader>("FlatInk"));resources.Add(ink);
        disc=DiscMesh();quad=QuadMesh();resources.Add(disc);resources.Add(quad);
        birds=new Rigidbody2D[2];
        for(int i=0;i<2;i++)
        {
            var bodyObject=new GameObject(i==0?"P1 — W":"P2 — Up Arrow");bodyObject.transform.SetParent(transform);
            birds[i]=bodyObject.AddComponent<Rigidbody2D>();
            birds[i].constraints=RigidbodyConstraints2D.FreezeRotation;
            birds[i].mass=1;birds[i].linearDamping=0;birds[i].interpolation=RigidbodyInterpolation2D.Interpolate;
            birds[i].collisionDetectionMode=CollisionDetectionMode2D.Continuous;
            bodyObject.AddComponent<CircleCollider2D>().radius=.205f;
            var collision=bodyObject.AddComponent<FlightBird>();collision.game=this;collision.player=i;
            Draw("Circle",disc,Vector2.zero,new Vector2(.235f,.235f),Paper,12,bodyObject.transform);
            // Fill distinguishes the partners without color or character decoration.
            if(i==1) Draw("Circle interior",disc,Vector2.zero,new Vector2(.17f,.17f),Dark,13,bodyObject.transform);
            birdRing[i]=Stroke("Active grip",new Vector3[49],.012f,Paper,14,transform);
            flapRing[i]=Stroke("Impulse feedback",new Vector3[49],.018f,Paper,15,transform);
        }
        sharedCamera=new GameObject("Shared camera").AddComponent<Camera>();
        sharedCamera.transform.SetParent(transform);sharedCamera.tag="MainCamera";
        sharedCamera.orthographic=true;sharedCamera.orthographicSize=corridorHalfHeight;
        sharedCamera.clearFlags=CameraClearFlags.SolidColor;sharedCamera.backgroundColor=Dark;
        sharedCamera.rect=new Rect(0,0,1,1);sharedCamera.cullingMask=1<<0;
        birds[0].gameObject.AddComponent<AudioListener>();
        tether=birds[1].gameObject.AddComponent<DistanceJoint2D>();tether.connectedBody=birds[0];tether.autoConfigureDistance=false;
        tether.autoConfigureConnectedAnchor=false;tether.anchor=tether.connectedAnchor=Vector2.zero;
        tether.distance=ropeLength;tether.maxDistanceOnly=true;tether.enableCollision=false;tether.breakForce=Mathf.Infinity;
        rope=Stroke("Shared physical tether",new Vector3[21],.024f,Paper,8,transform);
        // The world is shared. Soft lane forces leave room for real rope-driven arcs.
        for(int sign=-1;sign<=1;sign+=2)
        {
            var border=new GameObject("Boundary "+sign);border.transform.SetParent(transform);border.transform.position=new Vector2(0,sign*(corridorHalfHeight+.5f));
            var col=border.AddComponent<BoxCollider2D>();col.size=new Vector2(200,1);
        }
        for(int n=0;n<6;n++)
        {
            var root=new GameObject("Tube pair "+n);root.transform.SetParent(transform);
            var rb=root.AddComponent<Rigidbody2D>();rb.bodyType=RigidbodyType2D.Kinematic;
            var upper=new GameObject("Upper tube");upper.transform.SetParent(root.transform,false);
            var lower=new GameObject("Lower tube");lower.transform.SetParent(root.transform,false);
            var gate=new Gate{body=rb,upper=upper.AddComponent<BoxCollider2D>(),lower=lower.AddComponent<BoxCollider2D>()};
            gate.top=Draw("Upper tube ink",quad,Vector2.zero,Vector2.one,Paper,2,upper.transform);
            gate.bottom=Draw("Lower tube ink",quad,Vector2.zero,Vector2.one,Paper,2,lower.transform);
            gates.Add(gate);
        }
        audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=0;
        flapSound=Tone(230,360,.09f);passSound=Tone(640,880,.15f);faultSound=Tone(95,380,.36f);crashSound=Tone(150,42,.25f);warningSound=Tone(700,700,.06f);
    }

    public void ResetRun()
    {
        State=Mode.Ready;Score=Faults=0;GravitySign=1;RunTime=0;FaultWarning=-1;FlipNotice=0;nextFaultScore=3;
        GravityPractice=false;postFlipLessonPending=false;FlipCoopReady=true;FlipSettle=0;
        postFlipTap[0]=postFlipTap[1]=true;
        Best=PlayerPrefs.GetInt("TwinlineBest",0);rng=new System.Random(Seed);nextGate=0;lastCenter=0;
        cooldown[0]=cooldown[1]=buffer[0]=buffer[1]=pulse[0]=pulse[1]=0;
        flash=shake=crashAge=hintTime=pluck=0;contextHint="";ReelUses=0;
        ClearHeldInput();tether.distance=ropeLength;
        for(int i=0;i<2;i++)
        {
            birds[i].simulated=true;
            Vector2 spawn=IsSolo?Vector2.zero:new Vector2(i==0?-1.2f:1.2f,0);
            birds[i].transform.position=spawn;birds[i].position=spawn;
            birds[i].linearVelocity=Vector2.zero;birds[i].angularVelocity=0;
            birds[i].gravityScale=0;birds[i].simulated=true;
        }
        cameraCenter=SharedCameraTarget();sharedCamera.transform.position=cameraCenter;
        for(int i=0;i<gates.Count;i++) ConfigureGate(gates[i],7.5f+i*pipeSpacing);
        ApplySinglePlayerMode();
        Physics2D.SyncTransforms();
    }
    private void ApplySinglePlayerMode()
    {
        if(!built) return;
        rope.enabled=!IsSolo;
        tether.enabled=!IsSolo;
        birds[1].gameObject.SetActive(!IsSolo);
        birds[1].GetComponent<CircleCollider2D>().enabled=!IsSolo;
        birds[0].gameObject.name=IsSolo?"Player — W":"P1 — W";
    }
    public void StartRun(bool skipCountdown=false)
    {
        // The ready screen is a safe physics playground. Start from a fair flight pose.
        ResetRun();
        for(int i=0;i<2;i++) { birds[i].gravityScale=gravity/9.81f;birds[i].simulated=skipCountdown; }
        if(skipCountdown) { State=Mode.Flying;birds[0].simulated=birds[1].simulated=true; }
        else { State=Mode.Countdown;countdown=2f; }
    }
    private void Update()
    {
        if(!ValidationMode && Input.GetKeyDown(KeyCode.Escape))
        {
            if(State==Mode.Flying || State==Mode.Countdown) SetPaused(true);
            else if(State==Mode.Paused) SetPaused(false);
        }
        if(!ValidationMode && Input.GetKeyDown(KeyCode.Space)) PrimaryAction();
        if(!ValidationMode && State==Mode.Ready && Input.GetKeyDown(KeyCode.G)) ToggleGravityPractice();
        if(!ValidationMode && State==Mode.Ready && Input.GetKeyDown(KeyCode.Tab))
        {
            if(!singlePlayer) SwitchToSwingMode();
            else { singlePlayer=false;ResetRun(); }
        }
        if(!ValidationMode && (State==Mode.Ready || State==Mode.Flying || State==Mode.Countdown))
        {
            if(IsSolo)
            {
                if(Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) QueueFlap(0);
            }
            else
            {
                if(Input.GetKeyDown(KeyCode.W)) QueueFlap(0);
                if(Input.GetKeyDown(KeyCode.UpArrow)) QueueFlap(1);
                SetHeld(0,Input.GetKey(KeyCode.W));SetHeld(1,Input.GetKey(KeyCode.UpArrow));
            }
        }
        if(State==Mode.Crashed) crashAge+=Time.unscaledDeltaTime;
        flash=Mathf.MoveTowards(flash,0,Time.unscaledDeltaTime*2.8f);
        shake=Mathf.MoveTowards(shake,0,Time.unscaledDeltaTime*1.6f);
        for(int i=0;i<2;i++) pulse[i]=Mathf.Max(0,pulse[i]-Time.deltaTime);
    }
    private void FixedUpdate() => Step(Time.fixedDeltaTime);
    public void QueueFlap(int player) { if(player>=0 && player<2) buffer[player]=inputBuffer; }
    public void SetHeld(int player,bool down)
    {
        if(player<0 || player>=2 || (State!=Mode.Ready && State!=Mode.Flying && State!=Mode.Countdown)) return;
        held[player]=down;
    }
    private void ClearHeldInput()
    {
        for(int i=0;i<2;i++) { held[i]=false;heldTime[i]=0; }
    }
    private void Hint(string text,float seconds)
    {
        contextHint=text;hintTime=seconds;
    }
    public void BeginGravityPractice()
    {
        if(State!=Mode.Ready || GravityPractice) return;
        GravityPractice=true;GravitySign=1;FaultWarning=FirstFlipWarningDuration;FlipNotice=0;
        ClearHeldInput();
        for(int i=0;i<2;i++)
        {
            birds[i].gravityScale=gravity/9.81f;
            birds[i].linearVelocity=Vector2.zero;
            Vector2 spawn=new Vector2(i==0?-1.2f:1.2f,0);
            birds[i].transform.position=spawn;birds[i].position=spawn;
        }
        Play(warningSound,.3f);
    }
    public void EndGravityPractice()
    {
        if(!GravityPractice) return;
        GravityPractice=false;FaultWarning=-1;FlipNotice=0;GravitySign=1;
        ClearHeldInput();tether.distance=ropeLength;
        for(int i=0;i<2;i++)
        {
            birds[i].gravityScale=0;birds[i].linearVelocity=Vector2.zero;
            Vector2 spawn=new Vector2(i==0?-1.2f:1.2f,0);
            birds[i].transform.position=spawn;birds[i].position=spawn;
        }
    }
    private void ToggleGravityPractice()
    {
        if(GravityPractice) EndGravityPractice();
        else BeginGravityPractice();
    }
    private void StepBirds(float dt,bool applyTerminalFall)
    {
        for(int i=0;i<2;i++)
        {
            cooldown[i]=Mathf.Max(0,cooldown[i]-dt);
            if(buffer[i]>0 && cooldown[i]<=0) { Flap(i);buffer[i]=0;cooldown[i]=flapCooldown; }
            buffer[i]=Mathf.Max(0,buffer[i]-dt);
            if(applyTerminalFall)
            {
                float falling=-birds[i].linearVelocity.y*GravitySign;
                if(falling>8.5f) birds[i].AddForce(Vector2.up*GravitySign*(falling-8.5f),ForceMode2D.Impulse);
            }
        }
    }
    private void StepGravityWarning(float dt)
    {
        if(FaultWarning<=0) return;
        int previousBeat=Mathf.CeilToInt(FaultWarning);
        FaultWarning-=dt;
        if(FaultWarning<=0) { ReverseGravity();FaultWarning=-1; }
        else if(Mathf.CeilToInt(FaultWarning)<previousBeat) Play(warningSound,.3f);
    }
    private void StepRope(float dt)
    {
        if(IsSolo) return;
        int pulling=0;
        for(int i=0;i<2;i++)
        {
            float previous=heldTime[i];
            heldTime[i]=held[i]?heldTime[i]+dt:0;
            if(IsReeling(i))
            {
                pulling++;
                if(previous<holdThreshold)
                {
                    // Take up only slack immediately: never snap the bodies into a shorter pose.
                    tether.distance=Mathf.Min(tether.distance,Vector2.Distance(birds[0].position,birds[1].position)+.015f);
                    ReelUses++;pluck=.35f;
                    reelLessonSeen=true;
                    if(!releaseLessonSeen) { Hint("HOLD LONGER TO PULL CLOSER",2.5f);releaseLessonSeen=true; }
                }
            }
            // Releasing removes the winch load without changing either body's momentum.
            if(!held[i] && previous>=holdThreshold) { pluck=.6f;Play(passSound,.06f); }
            float home=i==0?-1.2f:1.2f;
            float restore=(home-birds[i].position.x)*laneSpring-birds[i].linearVelocity.x*laneDamping;
            birds[i].AddForce(Vector2.right*Mathf.Clamp(restore,-9,9)*birds[i].mass);
            // The holder braces against motion; gravity and partner forces still act on them.
            if(IsReeling(i)) birds[i].AddForce(-birds[i].linearVelocity*(.65f*birds[i].mass));
        }
        // Fast initial pull, then finer control as a sustained hold brings the circles close.
        float inwardSpeed=tether.distance>reeledLength+.65f?reelSpeed:closeReelSpeed;
        float speed=pulling>0?inwardSpeed*(pulling==2?1.35f:1):payoutSpeed;
        tether.distance=Mathf.MoveTowards(tether.distance,pulling>0?reeledLength:ropeLength,speed*dt);
        pluck=Mathf.MoveTowards(pluck,0,dt*2);
        if(!reelLessonSeen && RunTime>4 && Tension>.45f)
        {
            Hint("HOLD TO PULL YOUR PARTNER",3.5f);reelLessonSeen=true;
        }
    }
    public void Step(float dt)
    {
        if(State==Mode.Ready)
        {
            StepRope(dt);
            if(GravityPractice)
            {
                StepBirds(dt,true);
                for(int i=0;i<2;i++)
                {
                    float edge=Mathf.Abs(birds[i].position.y)-3.5f;
                    if(edge>0) birds[i].AddForce(Vector2.up*-(Mathf.Sign(birds[i].position.y)*edge*12*birds[i].mass));
                }
                StepGravityWarning(dt);
                FlipNotice=Mathf.Max(0,FlipNotice-dt);
            }
            else
            {
                StepBirds(dt,false);
                for(int i=0;i<2;i++)
                    // No scrolling or falling here: players can safely learn the real rope action.
                    birds[i].AddForce(Vector2.up*(-birds[i].position.y*5-birds[i].linearVelocity.y*2)*birds[i].mass);
            }
            return;
        }
        if(State==Mode.Countdown)
        {
            countdown-=dt;
            if(countdown<=0) { State=Mode.Flying;birds[0].simulated=birds[1].simulated=true; }
            return;
        }
        if(State!=Mode.Flying) return;
        RunTime+=dt;
        FlipNotice=Mathf.Max(0,FlipNotice-dt);
        FlipSettle=Mathf.Max(0,FlipSettle-dt);
        hintTime=Mathf.Max(0,hintTime-dt);
        if(postFlipLessonPending && FlipNotice<=0 && FaultWarning<0)
        {
            postFlipLessonPending=false;
            if(Faults==1) Hint(IsSolo?"TAP W AGAIN AFTER FLIP":"BOTH TAP AFTER FLIP · THEN ONE CAN HOLD",3.5f);
        }
        StepRope(dt);
        StepBirds(dt,true);
        foreach(var gate in gates)
        {
            float scroll=Speed*TubeScrollFactor;
            gate.body.MovePosition(gate.body.position+Vector2.left*(scroll*dt));
            float scoreLine=IsSolo?birds[0].position.x:Mathf.Min(birds[0].position.x,birds[1].position.x);
            if(!gate.scored && gate.body.position.x+.52f<scoreLine-.235f)
            {
                gate.scored=true;Score++;Play(passSound,.2f);
            }
        }
        foreach(var gate in gates)
        {
            if(gate.body.position.x<-10)
            {
                float furthest=0;foreach(var other in gates) furthest=Mathf.Max(furthest,other.body.position.x);
                ConfigureGate(gate,furthest+pipeSpacing);
            }
        }
        StepGravityWarning(dt);
        if(FaultWarning<0 && Score>=nextFaultScore && Faults<100 && TryBeginGravityWarning())
        {
            nextFaultScore=Score+3+rng.Next(0,3);
        }
    }
    public bool TryBeginGravityWarning()
    {
        if(State!=Mode.Flying || FaultWarning>0 || !IsFaultWindowSafe()) return false;
        bool firstFlip=Faults==0;
        FaultWarning=firstFlip?FirstFlipWarningDuration:GravityWarningDuration;
        if(firstFlip) WidenGatesForFirstFlip();
        ClearHeldInput();
        Play(warningSound,.3f);
        return true;
    }
    private void Flap(int player)
    {
        float liftDirection=GravitySign;
        float currentLift=birds[player].linearVelocity.y*liftDirection;
        float impulse=Mathf.Min(Mathf.Max(flapImpulse,catchUpLift-currentLift),Mathf.Max(0,maxLiftSpeed-currentLift));
        birds[player].AddForce(Vector2.up*(liftDirection*impulse*birds[player].mass),ForceMode2D.Impulse);
        if(IsSolo) FlipCoopReady=true;
        else
        {
            postFlipTap[player]=true;
            if(postFlipTap[0] && postFlipTap[1]) FlipCoopReady=true;
        }
        pulse[player]=.22f;Play(flapSound,.12f);
    }
    public void ReverseGravity()
    {
        GravitySign=-GravitySign;Faults++;
        for(int i=0;i<2;i++) birds[i].gravityScale=GravitySign*gravity/9.81f;
        // Do not flip velocities, swap controls, teleport, or detach the tether.
        ClearHeldInput();
        if(IsSolo) { FlipCoopReady=true;postFlipTap[0]=postFlipTap[1]=true; }
        else { FlipCoopReady=false;postFlipTap[0]=postFlipTap[1]=false; }
        FlipSettle=Faults==1?FirstFlipSettleDuration:FlipSettleDuration;
        postFlipLessonPending=State==Mode.Flying;
        flash=.16f;shake=.075f;FlipNotice=1.2f;Play(faultSound,.35f);
    }
    public bool IsFaultWindowSafe()
    {
        float rear=IsSolo?birds[0].position.x:Mathf.Min(birds[0].position.x,birds[1].position.x);
        float lead=IsSolo?birds[0].position.x:Mathf.Max(birds[0].position.x,birds[1].position.x);
        float warningLead=Faults==0?FirstFlipWarningDuration:GravityWarningDuration;
        foreach(var gate in gates)
        {
            float distance=gate.body.position.x-.52f-lead-.235f;
            if(distance>0 && distance/Speed<warningLead*WarningScrollFactor+1.1f) return false;
            if(gate.body.position.x+.52f>rear-.35f && gate.body.position.x-.52f<lead+.35f) return false;
        }
        return Mathf.Abs(birds[0].position.y)<3.5f && (IsSolo || Mathf.Abs(birds[1].position.y)<3.5f);
    }
    public void Crash(int player,string reason)
    {
        if(State!=Mode.Flying) return;
        State=Mode.Crashed;crashAge=0;
        ClearHeldInput();hintTime=0;
        birds[0].simulated=birds[1].simulated=false;buffer[0]=buffer[1]=0;
        Best=Mathf.Max(Best,Score);
        if(!ValidationMode) { PlayerPrefs.SetInt("TwinlineBest",Best);PlayerPrefs.Save(); }
        flash=.22f;shake=.14f;Play(crashSound,.35f);
    }
    public void SetPaused(bool paused)
    {
        if(paused && (State==Mode.Flying || State==Mode.Countdown))
        {
            beforePause=State;State=Mode.Paused;birds[0].simulated=birds[1].simulated=false;buffer[0]=buffer[1]=0;ClearHeldInput();
        }
        else if(!paused && State==Mode.Paused)
        {
            State=beforePause;birds[0].simulated=birds[1].simulated=State==Mode.Flying;
        }
    }
    private void OnApplicationFocus(bool focus) { if(!focus) { ClearHeldInput();SetPaused(true); } }
    private void ConfigureGate(Gate gate,float x)
    {
        float difficulty=Mathf.Clamp01(nextGate/24f);
        float gap=Mathf.Lerp(initialGap,3.4f,difficulty);
        // Bounded changes produce a learnable line, never arbitrary unreachable jumps.
        lastCenter=Mathf.Clamp(lastCenter+((float)rng.NextDouble()-.5f)*1.25f,-1.5f,1.5f);
        if(nextGate<2) lastCenter=0;
        if(nextGate==3) { lastCenter=0;gap=FirstFlipGap; }
        gate.number=nextGate++;gate.scored=false;gate.body.transform.position=new Vector3(x,0,0);gate.body.position=new Vector2(x,0);
        ReshapeGate(gate,lastCenter,gap);
    }
    private static void ReshapeGate(Gate gate,float center,float gap)
    {
        gate.center=center;gate.gap=gap;
        float topEdge=center+gap*.5f,bottomEdge=center-gap*.5f;
        float topHeight=7-topEdge,bottomHeight=bottomEdge+7;
        gate.upper.transform.localPosition=new Vector3(0,topEdge+topHeight*.5f,0);gate.upper.size=new Vector2(1.04f,topHeight);
        gate.lower.transform.localPosition=new Vector3(0,-7+bottomHeight*.5f,0);gate.lower.size=new Vector2(1.04f,bottomHeight);
        gate.top.localScale=new Vector3(1.04f,topHeight,1);gate.bottom.localScale=new Vector3(1.04f,bottomHeight,1);
    }
    private void WidenGatesForFirstFlip()
    {
        float lead=IsSolo?birds[0].position.x:Mathf.Max(birds[0].position.x,birds[1].position.x);
        int widened=0;
        foreach(var gate in gates)
        {
            if(gate.body.position.x>lead-1f && widened<2)
            {
                ReshapeGate(gate,0,FirstFlipGap);
                widened++;
            }
        }
    }
    public bool HasTubeContact(int player)
    {
        var circle=birds[player].GetComponent<CircleCollider2D>();
        foreach(var gate in gates)
            if(circle.Distance(gate.upper).distance<-.001f || circle.Distance(gate.lower).distance<-.001f) return true;
        return false;
    }
    public float TargetHeight(int player)
    {
        Gate target=null;float nearest=float.MaxValue;
        foreach(var gate in gates)
        {
            float d=gate.body.position.x+.8f-birds[player].position.x;
            if(d>0 && d<nearest) { target=gate;nearest=d; }
        }
        return target==null?0:target.center;
    }
    public void DisposeWorld()
    {
        foreach(var resource in resources) if(resource!=null)
        {
            if(Application.isPlaying) Destroy(resource);else DestroyImmediate(resource);
        }
        resources.Clear();
    }
    private void OnDestroy() => DisposeWorld();
    private Vector3 SharedCameraTarget()
    {
        // A single camera frames the pair with room ahead. Screen edges match the flight limits.
        float halfWidth=sharedCamera.orthographicSize*sharedCamera.aspect;
        if(IsSolo)
        {
            float lead=Mathf.Clamp(halfWidth-1.8f,0,2.5f);
            return new Vector3(birds[0].position.x+lead,0,-10);
        }
        float partnerSeparation=Mathf.Abs(birds[0].position.x-birds[1].position.x);
        float coopLead=Mathf.Clamp(halfWidth-partnerSeparation*.5f-1.2f,0,2.5f);
        return new Vector3((birds[0].position.x+birds[1].position.x)*.5f+coopLead,0,-10);
    }
    private void LateUpdate()
    {
        if(!built) return;
        if(IsSolo)
        {
            flapRing[0].enabled=pulse[0]>0;
            if(pulse[0]>0) Ring(flapRing[0],birds[0].transform.position,.28f+(.22f-pulse[0])*1.6f,Paper,pulse[0]*3);
            cameraCenter=Vector3.Lerp(cameraCenter,SharedCameraTarget(),1-Mathf.Exp(-6*Time.unscaledDeltaTime));
            sharedCamera.transform.position=cameraCenter+
                new Vector3(Mathf.Sin(Time.unscaledTime*67),Mathf.Cos(Time.unscaledTime*73),0)*shake;
            return;
        }
        Vector3 a=birds[0].transform.position,b=birds[1].transform.position;
        float distance=Vector2.Distance(a,b),slack=Mathf.Max(0,tether.distance-distance);
        Color tetherColor=Color.Lerp(Muted,Paper,Tension);
        rope.startColor=rope.endColor=tetherColor;
        rope.startWidth=rope.endWidth=Mathf.Lerp(.025f,.055f,Mathf.Max(Tension,ReelAmount));
        Vector3 normal=Vector3.Cross((b-a).normalized,Vector3.forward);
        for(int n=0;n<rope.positionCount;n++)
        {
            float t=n/(float)(rope.positionCount-1);
            float ripple=Mathf.Sin(t*Mathf.PI*3-Time.unscaledTime*25)*Mathf.Sin(t*Mathf.PI)*pluck*.17f;
            rope.SetPosition(n,Vector3.Lerp(a,b,t)+Vector3.down*(GravitySign*Mathf.Sin(t*Mathf.PI)*slack*.3f)+normal*ripple);
        }
        for(int i=0;i<2;i++)
        {
            birdRing[i].enabled=IsReeling(i);
            birdRing[i].startWidth=birdRing[i].endWidth=Mathf.Lerp(.012f,.025f,ReelAmount);
            if(IsReeling(i)) Ring(birdRing[i],birds[i].transform.position,.31f,Paper,.8f);
            flapRing[i].enabled=pulse[i]>0;
            if(pulse[i]>0) Ring(flapRing[i],birds[i].transform.position,.28f+(.22f-pulse[i])*1.6f,Paper,pulse[i]*3);
        }
        cameraCenter=Vector3.Lerp(cameraCenter,SharedCameraTarget(),1-Mathf.Exp(-6*Time.unscaledDeltaTime));
        sharedCamera.transform.position=cameraCenter+
            new Vector3(Mathf.Sin(Time.unscaledTime*67),Mathf.Cos(Time.unscaledTime*73),0)*shake;
    }
    private void PrimaryAction()
    {
        if(State==Mode.Ready) StartRun();
        else if(State==Mode.Crashed && crashAge>.2f) { ResetRun();StartRun(); }
        else if(State==Mode.Paused) SetPaused(false);
    }
    private void OnGUI()
    {
        float scale=Screen.height/900f;guiWidth=Screen.width/scale;
        GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(scale,scale,1));
        if(labelStyle==null) labelStyle=new GUIStyle(GUI.skin.label){fontSize=42,alignment=TextAnchor.MiddleCenter};
        if(hintStyle==null) hintStyle=new GUIStyle(GUI.skin.label){fontSize=21,alignment=TextAnchor.MiddleCenter};
        if(warningStyle==null) warningStyle=new GUIStyle(GUI.skin.label){fontSize=26,alignment=TextAnchor.MiddleCenter};
        if(countStyle==null) countStyle=new GUIStyle(GUI.skin.label){fontSize=112,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
        float center=guiWidth*.5f;
        FloatingText(new Rect(center-70,20,140,65),Score.ToString("00"),labelStyle,new Color(.85f,.85f,.85f));

        bool showKeys=State==Mode.Ready || State==Mode.Countdown || (State==Mode.Flying && RunTime<3);
        if(showKeys)
        {
            float alpha=State==Mode.Flying?Mathf.Clamp01(3-RunTime):1;
            SmallHint(guiWidth-185,28,170,"ESC · PAUSE",alpha);
            if(IsSolo)
            {
                Vector3 p=sharedCamera.WorldToScreenPoint(birds[0].transform.position);
                SmallHint(p.x/scale-70,(Screen.height-p.y)/scale+34,140,"W",alpha);
            }
            else for(int i=0;i<2;i++)
            {
                Vector3 p=sharedCamera.WorldToScreenPoint(birds[i].transform.position);
                SmallHint(p.x/scale-70,(Screen.height-p.y)/scale+34,140,i==0?"W":"↑",alpha);
            }
        }
        if(State==Mode.Ready && !ValidationMode)
            SmallHint(guiWidth-210,55,200,IsSolo?"TAB · CO-OP MODE":"TAB · SWING MODE",.7f);
        if(State==Mode.Ready)
        {
            if(GravityPractice)
            {
                if(FaultWarning>0)
                    DrawCountdown(FaultWarning,GravitySign==1?"GRAVITY WILL FLIP ↑":"GRAVITY WILL FLIP ↓",TapDirectionLabel);
                else if(FlipNotice>0)
                    FloatingText(new Rect(center-220,142,440,54),GravitySign==1?"GRAVITY ↓":"GRAVITY ↑",warningStyle,
                        new Color(1,1,1,Mathf.Min(1,FlipNotice*2)));
                else
                    SmallHint(0,775,guiWidth,IsSolo?"PRACTICE FLIP · TAP W · G · EXIT":"PRACTICE FLIP · TAP W / ↑ · G · EXIT",1);
            }
            else if(IsSolo)
            {
                SmallHint(0,775,guiWidth,"TAP W TO LIFT · HOLD DOES NOTHING",1);
                SmallHint(0,808,guiWidth,"G · TRY GRAVITY FLIP",.85f);
            }
            else
            {
                bool pulling=IsReeling(0)||IsReeling(1);
                string lesson=pulling?(ReelAmount>.98f?"RELEASE TO SWING APART":"KEEP HOLDING TO PULL CLOSER"):
                    "TAP TO LIFT · HOLD THE SAME KEY LONGER TO PULL CLOSER";
                SmallHint(0,775,guiWidth,lesson,1);
                SmallHint(0,808,guiWidth,"G · TRY GRAVITY FLIP",.85f);
            }
        }
        else if(State==Mode.Flying && hintTime>0 && FaultWarning<=0 && FlipNotice<=0)
            SmallHint(0,836,guiWidth,contextHint,Mathf.Min(1,hintTime));

        if(State==Mode.Ready || State==Mode.Crashed || State==Mode.Paused)
        {
            string action=State==Mode.Ready?"SPACE · BEGIN":State==Mode.Crashed?"SPACE · RETRY":"SPACE · RESUME";
            hintStyle.normal.textColor=Paper;
            if(GUI.Button(new Rect(center-160,830,320,48),action,hintStyle)) PrimaryAction();
            if(State==Mode.Paused && FaultWarning<=0)
                FloatingText(new Rect(center-160,140,320,48),"PAUSED",warningStyle,Muted);
        }
        if(State==Mode.Countdown) DrawCountdown(countdown,"READY",null);
        if((State==Mode.Flying || State==Mode.Paused) && FaultWarning>0)
            DrawCountdown(FaultWarning,GravitySign==1?"GRAVITY WILL FLIP ↑":"GRAVITY WILL FLIP ↓",TapDirectionLabel);
        else if((State==Mode.Flying || State==Mode.Paused) && FlipNotice>0)
            FloatingText(new Rect(center-220,142,440,54),GravitySign==1?"GRAVITY ↓":"GRAVITY ↑",warningStyle,
                new Color(1,1,1,Mathf.Min(1,FlipNotice*2)));
        DrawGravityArrow();
        if(flash>0) Box(0,0,guiWidth,900,new Color(1,1,1,flash*.35f));
    }
    private void DrawCountdown(float remaining,string caption,string nextAction)
    {
        float center=guiWidth*.5f;
        FloatingText(new Rect(center-260,111,520,43),caption,warningStyle,Paper);
        float beat=Mathf.Clamp01((Mathf.Repeat(remaining,1)-.75f)*4);
        countStyle.fontSize=Mathf.RoundToInt(Mathf.Lerp(112,124,beat));
        FloatingText(new Rect(center-125,145,250,157),Mathf.CeilToInt(remaining).ToString(),countStyle,Paper);
        if(nextAction!=null) SmallHint(center-200,308,400,nextAction,1);
    }
    private static void Box(float x,float y,float w,float h,Color c)
    {
        GUI.color=c;GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture);GUI.color=Color.white;
    }
    private static void FloatingText(Rect rect,string text,GUIStyle style,Color color)
    {
        // Text stays legible over a white tube without panels, dividers or border lines.
        style.normal.textColor=new Color(0,0,0,color.a);
        GUI.Label(new Rect(rect.x+2,rect.y+2,rect.width,rect.height),text,style);
        style.normal.textColor=color;GUI.Label(rect,text,style);
    }
    private void SmallHint(float x,float y,float width,string text,float alpha)
    {
        FloatingText(new Rect(x,y,width,32),text,hintStyle,new Color(.7f,.7f,.7f,alpha));
    }
    private bool ShowsGravityArrow()
    {
        return State==Mode.Flying || (State==Mode.Ready && GravityPractice) ||
            (State==Mode.Paused && beforePause==Mode.Flying);
    }
    private void DrawGravityArrow()
    {
        if(!ShowsGravityArrow()) return;
        string arrow=GravitySign==1?"↓":"↑";
        FloatingText(new Rect(guiWidth-72,88,48,48),arrow,labelStyle,new Color(.62f,.62f,.62f,.9f));
    }
    private Transform Draw(string name,Mesh mesh,Vector2 position,Vector2 scale,Color color,int order,Transform parent)
    {
        var obj=new GameObject(name);obj.transform.SetParent(parent,false);obj.transform.localPosition=position;obj.transform.localScale=new Vector3(scale.x,scale.y,1);
        obj.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=ink;renderer.sortingOrder=order;
        var props=new MaterialPropertyBlock();props.SetColor("_Color",color);renderer.SetPropertyBlock(props);return obj.transform;
    }
    private LineRenderer Stroke(string name,Vector3[] positions,float width,Color color,int order,Transform parent)
    {
        var obj=new GameObject(name);obj.transform.SetParent(parent,false);var line=obj.AddComponent<LineRenderer>();line.sharedMaterial=ink;line.useWorldSpace=false;
        line.positionCount=positions.Length;line.SetPositions(positions);line.startWidth=line.endWidth=width;line.startColor=line.endColor=color;line.sortingOrder=order;line.numCapVertices=3;return line;
    }
    private static void Ring(LineRenderer line,Vector3 center,float radius,Color c,float alpha)
    {
        c.a=alpha;line.startColor=line.endColor=c;
        for(int n=0;n<line.positionCount;n++) { float a=n/(float)(line.positionCount-1)*Mathf.PI*2;line.SetPosition(n,center+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius); }
    }
    private static Mesh QuadMesh()
    {
        var mesh=new Mesh{name="Unit rectangle"};mesh.vertices=new[]{new Vector3(-.5f,-.5f),new Vector3(.5f,-.5f),new Vector3(.5f,.5f),new Vector3(-.5f,.5f)};
        mesh.triangles=new[]{0,1,2,0,2,3};mesh.colors=new[]{Color.white,Color.white,Color.white,Color.white};mesh.RecalculateBounds();return mesh;
    }
    private static Mesh DiscMesh()
    {
        const int count=32;var vertices=new Vector3[count+1];var triangles=new int[count*3];var colors=new Color[count+1];colors[0]=Color.white;
        for(int n=0;n<count;n++) { float a=n/(float)count*Mathf.PI*2;vertices[n+1]=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);colors[n+1]=Color.white;triangles[n*3]=0;triangles[n*3+1]=n+1;triangles[n*3+2]=(n+1)%count+1; }
        var mesh=new Mesh{name="Unit disc",vertices=vertices,triangles=triangles,colors=colors};mesh.RecalculateBounds();return mesh;
    }
    private AudioClip Tone(float from,float to,float duration)
    {
        const int rate=22050;var samples=new float[(int)(rate*duration)];float phase=0;
        for(int n=0;n<samples.Length;n++) { float t=n/(float)samples.Length;phase+=Mathf.Lerp(from,to,t)*2*Mathf.PI/rate;samples[n]=Mathf.Sin(phase)*Mathf.Sin(t*Mathf.PI)*Mathf.Exp(-t*3)*.2f; }
        var clip=AudioClip.Create("Flight cue",samples.Length,1,rate,false);clip.SetData(samples,0);resources.Add(clip);return clip;
    }
    private void Play(AudioClip clip,float volume) { if(Application.isPlaying && audioSource!=null && clip!=null) audioSource.PlayOneShot(clip,volume); }
    private void SwitchToSwingMode()
    {
        DisposeWorld();built=false;
        enabled=false;
        var swingObject=new GameObject("Twinline swing");
        swingObject.AddComponent<TwinSwingGame>();
        Destroy(gameObject);
    }
}
