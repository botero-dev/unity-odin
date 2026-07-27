using UnityEngine;

/// <summary>
/// Creates a DontDestroyOnLoad GameObject that forwards Unity lifecycle callbacks
/// to Odin via the Global import class. 
/// </summary>
[AddComponentMenu("")]
public class OdinGlobalHooks : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialise() => DontDestroyOnLoad(new GameObject("[OdinGlobalHooks]", typeof(OdinGlobalHooks)));

    private void Awake() => Global.Awake();
    private void Start() => Global.Start();
    private void FixedUpdate() => Global.FixedUpdate(Time.fixedDeltaTime);
    private void Update() => Global.Update(Time.deltaTime, Time.unscaledDeltaTime);
    private void LateUpdate() => Global.LateUpdate(Time.deltaTime, Time.unscaledDeltaTime);
    private void OnDestroy() => Global.Destroy();
}
