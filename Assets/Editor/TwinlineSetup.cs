using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class TwinlineSetup
{
    private const string ScenePath="Assets/Scenes/Flight.unity";
    [InitializeOnLoadMethod]
    private static void Setup()
    {
        EditorApplication.playModeStateChanged-=OnPlayModeChanged;
        EditorApplication.playModeStateChanged+=OnPlayModeChanged;
        EditorApplication.delayCall+=PrepareAndBuild;
    }
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall+=PrepareAndBuild;
    }
    private static void PrepareAndBuild()
    {
        string request=Path.GetFullPath(Path.Combine(Application.dataPath,"../validate-build.request"));
        if(EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // Structural changes need a fresh generated world, not a stale live session.
            if(File.Exists(request)) EditorApplication.isPlaying=false;
            return;
        }
        if(!File.Exists(ScenePath)) CreateScene();
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(!scene.isDirty && string.IsNullOrEmpty(scene.path)) EditorSceneManager.OpenScene(ScenePath);
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        if(!File.Exists(request)) return;
        File.Delete(request);
        try
        {
            Validate();BuildMac();
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../Validation.txt")),"Physics validation and Mac build passed. See the Unity Editor log for detailed results.");
        }
        catch(Exception error)
        {
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../Validation.txt")),error.ToString());
            Debug.LogException(error);
        }
    }
    [MenuItem("Twinline/Create Flight Scene")]
    public static void CreateScene()
    {
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("TWINLINE — press Play").AddComponent<TwinFlightGame>();
        EditorSceneManager.SaveScene(scene,ScenePath);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        PlayerSettings.productName="Twinline";
        PlayerSettings.companyName="LocalPrototypes";
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
        PlayerSettings.defaultIsNativeResolution=false;
        PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;
        PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=false;
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone,"com.local.twinline");
        AssetDatabase.SaveAssets();
        Debug.Log("TWINLINE_SCENE_READY: local cooperative flight with one shared camera.");
    }
    [MenuItem("Twinline/Validate Flight Mechanics")]
    public static void Validate()
    {
        if(EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var prior=Physics2D.simulationMode;
        try
        {
            EditorSceneManager.OpenScene(ScenePath);
            var game=UnityEngine.Object.FindFirstObjectByType<TwinFlightGame>();
            game.ValidationMode=true;game.BuildWorld();game.ResetRun();game.StartRun(true);
            Physics2D.simulationMode=SimulationMode2D.Script;Physics2D.SyncTransforms();
            Check(game.GetComponentsInChildren<Camera>().Length==1 && game.sharedCamera.rect==new Rect(0,0,1,1),"The game must use one full-screen camera");
            Check(game.birds[0].position.x < -1 && game.birds[1].position.x > 1,"Player lanes overlapped");
            Check((Vector2)game.birds[0].transform.position==game.birds[0].position && (Vector2)game.birds[1].transform.position==game.birds[1].position,"Initial rendered and physics poses differ");
            game.QueueFlap(0);game.QueueFlap(1);game.Step(.01f);Physics2D.Simulate(.01f);
            Check(game.birds[0].linearVelocity.y>0 && game.birds[1].linearVelocity.y>0,$"Both independent flap keys must lift: {game.birds[0].linearVelocity}, {game.birds[1].linearVelocity}");
            // The ready screen uses the same rope physics and visibly responds before starting.
            game.ResetRun();Physics2D.SyncTransforms();
            game.QueueFlap(0);game.SetHeld(0,true);
            float previewStart=Vector2.Distance(game.birds[0].position,game.birds[1].position);
            for(int n=0;n<60;n++) { game.Step(.01f);Physics2D.Simulate(.01f); }
            float previewPull=previewStart-Vector2.Distance(game.birds[0].position,game.birds[1].position);
            Check(previewPull>.85f,$"Rope must visibly pull within 0.6 seconds, moved only {previewPull}");
            Check(game.State==TwinFlightGame.Mode.Ready && game.Score==0 && game.Faults==0,"Preview started the run or hazards");
            float shortHoldDistance=Vector2.Distance(game.birds[0].position,game.birds[1].position);
            for(int n=0;n<90;n++) { game.Step(.01f);Physics2D.Simulate(.01f); }
            float longHoldDistance=Vector2.Distance(game.birds[0].position,game.birds[1].position);
            Check(longHoldDistance<shortHoldDistance-.35f,$"Holding the same key longer must visibly tighten the pair: {shortHoldDistance} -> {longHoldDistance}");
            for(int n=0;n<50;n++) { game.Step(.01f);Physics2D.Simulate(.01f); }
            Check(game.tether.distance>=game.reeledLength-.001f && Vector2.Distance(game.birds[0].position,game.birds[1].position)>.47f,"Sustained hold compressed the circles into each other");
            Check(game.ReelUses==1,"Progressive tightening required another press");
            Debug.Log($"TWINLINE_PROGRESSIVE_HOLD_PASSED: same continuous key, distance {shortHoldDistance:0.000} at 0.6s -> {longHoldDistance:0.000} at 1.5s; minimum spacing and release checked.");
            game.SetHeld(0,false);
            for(int n=0;n<140;n++) { game.Step(.01f);Physics2D.Simulate(.01f); }
            Check(Vector2.Distance(game.birds[0].position,game.birds[1].position)>1.9f,"Preview release did not swing the pair apart");
            game.StartRun();
            Check(game.State==TwinFlightGame.Mode.Countdown && game.birds[0].position==new Vector2(-1.2f,0) && game.birds[1].position==new Vector2(1.2f,0),"Preview did not reset to a fair start");
            Check(!game.IsReeling(0) && game.tether.distance==game.ropeLength,"Preview leaked held state into the run");
            Debug.Log($"TWINLINE_PREVIEW_PASSED: {previewPull:0.000} units of visible pull within 0.6 seconds; release motion and clean start.");
            game.StartRun(true);game.QueueFlap(0);game.QueueFlap(1);game.Step(.01f);Physics2D.Simulate(.01f);
            var v0=game.birds[0].linearVelocity;var v1=game.birds[1].linearVelocity;
            game.ReverseGravity();
            Check(v0==game.birds[0].linearVelocity && v1==game.birds[1].linearVelocity,"Fault changed existing momentum");
            Check(game.birds[0].gravityScale<0 && game.birds[1].gravityScale<0,"Gravity must reverse together");
            // Stretch the tether with opposing velocities. It must pull the other bird.
            game.birds[0].gravityScale=game.birds[1].gravityScale=0;
            game.birds[0].position=new Vector2(-1.2f,1.1f);game.birds[1].position=new Vector2(1.2f,-1.1f);
            game.birds[0].linearVelocity=Vector2.up*2;game.birds[1].linearVelocity=Vector2.down*2;
            Physics2D.SyncTransforms();
            for(int n=0;n<30;n++) Physics2D.Simulate(.01f);
            Check(Vector2.Distance(game.birds[0].position,game.birds[1].position)<game.ropeLength+.03f,"Tension test stretched rope");
            Check(game.birds[1].linearVelocity.y>-1.5f,"Tether failed to pull the other bird");
            // An active reel must move the partner through physics, including sideways.
            game.ResetRun();game.StartRun(true);
            game.birds[0].position=new Vector2(-1.2f,.9f);game.birds[1].position=new Vector2(1.2f,-.9f);
            game.birds[0].gravityScale=game.birds[1].gravityScale=0;
            Physics2D.SyncTransforms();
            game.SetHeld(0,true);
            for(int n=0;n<10;n++) { game.Step(.01f);Physics2D.Simulate(.01f); }
            Check(Mathf.Abs(game.tether.distance-game.ropeLength)<.001f,"A short tap must not start the winch");
            for(int n=0;n<100;n++)
            {
                game.Step(.01f);Physics2D.Simulate(.01f);
                Check(Vector2.Distance(game.birds[0].position,game.birds[1].position)<game.tether.distance+.08f,"Reeling violated the live rope length");
            }
            float rescuedHeight=game.birds[1].position.y+.9f;
            float sideways=1.2f-game.birds[1].position.x;
            Check(rescuedHeight>.2f,$"Reeling did not lift the lower partner: {rescuedHeight}");
            Check(sideways>.15f,$"Reeling did not permit sideways movement: {sideways}");
            Check(game.ReelUses==1,"Holding must be one continuous reel, not repeated actions");
            float shortLength=game.tether.distance;
            v0=game.birds[0].linearVelocity;v1=game.birds[1].linearVelocity;
            game.SetHeld(0,false);
            Check(v0==game.birds[0].linearVelocity && v1==game.birds[1].linearVelocity,"Release overwrote momentum");
            game.ReverseGravity();
            Check(v0==game.birds[0].linearVelocity && v1==game.birds[1].linearVelocity,"Reversal after a reel overwrote momentum");
            game.Step(.01f);Physics2D.Simulate(.01f);
            Check(game.tether.distance>shortLength && game.tether.enabled,"Release must pay out without detaching");
            game.SetHeld(1,true);for(int n=0;n<25;n++) { game.Step(.01f);Physics2D.Simulate(.01f); }
            game.SetPaused(true);float pausedLength=game.tether.distance;
            game.Step(.5f);
            Check(!game.IsReeling(0) && !game.IsReeling(1) && game.tether.distance==pausedLength,"Pause retained a stuck reel or moved the rope");
            game.SetPaused(false);
            Debug.Log($"TWINLINE_REEL_PASSED: rescued partner by {rescuedHeight:0.000}, sideways motion {sideways:0.000}; tap/hold distinction, live length limit, continuous hold, momentum on release/reversal, and pause input cleanup.");
            // Sustained two-player input reaches the minimum safely and remains reversible.
            game.ResetRun();game.StartRun(true);
            game.birds[0].gravityScale=game.birds[1].gravityScale=0;
            game.SetHeld(0,true);game.SetHeld(1,true);
            for(int n=0;n<220;n++)
            {
                game.Step(.01f);Physics2D.Simulate(.01f);
                Check(Vector2.Distance(game.birds[0].position,game.birds[1].position)<game.tether.distance+.08f,"Two-player winch violated rope limit");
            }
            Check(Mathf.Abs(game.tether.distance-game.reeledLength)<.001f && game.ReelUses==2,"Two held keys did not settle at the minimum length");
            v0=game.birds[0].linearVelocity;v1=game.birds[1].linearVelocity;
            game.ReverseGravity();
            Check(v0==game.birds[0].linearVelocity && v1==game.birds[1].linearVelocity,"Gravity fault during two-player reeling changed velocity");
            game.birds[0].gravityScale=game.birds[1].gravityScale=0;
            game.SetHeld(0,false);game.SetHeld(1,false);
            for(int n=0;n<150;n++) { game.Step(.01f);Physics2D.Simulate(.01f); }
            Check(Mathf.Abs(game.tether.distance-game.ropeLength)<.001f,"Full release did not restore rope length");
            Check(Mathf.Abs(game.birds[0].position.x-game.birds[1].position.x)>2.05f,"Released partners did not separate again");
            Debug.Log("TWINLINE_TWO_PLAYER_REEL_PASSED: minimum length, sustained hold, reversal under tension, payout and separation.");
            FlyRoute(game,false);
            FlyRoute(game,true);
            float time=game.RunTime;var position=game.birds[0].position;
            game.SetPaused(true);game.Step(.2f);
            Check(game.RunTime==time && game.birds[0].position==position,"Pause advanced play");
            game.SetPaused(false);Check(game.State==TwinFlightGame.Mode.Flying,"Resume failed");
            game.Crash(1,"Validation");Check(game.State==TwinFlightGame.Mode.Crashed && !game.birds[0].simulated && !game.birds[1].simulated,"Impact must stop both players");
            game.ResetRun();Check(game.Score==0 && game.Faults==0 && game.GravitySign==1,"Retry did not reset shared state");
            Check(Mathf.Abs(game.tether.distance-game.ropeLength)<.001f && !game.IsReeling(0) && !game.IsReeling(1),"Retry retained the reeled rope or held inputs");
            Debug.Log("TWINLINE_VALIDATION_PASSED: passive flight and active cooperative reeling routes, rescue, momentum, live rope limits, paired score, pause, crash and clean restart.");
        }
        finally { Physics2D.simulationMode=prior;EditorSceneManager.OpenScene(ScenePath); }
    }
    private static void FlyRoute(TwinFlightGame game,bool useReel)
    {
        game.ResetRun();game.StartRun(true);Physics2D.SyncTransforms();
        int steps=0,lastReeledScore=-1,reelingPlayer=-1,warnings=0;
        float reelUntil=-1,maxLength=0,minLength=game.ropeLength,warningStarted=-1;
        float shortestWarning=float.MaxValue;
        bool checkedWarningPause=false;
        for(;steps<50000 && game.Score<36;steps++)
        {
            if(useReel && game.Score!=lastReeledScore && game.RunTime>.4f && game.IsFaultWindowSafe() && game.FaultWarning<0)
            {
                reelingPlayer=game.Score%2;game.SetHeld(reelingPlayer,true);game.QueueFlap(reelingPlayer);
                reelUntil=game.RunTime+.8f;lastReeledScore=game.Score;
            }
            if(reelingPlayer>=0 && game.RunTime>=reelUntil) { game.SetHeld(reelingPlayer,false);reelingPlayer=-1; }
            for(int i=0;i<2;i++)
            {
                if(i==reelingPlayer) continue; // A held key cannot also repeatedly tap.
                var body=game.birds[i];
                float lift=game.GravitySign;
                float predicted=body.position.y+body.linearVelocity.y*.2f-game.GravitySign*game.gravity*.02f;
                if((predicted-game.TargetHeight(i))*lift<-.05f && body.linearVelocity.y*lift<1.2f) game.QueueFlap(i);
            }
            float warningBefore=game.FaultWarning;int signBefore=game.GravitySign,faultsBefore=game.Faults;
            game.Step(.01f);Physics2D.Simulate(.01f);
            if(warningBefore<=0 && game.FaultWarning>0)
            {
                warnings++;warningStarted=game.RunTime;
                Check(game.FlipWarningLabel.Contains(signBefore==1?"↑":"↓"),"Warning indicated the wrong future gravity direction");
                if(!checkedWarningPause)
                {
                    float remaining=game.FaultWarning;
                    game.SetPaused(true);game.Step(.8f);
                    Check(game.FaultWarning==remaining && game.GravitySign==signBefore,"Warning ran down or flipped while paused");
                    game.SetPaused(false);checkedWarningPause=true;
                }
            }
            if(game.Faults>faultsBefore)
            {
                Check(warningStarted>=0,"Gravity flipped without a preceding warning");
                float duration=game.RunTime-warningStarted;shortestWarning=Mathf.Min(shortestWarning,duration);
                Check(duration>=TwinFlightGame.GravityWarningDuration-.02f,$"Warning was too short: {duration}");
                Check(game.FlipNotice>0,"Flip confirmation is missing");
                warningStarted=-1;
            }
            else Check(game.GravitySign==signBefore,"Gravity changed before its countdown finished");
            float distance=Vector2.Distance(game.birds[0].position,game.birds[1].position);
            maxLength=Mathf.Max(maxLength,distance);minLength=Mathf.Min(minLength,game.tether.distance);
            Check(game.State==TwinFlightGame.Mode.Flying,$"Route (reel={useReel}) crashed at tube {game.Score}, step {steps}");
            Check(distance<game.tether.distance+.08f,$"Live rope overstretched: {distance} > {game.tether.distance}");
            Check(!game.HasTubeContact(0) && !game.HasTubeContact(1),$"Route (reel={useReel}) tube contact at score {game.Score}, step {steps}: {game.birds[0].position}, {game.birds[1].position}");
            for(int i=0;i<2;i++)
            {
                Check(Mathf.Abs(game.birds[i].position.y)<4.7f,$"Route (reel={useReel}) left boundary at tube {game.Score}, step {steps}");
                Check(Mathf.Abs(game.birds[i].position.x)<3.5f,"Lane recovery let a player drift off screen");
                Check(game.birds[i].bodyType==RigidbodyType2D.Dynamic,"Body type changed");
            }
        }
        Check(game.Score>=36,"Tube recycling or paired scoring stalled");
        Check(game.Faults>=7,"Fault windows failed to fire");
        Check(warnings>=game.Faults && checkedWarningPause,"Every reversal needs its own pausable warning");
        Debug.Log($"TWINLINE_WARNING_PASSED: reel={useReel}, {game.Faults} flips, {warnings} advance warnings, shortest {shortestWarning:0.000}s; direction and pause checked.");
        if(useReel) Check(game.ReelUses>=24 && minLength<2.5f,"Cooperative route did not exercise meaningful reeling");
        Debug.Log($"TWINLINE_ROUTE_PASSED: reel={useReel}, 36 tubes, {game.Faults} reversals, {game.ReelUses} reels, {steps} steps, max separation {maxLength:0.000}, shortest rope {minLength:0.000}.");
    }
    [MenuItem("Twinline/Build Mac App")]
    public static void BuildMac()
    {
        if(EditorApplication.isPlaying) return;
        string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Twinline.app"));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=path,target=BuildTarget.StandaloneOSX,options=BuildOptions.None});
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Twinline build failed: "+report.summary.result);
        Debug.Log("TWINLINE_MAC_BUILD_PASSED: "+path);
    }
    private static void Check(bool condition,string message) { if(!condition) throw new Exception(message); }
}
