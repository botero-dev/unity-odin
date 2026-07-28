using UnityEngine;

namespace OdinInterop
{
    [OdinExport]
    internal static unsafe partial class UnityObject
    {
        private static bool IsOfType(ObjectHandle<Object> obj, String8 typeName)
        {
            if (!obj) return false;
            var type = BindingsHelper.GetCachedType(typeName);
            if (type == null) return false;
            return type.IsAssignableFrom(obj.value.GetType());
        }

        private static HideFlags GetHideFlags(ObjectHandle<Object> obj)
            => obj ? obj.value.hideFlags : HideFlags.None;

        private static void SetHideFlags(ObjectHandle<Object> obj, HideFlags flags)
        {
            if (obj) obj.value.hideFlags = flags;
        }

        private static String8 GetName(ObjectHandle<Object> obj, Allocator allocator)
            => obj ? new String8(obj.value.name, allocator) : default;

        private static void SetName(ObjectHandle<Object> obj, String8 name)
        {
            if (obj) obj.value.name = name.ToString();
        }

        private static void Destroy(ObjectHandle<Object> obj)
        {
            if (obj) Object.Destroy(obj.value);
        }

        private static void DestroyImmediate(ObjectHandle<Object> obj, bool allowDestroyingAssets = default)
        {
            if (obj) Object.DestroyImmediate(obj.value, allowDestroyingAssets);
        }

        private static void DontDestroyOnLoad(ObjectHandle<Object> obj)
        {
            if (obj) Object.DontDestroyOnLoad(obj.value);
        }

        private static ObjectHandle<Object> InstantiateWithoutTransform(
            ObjectHandle<Object> original,
            ObjectHandle<Transform> parent = default,
            bool instantiateInWorldSpace = default)
        {
            if (!original) return default;
            if (parent) return Object.Instantiate(original, parent, instantiateInWorldSpace);
            return Object.Instantiate(original);
        }

        private static ObjectHandle<Object> InstantiateWithTransform(
            ObjectHandle<Object> original,
            Vector3 position,
            Quaternion rotation,
            ObjectHandle<Transform> parent = default)
        {
            if (!original) return default;
            if (parent) return Object.Instantiate(original, position, rotation, parent);
            return Object.Instantiate(original, position, rotation);
        }
    }
}
