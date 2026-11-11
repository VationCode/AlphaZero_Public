using UnityEngine;

namespace alpha.ingame
{
    /// <summary>대상을 따라가는 위치 보간과 속도 상태를 관리한다.</summary>
    public sealed class SmoothPositionFollower
    {
        private Vector3 _velocity;

        public Vector3 Step(Vector3 current, Vector3 target, float smoothTime) =>
            Vector3.SmoothDamp(current, target, ref _velocity, Mathf.Max(0.0001f, smoothTime));

        public void Reset() => _velocity = Vector3.zero;
    }
}
