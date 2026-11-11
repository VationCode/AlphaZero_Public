using System;
using System.Collections.Generic;
using UnityEngine;

namespace alpha.system.combat
{
    // 쉴드의 위치와 활성 상태만 공격 객체에 공개한다.
    public interface IShieldBarrier
    {
        bool IsActive { get; }
        Vector3 Center { get; }
        float Radius { get; }
        float PushImpulse { get; }
    }

    public static class ShieldBarrierRegistry
    {
        private static readonly List<IShieldBarrier> Barriers = new();
        public static event Action<IShieldBarrier> Activated;

        public static void Register(IShieldBarrier barrier)
        {
            if (barrier == null || Barriers.Contains(barrier)) return;
            Barriers.Add(barrier);
            Activated?.Invoke(barrier);
        }

        public static void Unregister(IShieldBarrier barrier) => Barriers.Remove(barrier);

        public static bool TryGetContaining(Vector3 point, float margin,
            out IShieldBarrier barrier)
        {
            foreach (IShieldBarrier candidate in Barriers)
            {
                if (!candidate.IsActive ||
                    (point - candidate.Center).sqrMagnitude >
                    Mathf.Pow(candidate.Radius + margin, 2f)) continue;
                barrier = candidate;
                return true;
            }
            barrier = null;
            return false;
        }

        // 시작점이 바깥인 이동 선분이 쉴드 구체에 처음 닿는 위치를 찾는다.
        public static bool TryIntercept(Vector3 from, Vector3 to, float margin,
            out Vector3 contact)
        {
            foreach (IShieldBarrier barrier in Barriers)
            {
                if (!barrier.IsActive) continue;
                float radius = barrier.Radius + Mathf.Max(0f, margin);
                Vector3 offset = from - barrier.Center;
                float c = Vector3.Dot(offset, offset) - radius * radius;
                if (c < -0.0001f) continue; // 내부에서 바깥으로 나가는 공격은 통과한다.
                Vector3 movement = to - from;
                float a = Vector3.Dot(movement, movement);
                if (a <= 0.000001f) continue;
                float b = Vector3.Dot(offset, movement);
                if (c <= 0f && b < 0f)
                {
                    contact = from; // 경계에서 안쪽으로 움직이기 시작한 경우다.
                    return true;
                }
                float discriminant = b * b - a * c;
                if (b >= 0f || discriminant < 0f) continue;
                float fraction = (-b - Mathf.Sqrt(discriminant)) / a;
                if (fraction < 0f || fraction > 1f) continue;
                contact = from + movement * fraction;
                return true;
            }
            contact = default;
            return false;
        }
    }
}
