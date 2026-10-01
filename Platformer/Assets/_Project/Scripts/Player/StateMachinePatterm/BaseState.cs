using DG.Tweening;
using UnityEngine;
using UnityUtils.StateMachine;

namespace Platformer
{
    public abstract class BaseState : IState   
    {
        protected readonly PlayerMovement player;
        protected readonly Animator animator;

        protected static readonly int DoubleJumpHash = Animator.StringToHash("DoubleJump");
        protected static readonly int LocomotionHash = Animator.StringToHash("Locomotion");
        protected static readonly int JumpHash = Animator.StringToHash("Jump");
        protected static readonly int WallclimbHash = Animator.StringToHash("WallClimb");
        // The Dodge state holds the DodgeAnims blend tree (VelX/VelZ directional)
        protected static readonly int DodgeHash = Animator.StringToHash("Dodge");
        //protected static readonly int AttackHash = Animator.StringToHash("Attack");
        //protected static readonly int BlastAttackHash = Animator.StringToHash("SpinAttack");
        protected static readonly int GlideHash = Animator.StringToHash("Glide");
        protected static readonly int DieHash = Animator.StringToHash("Death");
        protected static readonly int TeleportationHash = Animator.StringToHash("Teleport");
        protected static readonly int HurtHash = Animator.StringToHash("Hurt");
        protected static readonly int SprintHash = Animator.StringToHash("Sprint");

        // Armed locomotion variants — used when player.combat.hasWeapon is true
        protected static readonly int LocomotionArmedHash = Animator.StringToHash("Locomotion_Armed");
        protected static readonly int SprintArmedHash = Animator.StringToHash("Sprint_Armed");

        // Aim strafe locomotion (2D blend tree driven by VelX/VelZ)
        protected static readonly int AimLocomotionHash = Animator.StringToHash("AimLocomotion");

        protected const float crossFadeDuration = 0.1f;

        protected BaseState(PlayerMovement player, Animator animator)
        {
            this.player = player;
            this.animator = animator;
        }

        public virtual void OnEnter() { }
        public virtual void Update() { }
        public virtual void FixedUpdate() { }
        public virtual void OnExit() { }

        // Helper for states that have armed/unarmed variants
        protected void CrossFadeArmed(int unarmedHash, int armedHash)
            => animator.CrossFade(player.combat.hasWeapon ? armedHash : unarmedHash, crossFadeDuration);
    }
    
    #region States
    public class LocomotionState : BaseState
    {
        bool wasArmed;

        public LocomotionState(PlayerMovement player, Animator animator) : base(player, animator) { }

        public override void OnEnter()
        {
            wasArmed = player.combat.hasWeapon;
            CrossFadeArmed(LocomotionHash, LocomotionArmedHash);
        }

        public override void Update()
        {
            // Swap anim if weapon equip state changes mid-locomotion
            if (wasArmed != player.combat.hasWeapon)
            {
                wasArmed = player.combat.hasWeapon;
                CrossFadeArmed(LocomotionHash, LocomotionArmedHash);
            }
        }

        public override void FixedUpdate()
        {
            player.HandleMovement();
        }
    }

    public class SprintState : BaseState
    {
        bool wasArmed;

        public SprintState(PlayerMovement player, Animator animator) : base(player, animator) { }

        public override void OnEnter()
        {
            wasArmed = player.combat.hasWeapon;
            CrossFadeArmed(SprintHash, SprintArmedHash);
        }

        public override void Update()
        {
            // Swap anim if weapon equip state changes mid-sprint
            if (wasArmed != player.combat.hasWeapon)
            {
                wasArmed = player.combat.hasWeapon;
                CrossFadeArmed(SprintHash, SprintArmedHash);
            }
        }

        public override void FixedUpdate()
        {
            player.HandleMovement();
        }
    }
    public class AimState : BaseState
    {
        public AimState(PlayerMovement player, Animator animator) : base(player, animator) { }

        public override void OnEnter()
        {
            animator.CrossFade(AimLocomotionHash, crossFadeDuration);
        }

        public override void FixedUpdate()
        {
            player.HandleAimMovement();
        }

        public override void OnExit()
        {
            player.ResetAimBlend();
        }
    }

    public class WallClimbState : BaseState
    {
        public WallClimbState(PlayerMovement player, Animator animator) : base(player, animator) { }

        public override void OnEnter()
        {
            animator.CrossFade(WallclimbHash, crossFadeDuration);
            player.wallClimbPos = player.transform.position;
        }
        public override void FixedUpdate()
        {
            player.HandleWallClimb();
        }

        public override void OnExit()
        {
            player.wallClimbPos = Vector3.zero;
            player.wallClimbimg = false;
        }
    }
    public class DodgeState : BaseState
    {
        public DodgeState(PlayerMovement player, Animator animator) : base(player, animator) { }
        public override void OnEnter()
        {
            player.ApplyDodgeBlend();
            animator.CrossFade(DodgeHash, crossFadeDuration);
        }
        public override void FixedUpdate()
        {
            player.HandleDodge();
        }
    }
    public class GlideState : BaseState
    {
        public GlideState(PlayerMovement player, Animator animator) : base(player, animator) { }
        public override void OnEnter() {
            animator.CrossFade(GlideHash, crossFadeDuration);
        }
        public override void FixedUpdate() 
        {
            player.HandleGlide();
            player.HandleMovement();
        }
    }
    public class JumpState : BaseState 
    {
        public JumpState(PlayerMovement player, Animator animator) : base(player, animator) { }

        public override void OnEnter() {
            animator.CrossFade(JumpHash, crossFadeDuration);
        }

        public override void FixedUpdate() {
            player.HandleJump();
            player.HandleMovement();
        }
    }
    public class DoubleJumpState : BaseState
    {
        public DoubleJumpState(PlayerMovement player, Animator animator) : base(player, animator) { }

        public override void OnEnter()
        {
            // Play double jump animation
            animator.CrossFade(DoubleJumpHash, crossFadeDuration);
        }

        public override void FixedUpdate()
        {
            player.HandleJump();
            player.HandleMovement();
        }
    }
    public class AttackState : BaseState {
        bool lungingAtTarget;

        public AttackState(PlayerMovement player, Animator animator) : base(player, animator) { }

        public override void OnEnter() => BeginSwing();

        public override void Update()
        {
            // Combo flow: a queued press starts the next swing once the current one
            // has played far enough, so chains don't stall between swings
            if (player.combat.TryConsumeBufferedCombo()) BeginSwing();
        }

        void BeginSwing()
        {
            player.transform.DOKill();

            // Re-arm the attack window so it spans this whole swing (the timer may
            // have been started earlier, e.g. mid-dodge or by the previous swing)
            player.combat.RestartAttackWindow();

            // When we have a soft-lock target the DOTween lunge owns our position —
            // stop feeding the rigidbody velocity or the two visibly fight.
            lungingAtTarget = player.combat.enemyDetection != null
                              && player.combat.enemyDetection.CurrentTarget() != null;
            if (lungingAtTarget) player.StopMovement();

            string anim = player.commandManager.LightAttackCommand.Advance();
            animator.CrossFade(anim, crossFadeDuration);
            _ = player.commandManager.LightAttackCommand.Execute();
        }

        public override void FixedUpdate()
        {
            if (!lungingAtTarget) player.HandleMovement();
        }
    }
    public class BlastAttackState : BaseState
    {
        public BlastAttackState(PlayerMovement player, Animator animator) : base(player, animator) { }
        public override void OnEnter()
        {
            // Kill any lingering DOTween tweens (e.g. DOLookAt/DOMove from light attack)
            player.transform.DOKill();

            string anim = player.commandManager.BlastAttackCommand.Advance();
            animator.CrossFade(anim, crossFadeDuration);
            _ = player.commandManager.BlastAttackCommand.Execute();
        }
        public override void FixedUpdate() { }
    }
    public class HurtState : BaseState 
    {
        public HurtState(PlayerMovement player, Animator animator) : base(player, animator) { }

        public override void OnEnter() 
        {
            animator.CrossFade(HurtHash, crossFadeDuration);
            player.StopMovement(); 
        }
        public override void FixedUpdate() { }
    }
    public class DeathState : BaseState
    {
        Health playerHealth;
        public DeathState(PlayerMovement player, Animator animator, Health playerHealth) : base(player, animator)
        {
            this.playerHealth = playerHealth;
        }
        public override void OnEnter()
        {
            animator.CrossFade(DieHash, crossFadeDuration);
            playerHealth.HandleDeath();
        }
        public override void FixedUpdate() { }
    }
    public class TeleportState : BaseState
    {
        public TeleportState(PlayerMovement player, Animator animator) : base(player, animator) { }
        
        public override void OnEnter() {
            animator.CrossFade(TeleportationHash, crossFadeDuration);
        }
        public override void FixedUpdate() 
        {
            
        }
    }
    #endregion
}