using UnityEngine;

namespace alpha.ingame.player
{
    /// <summary>조준한 바닥의 거리, 경사와 설치 공간을 검사한다.</summary>
    public static class SkillPlacementValidator
    {
        public static bool TryGetPlacement(DeployableSkillData data, Transform owner, Camera camera, out Pose pose)
            => TryGetPlacement(data, owner, camera, out pose, out _);

        // 조준한 표면이 있으면 설치 불가 상태에서도 미리보기 위치를 돌려준다.
        public static bool TryGetPlacement(DeployableSkillData data, Transform owner, Camera camera,
            out Pose pose, out bool hasTarget)
        {
            pose = default;
            hasTarget = false;
            if (data == null || owner == null || camera == null) return false;

            Ray ray = alpha.ingame.camera.CameraAim.CenterRay(camera);
            float rayDistance = data.MaxDistance + Vector3.Distance(ray.origin, owner.position) + 1f;
            RaycastHit[] hits = Physics.RaycastAll(ray, rayDistance, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            // 플레이어 자신의 충돌체를 제외하고 가장 앞에 있는 물체를 검사한다.
            RaycastHit nearest = default;
            float nearestDistance = float.PositiveInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(owner) || hit.distance >= nearestDistance) continue;
                nearest = hit;
                nearestDistance = hit.distance;
            }

            if (nearest.collider == null) return false;

            Vector3 forward = Vector3.ProjectOnPlane(owner.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(ray.direction, Vector3.up);
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            Vector3 position = nearest.point + rotation * data.PlacementOffset;
            pose = new Pose(position, rotation);
            hasTarget = true;

            if ((data.SurfaceMask.value & (1 << nearest.collider.gameObject.layer)) == 0 ||
                Vector3.Distance(owner.position, position) > data.MaxDistance ||
                Vector3.Angle(nearest.normal, Vector3.up) > data.MaxSlopeAngle)
                return false;

            float radius = data.ClearanceRadius;
            float height = Mathf.Max(data.ClearanceHeight, radius * 2f);
            Vector3 bottom = position + Vector3.up * (radius + 0.01f);
            Vector3 top = position + Vector3.up * (height - radius + 0.01f);
            Collider[] overlaps = Physics.OverlapCapsule(bottom, top, radius, data.ObstacleMask,
                QueryTriggerInteraction.Ignore);
            foreach (Collider collider in overlaps)
            {
                if (collider == nearest.collider || collider.transform.IsChildOf(owner)) continue;
                return false;
            }

            return true;
        }
    }
}
