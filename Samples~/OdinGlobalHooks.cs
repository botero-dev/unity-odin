using UnityEngine;

/// <summary>
/// Creates a DontDestroyOnLoad GameObject that forwards Unity lifecycle callbacks
/// to Odin via the Global import class. 
[AddComponentMenu("")]
public class OdinGlobalHooks : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialise() => DontDestroyOnLoad(new GameObject("[OdinInteropHook]", typeof(OdinInteropHook)));

    private void Awake() => Global.OnAwake();
    private void Start() => Global.OnStart();
    private void FixedUpdate() => Global.OnFixedUpdate(Time.fixedDeltaTime);
    private void Update() => Global.OnUpdate(Time.deltaTime, Time.unscaledDeltaTime);
    private void LateUpdate() => Global.OnLateUpdate(Time.deltaTime, Time.unscaledDeltaTime);
    private void OnDestroy() => Global.OnDestroy();
}
