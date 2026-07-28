using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityAllocator = Unity.Collections.Allocator;

namespace OdinInterop
{
    [OdinExport]
    internal static unsafe partial class UnityScene
    {
        // scenes api

        private static int GetBuildIndex(Scene scene) => scene.buildIndex;
        private static bool IsDirty(Scene scene) => scene.isDirty;
        private static bool IsLoaded(Scene scene) => scene.isLoaded;
        private static bool IsValid(Scene scene) => scene.IsValid();
        private static String8 GetName(Scene scene, Allocator allocator) => new String8(scene.name, allocator);
        private static String8 GetPath(Scene scene, Allocator allocator) => new String8(scene.path, allocator);
        private static int GetRootGameObjectsCount(Scene scene) => scene.rootCount;
        private static Slice<ObjectHandle<GameObject>> GetRootGameObjects(Scene scene, Allocator allocator)
        {
            var roots = scene.GetRootGameObjects();
            var slice = new Slice<ObjectHandle<GameObject>>(roots.Length, allocator);
            for (var i = 0; i < roots.Length; i++)
                slice.ptr[i] = roots[i];
            return slice;
        }

        // scene manager api

        private static int GetLoadedScenesCount() => SceneManager.loadedSceneCount;
        private static int GetScenesCount() => SceneManager.sceneCount;
        private static int GetScenesCountInBuildSettings() => SceneManager.sceneCountInBuildSettings;
        private static int Create(String8 name, LocalPhysicsMode physicsMode = default) => SceneManager.CreateScene(name.ToString(), new CreateSceneParameters(physicsMode)).handle;
        private static int GetActive() => SceneManager.GetActiveScene().handle;
        private static int GetAtIndex(int index) => SceneManager.GetSceneAt(index).handle;
        private static int GetByBuildIndex(int buildIndex) => SceneManager.GetSceneByBuildIndex(buildIndex).handle;
        private static int GetByName(String8 name) => SceneManager.GetSceneByName(name.ToString()).handle;
        private static int GetByPath(String8 path) => SceneManager.GetSceneByPath(path.ToString()).handle;
        private static void LoadByBuildIndex(int buildIndex, LoadSceneMode mode = default) => SceneManager.LoadScene(buildIndex, mode);
        private static void LoadByName(String8 name, LoadSceneMode mode = default) => SceneManager.LoadScene(name.ToString(), mode);
        private static void MergeScenes(Scene src, Scene dst) => SceneManager.MergeScenes(src, dst);
        private static void MoveGameObjectsTo(Slice<ObjectHandle<GameObject>> gameObjects, Scene scene) =>
            SceneManager.MoveGameObjectsToScene(NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<EntityId>(gameObjects.ptr, gameObjects.len.ToInt32(), UnityAllocator.None), scene);
        private static void MoveGameObjectTo(ObjectHandle<GameObject> gameObject, Scene scene) => SceneManager.MoveGameObjectToScene(gameObject.value, scene);
        private static bool SetActive(Scene scene) => SceneManager.SetActiveScene(scene);
        private static uint LoadAsyncByBuildIndex(int buildIndex, LoadSceneMode lsMode = default, LocalPhysicsMode phMode = default) =>
            BindingsHelper.RegisterAsyncOperation(SceneManager.LoadSceneAsync(buildIndex, new LoadSceneParameters(lsMode, phMode)));
        private static uint LoadAsyncByName(String8 name, LoadSceneMode lsMode = default, LocalPhysicsMode phMode = default) =>
            BindingsHelper.RegisterAsyncOperation(SceneManager.LoadSceneAsync(name.ToString(), new LoadSceneParameters(lsMode, phMode)));
        private static uint UnloadAsyncByHandle(Scene scene, bool unloadEmbedded) =>
            BindingsHelper.RegisterAsyncOperation(SceneManager.UnloadSceneAsync(scene, unloadEmbedded ? UnloadSceneOptions.UnloadAllEmbeddedSceneObjects : UnloadSceneOptions.None));
        private static uint UnloadAsyncByBuildIndex(int buildIndex, bool unloadEmbedded) =>
            BindingsHelper.RegisterAsyncOperation(SceneManager.UnloadSceneAsync(buildIndex, unloadEmbedded ? UnloadSceneOptions.UnloadAllEmbeddedSceneObjects : UnloadSceneOptions.None));
        private static uint UnloadAsyncByName(String8 name, bool unloadEmbedded) =>
            BindingsHelper.RegisterAsyncOperation(SceneManager.UnloadSceneAsync(name.ToString(), unloadEmbedded ? UnloadSceneOptions.UnloadAllEmbeddedSceneObjects : UnloadSceneOptions.None));
        private static float GetAsyncOperationProgress(uint asyncOpId) => BindingsHelper.GetAsyncOperationProgress(asyncOpId);
        private static bool IsAsyncOperationDone(uint asyncOpId) => BindingsHelper.IsAsyncOperationDone(asyncOpId);
        private static int GetAsyncOperationPriority(uint asyncOpId) => BindingsHelper.GetAsyncOperationPriority(asyncOpId);
        private static void SetAsyncOperationPriority(uint asyncOpId, int priority) => BindingsHelper.SetAsyncOperationPriority(asyncOpId, priority);
        private static bool DoesAsyncOperationAllowActivation(uint asyncOpId) => BindingsHelper.DoesAsyncSceneOperationAllowActivation(asyncOpId);
        private static void SetAsyncOperationAllowActivation(uint asyncOpId, bool allow) => BindingsHelper.SetAsyncSceneOperationAllowActivation(asyncOpId, allow);
    }
}