using UnityEngine;

namespace OdinInterop
{
    [OdinExport]
    internal static unsafe partial class UnityTransform
    {
        // Transform API

        private static Vector3 GetPosition(ObjectHandle<Transform> transform) => transform ? transform.value.position : default;
        private static void SetPosition(ObjectHandle<Transform> transform, Vector3 position)
        {
            if (transform)
                transform.value.position = position;
        }
        private static Vector3 GetLocalPosition(ObjectHandle<Transform> transform) => transform ? transform.value.localPosition : default;
        private static void SetLocalPosition(ObjectHandle<Transform> transform, Vector3 localPosition)
        {
            if (transform)
                transform.value.localPosition = localPosition;
        }
        private static Quaternion GetRotation(ObjectHandle<Transform> transform) => transform ? transform.value.rotation : Quaternion.identity;
        private static void SetRotation(ObjectHandle<Transform> transform, Quaternion rotation)
        {
            if (transform)
                transform.value.rotation = rotation;
        }
        private static Quaternion GetLocalRotation(ObjectHandle<Transform> transform) => transform ? transform.value.localRotation : Quaternion.identity;
        private static void SetLocalRotation(ObjectHandle<Transform> transform, Quaternion localRotation)
        {
            if (transform)
                transform.value.localRotation = localRotation;
        }
        private static Vector3 GetEulerAngles(ObjectHandle<Transform> transform) => transform ? transform.value.eulerAngles : default;
        private static void SetEulerAngles(ObjectHandle<Transform> transform, Vector3 eulerAngles)
        {
            if (transform)
                transform.value.eulerAngles = eulerAngles;
        }
        private static Vector3 GetLocalEulerAngles(ObjectHandle<Transform> transform) => transform ? transform.value.localEulerAngles : default;
        private static void SetLocalEulerAngles(ObjectHandle<Transform> transform, Vector3 localEulerAngles)
        {
            if (transform)
                transform.value.localEulerAngles = localEulerAngles;
        }
        private static Vector3 GetLocalScale(ObjectHandle<Transform> transform) => transform ? transform.value.localScale : Vector3.one;
        private static void SetLocalScale(ObjectHandle<Transform> transform, Vector3 localScale)
        {
            if (transform)
                transform.value.localScale = localScale;
        }
        private static Vector3 GetLossyScale(ObjectHandle<Transform> transform) => transform ? transform.value.lossyScale : Vector3.one;
        private static Vector3 GetRight(ObjectHandle<Transform> transform) => transform ? transform.value.right : Vector3.right;
        private static void SetRight(ObjectHandle<Transform> transform, Vector3 right)
        {
            if (transform)
                transform.value.right = right;
        }
        private static Vector3 GetUp(ObjectHandle<Transform> transform) => transform ? transform.value.up : Vector3.up;
        private static void SetUp(ObjectHandle<Transform> transform, Vector3 up)
        {
            if (transform)
                transform.value.up = up;
        }
        private static Vector3 GetForward(ObjectHandle<Transform> transform) => transform ? transform.value.forward : Vector3.forward;
        private static void SetForward(ObjectHandle<Transform> transform, Vector3 forward)
        {
            if (transform)
                transform.value.forward = forward;
        }
        private static Matrix4x4 GetLocalToWorldMatrix(ObjectHandle<Transform> transform) => transform ? transform.value.localToWorldMatrix : Matrix4x4.identity;
        private static Matrix4x4 GetWorldToLocalMatrix(ObjectHandle<Transform> transform) => transform ? transform.value.worldToLocalMatrix : Matrix4x4.identity;
        private static ObjectHandle<Transform> GetRoot(ObjectHandle<Transform> transform) => transform ? transform.value.root : default;
        private static ObjectHandle<Transform> GetParent(ObjectHandle<Transform> transform) => transform ? transform.value.parent : default;
        private static void SetParent(ObjectHandle<Transform> transform, ObjectHandle<Transform> parent, bool worldPositionStays = true)
        {
            if (transform)
                transform.value.SetParent(parent, worldPositionStays);
        }
        private static int GetChildCount(ObjectHandle<Transform> transform) => transform ? transform.value.childCount : 0;
        private static int GetHierarchyCount(ObjectHandle<Transform> transform) => transform ? transform.value.hierarchyCount : 0;
        private static bool GetHasChanged(ObjectHandle<Transform> transform) => transform ? transform.value.hasChanged : false;
        private static void SetHasChanged(ObjectHandle<Transform> transform, bool hasChanged)
        {
            if (transform)
                transform.value.hasChanged = hasChanged;
        }
        private static int GetSiblingIndex(ObjectHandle<Transform> transform) => transform ? transform.value.GetSiblingIndex() : -1;
        private static void SetSiblingIndex(ObjectHandle<Transform> transform, int index)
        {
            if (transform)
                transform.value.SetSiblingIndex(index);
        }
        private static void SetAsFirstSibling(ObjectHandle<Transform> transform)
        {
            if (transform)
                transform.value.SetAsFirstSibling();
        }
        private static void SetAsLastSibling(ObjectHandle<Transform> transform)
        {
            if (transform)
                transform.value.SetAsLastSibling();
        }
        private static ObjectHandle<Transform> GetChild(ObjectHandle<Transform> transform, int index)
        {
            if (transform && index >= 0 && index < transform.value.childCount)
                return transform.value.GetChild(index);
            return default;
        }
        private static ObjectHandle<Transform> FindChild(ObjectHandle<Transform> transform, String8 name)
        {
            if (!transform)
                return default;
            return transform.value.Find(name.ToString());
        }
        private static bool IsChildOf(ObjectHandle<Transform> transform, ObjectHandle<Transform> parent) => transform && parent ? transform.value.IsChildOf(parent) : false;
        private static void DetachChildren(ObjectHandle<Transform> transform)
        {
            if (transform)
                transform.value.DetachChildren();
        }
        private static void LookAt(ObjectHandle<Transform> transform, ObjectHandle<Transform> target, Vector3 worldUp = default)
        {
            if (transform && target)
                transform.value.LookAt(target, worldUp == default ? Vector3.up : worldUp);
        }
        private static void LookAtPosition(ObjectHandle<Transform> transform, Vector3 worldPosition, Vector3 worldUp = default)
        {
            if (transform)
                transform.value.LookAt(worldPosition, worldUp == default ? Vector3.up : worldUp);
        }
        private static void TransformTranslate(ObjectHandle<Transform> transform, Vector3 translation, Space relativeTo = Space.Self)
        {
            if (transform)
                transform.value.Translate(translation, relativeTo);
        }
        private static void TransformRotate(ObjectHandle<Transform> transform, Vector3 eulers, Space relativeTo = Space.Self)
        {
            if (transform)
                transform.value.Rotate(eulers, relativeTo);
        }
        private static void TransformRotateAxis(ObjectHandle<Transform> transform, Vector3 axis, float angle, Space relativeTo = Space.Self)
        {
            if (transform)
                transform.value.Rotate(axis, angle, relativeTo);
        }
        private static void TransformRotateAround(ObjectHandle<Transform> transform, Vector3 point, Vector3 axis, float angle)
        {
            if (transform)
                transform.value.RotateAround(point, axis, angle);
        }
        private static Vector3 TransformPoint(ObjectHandle<Transform> transform, Vector3 position) => transform ? transform.value.TransformPoint(position) : position;
        private static Vector3 InversePoint(ObjectHandle<Transform> transform, Vector3 position) => transform ? transform.value.InverseTransformPoint(position) : position;
        private static Vector3 TransformDirection(ObjectHandle<Transform> transform, Vector3 direction) => transform ? transform.value.TransformDirection(direction) : direction;
        private static Vector3 InverseDirection(ObjectHandle<Transform> transform, Vector3 direction) => transform ? transform.value.InverseTransformDirection(direction) : direction;
        private static Vector3 TransformVector(ObjectHandle<Transform> transform, Vector3 vector) => transform ? transform.value.TransformVector(vector) : vector;
        private static Vector3 InverseVector(ObjectHandle<Transform> transform, Vector3 vector) => transform ? transform.value.InverseTransformVector(vector) : vector;
        private static void SetPositionAndRotation(ObjectHandle<Transform> transform, Vector3 position, Quaternion rotation)
        {
            if (transform)
                transform.value.SetPositionAndRotation(position, rotation);
        }
        private static void SetLocalPositionAndRotation(ObjectHandle<Transform> transform, Vector3 localPosition, Quaternion localRotation)
        {
            if (transform)
                transform.value.SetLocalPositionAndRotation(localPosition, localRotation);
        }
        private static void GetPositionAndRotation(ObjectHandle<Transform> transform, out Vector3 position, out Quaternion rotation)
        {
            if (transform)
                transform.value.GetPositionAndRotation(out position, out rotation);
            else
            {
                position = default;
                rotation = Quaternion.identity;
            }
        }
        private static void GetLocalPositionAndRotation(ObjectHandle<Transform> transform, out Vector3 localPosition, out Quaternion localRotation)
        {
            if (transform)
                transform.value.GetLocalPositionAndRotation(out localPosition, out localRotation);
            else
            {
                localPosition = default;
                localRotation = Quaternion.identity;
            }
        }
        private static Slice<ObjectHandle<Transform>> GetChildren(ObjectHandle<Transform> transform, Allocator allocator)
        {
            if (!transform)
                return default;
            var count = transform.value.childCount;
            var slice = new Slice<ObjectHandle<Transform>>(count, allocator);
            for (var i = 0; i < count; i++)
                slice.ptr[i] = transform.value.GetChild(i);
            return slice;
        }

        // RectTransform specific (for UI)
        private static bool IsRect(ObjectHandle<Transform> transform) => transform ? transform.value is RectTransform : false;

    }
}
