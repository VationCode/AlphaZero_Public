using UnityEngine;

namespace alpha.ingame.camera
{
    /// <summary>화면 흔들림 이전의 카메라 좌표로 게임플레이 조준을 계산한다.</summary>
    public static class CameraAim
    {
        public static Transform StableTransform(Camera camera)
        {
            if (camera == null) return null;
            // 시각 카메라의 부모에서 화면 흔들림 컴포넌트를 찾는다.
            CameraShakeModule shake = camera.GetComponentInParent<CameraShakeModule>();
            return shake != null && shake.transform.parent != null
                ? shake.transform.parent : camera.transform;
        }

        public static Ray CenterRay(Camera camera)
        {
            // 흔들림 이전 Transform을 사격·스킬 광선의 기준으로 쓴다.
            Transform stable = StableTransform(camera);
            return stable != null ? new Ray(stable.position, stable.forward) : default;
        }

        public static Vector3 WorldToScreenPoint(Camera camera, Vector3 point)
        {
            if (camera == null) return default;
            // 화면 투영을 계산할 때 사용할 흔들림 이전 좌표계다.
            Transform stable = StableTransform(camera);
            // 시각 카메라의 투영을 유지하면서 기준 좌표계만 흔들림 이전으로 되돌린다.
            // 대상의 좌표를 시각 카메라 기준으로 재배치한 값이다.
            Vector3 remapped = camera.transform.TransformPoint(stable.InverseTransformPoint(point));
            return camera.WorldToScreenPoint(remapped);
        }
    }
}
