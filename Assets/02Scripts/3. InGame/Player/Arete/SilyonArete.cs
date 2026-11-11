using alpha.ingame.input;
using UnityEngine;

namespace alpha.ingame.player
{
    /// <summary>실리온의 지상 부스터와 부스터 효과를 관리한다.</summary>
    public sealed class SilyonArete : Arete
    {
        [SerializeField] private GameObject _mobilityEffect;

        public override EAreteType AreteType => EAreteType.Silyon;
        public override bool CanUseMobility => true;

        public override void Initialize(PlayerRoot playerCore)
        {
            base.Initialize(playerCore);
            if (_mobilityEffect != null) _mobilityEffect.SetActive(false);
        }

        public override void BeginMobility(AlphaInput input, bool isCombatStance)
        {
            Locomotion.BeginBoost();
            if (_mobilityEffect != null) _mobilityEffect.SetActive(true);
            Locomotion.SetBoostMoveType(isCombatStance);
            Locomotion.Move(input.MoveInput);
        }

        public override ELocomotionState? UpdateMobility(AlphaInput input, bool isCombatStance)
        {
            if (!input.IsMobilityHeld)
                return GetStateAfterBoost(input, isCombatStance);

            if (input.IsJump && Locomotion.CanGroundJump)
                return ELocomotionState.Jump;

            if (!Locomotion.ConsumeBoost(Time.deltaTime))
                return GetStateAfterBoost(input, isCombatStance);

            Locomotion.SetBoostMoveType(isCombatStance);
            Locomotion.Move(input.MoveInput);
            return Locomotion.IsFalling ? ELocomotionState.Fall : null;
        }

        public override void EndMobility()
        {
            Locomotion.EndBoost();
            if (_mobilityEffect != null) _mobilityEffect.SetActive(false);
        }

        private ELocomotionState GetStateAfterBoost(AlphaInput input, bool isCombatStance)
        {
            if (!Locomotion.IsGrounded) return ELocomotionState.Fall;
            return input.MoveInput.sqrMagnitude > 0.0001f || isCombatStance
                ? ELocomotionState.Move
                : ELocomotionState.Idle;
        }
    }
}
