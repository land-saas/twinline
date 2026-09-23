using UnityEngine;

/// <summary>Scene entry point — spawns the active prototype on Play.</summary>
[DefaultExecutionOrder(-1000)]
public sealed class TwinlineRoot : MonoBehaviour
{
    public bool startWithCoopFlight;

    private void Awake()
    {
        foreach(var component in GetComponents<MonoBehaviour>())
        {
            if(component==this) continue;
            if(component is TwinSwingGame || component is TwinFlightGame)
                Destroy(component);
        }
        if(startWithCoopFlight)
        {
            var flight=gameObject.AddComponent<TwinFlightGame>();
            flight.singlePlayer=false;
        }
        else gameObject.AddComponent<TwinSwingGame>();
        Destroy(this);
    }
}
