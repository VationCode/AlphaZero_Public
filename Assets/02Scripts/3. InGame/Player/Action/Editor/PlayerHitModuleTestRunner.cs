using System;
using UnityEditor;
using UnityEngine;

namespace alpha.ingame.player
{
    // PlayMode의 독립 객체에서 피격 4종, 피해 무적, 반응 면역을 순서대로 확인한다.
    public static class PlayerHitModuleTestRunner
    {
        private const string MenuPath = "Tools/AlphaZero/Test Player Hit Module";

        private static GameObject _rig;
        private static PlayerHealthModule _health;
        private static PlayerHitModule _hit;
        private static PlayerActionStateFlow _action;
        private static int _step;
        private static float _deadline;
        private static Vector3 _knockbackStart;
        private static Vector3 _knockdownStart;

        [MenuItem(MenuPath, true)]
        private static bool ValidateRun() => EditorApplication.isPlaying && _rig == null;

        [MenuItem(MenuPath)]
        private static void Run()
        {
            if (!EditorApplication.isPlaying || _rig != null) return;

            try
            {
                _rig = new GameObject("Player Hit Test Rig");
                _rig.SetActive(false);
                _rig.transform.position = new Vector3(500f, 0f, 500f);
                _rig.AddComponent<CharacterController>();
                _rig.AddComponent<PlayerMotionModule>();
                PlayerRoot core = _rig.AddComponent<PlayerRoot>();
                core.enabled = false; // 실제 모델과 게임 입력은 생성하지 않는다.
                _health = _rig.AddComponent<PlayerHealthModule>();
                _rig.AddComponent<LocomotionModule>();
                _hit = _rig.AddComponent<PlayerHitModule>();
                _action = _rig.AddComponent<PlayerActionStateFlow>();
                _rig.SetActive(true);

                Require(_health.CurrentHealth > 0f, "체력이 초기화되지 않았습니다.");
                EditorApplication.update += Tick;
                EditorApplication.playModeStateChanged += HandlePlayModeChanged;
                StartCase(EPlayerHitType.Normal, false);
                _step = 1;
            }
            catch (Exception exception)
            {
                Finish(false, exception.Message);
            }
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || _rig == null)
            {
                Finish(false, "PlayMode가 중단되었습니다.");
                return;
            }
            // 피격 이동은 Action State Flow의 Update에서 갱신한다.
            if (Time.time < _deadline) return;

            try
            {
                switch (_step)
                {
                    case 1:
                        Require(_action.CurrentPhase == EPlayerHitPhase.None,
                            "일반 피격이 종료되지 않았습니다.");
                        StartCase(EPlayerHitType.Stagger, true);
                        _step = 2;
                        break;
                    case 2:
                        Require(_action.CurrentPhase == EPlayerHitPhase.None,
                            "경직 피격이 종료되지 않았습니다.");
                        _knockbackStart = _rig.transform.position;
                        StartCase(EPlayerHitType.Knockback, true, 1.5f, 0f, 3f, 0.3f);
                        _step = 3;
                        break;
                    case 3:
                        Require(_action.CurrentPhase == EPlayerHitPhase.None,
                            "넉백 피격이 종료되지 않았습니다.");
                        float moved = Mathf.Abs(_rig.transform.position.x - _knockbackStart.x);
                        float expected = Mathf.Max(GetFloat("_knockbackDistance") + 1.5f, 3f);
                        Require(Mathf.Abs(moved - expected) < 0.25f,
                            $"몸체 바깥 이격 거리가 적용되지 않았습니다: {moved:0.##}m (예상 {expected:0.##}m)");
                        _knockdownStart = _rig.transform.position;
                        StartCase(EPlayerHitType.Knockdown, true, 1.5f);
                        _deadline = Time.time + GetTiming(EPlayerHitType.Knockdown,
                            "_reactionDuration") + 0.1f;
                        _step = 4;
                        break;
                    case 4:
                        Require(_action.CurrentPhase == EPlayerHitPhase.Lying,
                            "넉다운이 누운 상태로 전환되지 않았습니다.");
                        Require(_hit.IsHitMotionActive,
                            "넘어짐 애니메이션 단계가 끝나기 전에 밀림이 종료되었습니다.");
                        // 넘어지는 클립 뒤에도 밀림이 남을 수 있어 이동 완료 후 거리를 검사한다.
                        _deadline = Time.time + _hit.HitMotionRemainingDuration +
                            GetFloat("_knockdownLyingDuration") + 0.1f;
                        _step = 5;
                        break;
                    case 5:
                        float knockdownMoved = _knockdownStart.x - _rig.transform.position.x;
                        float knockdownDistance = GetFloat("_knockdownDistance") + 1.5f;
                        Require(Mathf.Abs(knockdownMoved - knockdownDistance) < 0.25f,
                            $"넉다운 추가 거리가 적용되지 않았습니다: {knockdownMoved:0.##}m");
                        Require(_action.CurrentPhase == EPlayerHitPhase.Standing,
                            "넉다운이 기상 상태로 전환되지 않았습니다.");
                        _deadline = Time.time + GetFloat("_standUpDuration") + 0.1f;
                        _step = 6;
                        break;
                    case 6:
                        Require(_action.CurrentPhase == EPlayerHitPhase.None,
                            "기상 후 일반 상태로 복귀하지 않았습니다.");
                        VerifyReactionImmunity();
                        Finish(true, "피해 없는 넉백, 나머지 피격 3종, 피해 무적과 반응 면역을 확인했습니다.");
                        break;
                }
            }
            catch (Exception exception)
            {
                Finish(false, exception.Message);
            }
        }

        private static void StartCase(EPlayerHitType type, bool blocksActions,
            float additionalDistance = 0f, float damage = 1f,
            float minimumDistance = 0f, float moveDurationOverride = 0f)
        {
            float before = _health.CurrentHealth;
            Vector3 direction = type == EPlayerHitType.Knockdown
                ? Vector3.left : Vector3.right;
            PlayerHitInfo info = new(damage, type, null, direction,
                additionalDistance, minimumDistance, moveDurationOverride);

            IPlayerHitReceiver receiver = _rig.GetComponent<PlayerRoot>();
            Require(receiver.TryReceiveHit(info), $"{type} 피격이 거부되었습니다.");
            Require(Mathf.Approximately(_health.CurrentHealth, before - damage),
                $"{type} 피해량이 체력에 반영되지 않았습니다.");
            EPlayerHitPhase expectedPhase = type switch
            {
                EPlayerHitType.Normal => EPlayerHitPhase.Normal,
                EPlayerHitType.Stagger => EPlayerHitPhase.Stagger,
                EPlayerHitType.Knockback => EPlayerHitPhase.Knockback,
                EPlayerHitType.Knockdown => EPlayerHitPhase.Knockdown,
                _ => EPlayerHitPhase.None
            };
            Require(_action.CurrentPhase == expectedPhase,
                $"{type} 피격 상태로 진입하지 않았습니다.");
            Require(_action.IsActionBlocked == blocksActions,
                $"{type} 행동 제한 상태가 잘못되었습니다.");
            if (type is EPlayerHitType.Knockback or EPlayerHitType.Knockdown)
                Require(Vector3.Dot(_rig.transform.forward, -direction) > 0.99f,
                    $"{type} 회전이 충돌 지점의 반대쪽을 바라봅니다.");
            if (moveDurationOverride > 0f)
            {
                float totalDistance = Mathf.Max(
                    GetFloat(type == EPlayerHitType.Knockback
                        ? "_knockbackDistance" : "_knockdownDistance") + additionalDistance,
                    minimumDistance);
                float expectedDuration = Mathf.Max(moveDurationOverride,
                    totalDistance / GetFloat("_maximumKnockbackSpeed"));
                Require(Mathf.Approximately(_hit.HitMotionRemainingDuration,
                    expectedDuration), "접촉 넉백 속도 제한이 적용되지 않았습니다.");
            }

            float damageInvulnerability = GetTiming(type, "_damageInvulnerabilityDuration");
            if (damage > 0f && damageInvulnerability > 0f)
            {
                Require(!receiver.TryReceiveHit(info), $"{type} 피해 무적이 적용되지 않았습니다.");
                Require(Mathf.Approximately(_health.CurrentHealth, before - damage),
                    $"{type} 중복 피해가 발생했습니다.");
            }

            float recovery = GetTiming(type, "_reactionDuration");
            _deadline = Time.time + Mathf.Max(recovery, damageInvulnerability,
                _hit.HitMotionRemainingDuration, 0.2f) + 0.1f;
        }

        private static void VerifyReactionImmunity()
        {
            if (GetTiming(EPlayerHitType.Knockdown, "_reactionImmunityDuration") <= 0.2f)
                return;

            float before = _health.CurrentHealth;
            PlayerHitInfo info = new(1f, EPlayerHitType.Knockdown, null, Vector3.right);
            IPlayerHitReceiver receiver = _rig.GetComponent<PlayerRoot>();
            Require(receiver.TryReceiveHit(info), "반응 면역 중 피해가 거부되었습니다.");
            Require(Mathf.Approximately(_health.CurrentHealth, before - 1f),
                "반응 면역 중 체력이 감소하지 않았습니다.");
            Require(_action.CurrentPhase == EPlayerHitPhase.None,
                "반응 면역 중 넉다운이 다시 시작되었습니다.");
        }

        private static float GetTiming(EPlayerHitType type, string field)
        {
            string name = type switch
            {
                EPlayerHitType.Normal => "_normal",
                EPlayerHitType.Stagger => "_stagger",
                EPlayerHitType.Knockback => "_knockback",
                EPlayerHitType.Knockdown => "_knockdown",
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };
            SerializedObject serialized = new(_hit);
            SerializedProperty timing = serialized.FindProperty(name);
            Require(timing != null, $"{name} 설정을 찾지 못했습니다.");
            SerializedProperty value = timing.FindPropertyRelative(field);
            Require(value != null, $"{name}.{field} 설정을 찾지 못했습니다.");
            return value.floatValue;
        }

        private static float GetFloat(string field)
        {
            SerializedObject serialized = new(_hit);
            SerializedProperty value = serialized.FindProperty(field);
            Require(value != null, $"{field} 설정을 찾지 못했습니다.");
            return value.floatValue;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void HandlePlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                Finish(false, "PlayMode가 중단되었습니다.");
        }

        private static void Finish(bool passed, string message)
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            if (passed) Debug.Log($"PlayerHitModule 테스트 통과: {message}");
            else Debug.LogError($"PlayerHitModule 테스트 실패: {message}");
            if (_rig != null) UnityEngine.Object.Destroy(_rig);
            _rig = null;
            _health = null;
            _hit = null;
            _action = null;
            _step = 0;
        }
    }
}
