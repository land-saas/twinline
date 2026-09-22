#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;

// Editor-only playback of real game inputs, captured from the actual rendered frame.
public sealed class TwinlineVisualCheck : MonoBehaviour
{
    private TwinFlightGame game;
    private string directory;
    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.playModeStateChanged-=OnState;
        EditorApplication.playModeStateChanged+=OnState;
    }
    [MenuItem("Twinline/Capture Gameplay Checks")]
    public static void Begin()
    {
        if(EditorApplication.isPlaying) return;
        SessionState.SetBool("TwinlineVisualCheck",true);
        EditorApplication.isPlaying=true;
    }
    private static void OnState(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("TwinlineVisualCheck",false)) return;
        SessionState.SetBool("TwinlineVisualCheck",false);
        var game=Object.FindFirstObjectByType<TwinFlightGame>();
        game.ValidationMode=true;
        game.gameObject.AddComponent<TwinlineVisualCheck>();
    }
    private IEnumerator Start()
    {
        game=GetComponent<TwinFlightGame>();
        directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Twinline-checks"));
        Directory.CreateDirectory(directory);
        game.ResetRun();
        yield return new WaitForSeconds(.25f);
        yield return Capture("01-ready.png");
        game.QueueFlap(0);game.SetHeld(0,true);
        yield return new WaitForSeconds(.6f);
        yield return Capture("02-reeling.png");
        yield return new WaitForSeconds(.9f);
        yield return Capture("07-long-hold.png");
        game.SetHeld(0,false);
        yield return new WaitForSeconds(.6f);
        yield return Capture("03-released.png");
        game.StartRun();
        yield return new WaitForSeconds(.3f);
        yield return Capture("08-start-countdown.png");
        game.StartRun(true);
        if(!game.TryBeginGravityWarning()) throw new System.Exception("Visual check could not start warning");
        yield return new WaitForSeconds(.3f);
        yield return Capture("04-flip-in-two.png");
        yield return new WaitForSeconds(.85f);
        yield return Capture("05-flip-in-one.png");
        yield return new WaitForSeconds(1f);
        yield return Capture("06-flipped.png");
        if(game.Faults!=1 || game.GravitySign!=-1) throw new System.Exception("Visual check did not finish its announced flip");
        Debug.Log("TWINLINE_VISUAL_PASSED: one shared view, short hold, longer hold, release, large start countdown, two-second warning, one-second warning, reversed gravity; captured to "+directory);
        game.ResetRun();
        EditorApplication.isPlaying=false;
    }
    private void FixedUpdate()
    {
        if(game==null || game.State!=TwinFlightGame.Mode.Flying) return;
        for(int i=0;i<2;i++)
        {
            var body=game.birds[i];
            float predicted=body.position.y+body.linearVelocity.y*.2f-game.GravitySign*game.gravity*.02f;
            if((predicted-game.TargetHeight(i))*game.GravitySign<-.05f && body.linearVelocity.y*game.GravitySign<1.2f) game.QueueFlap(i);
        }
    }
    private IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();
        var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(directory,name),image.EncodeToPNG());
        Destroy(image);
    }
}

#endif
