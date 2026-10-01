using KBCore.Refs;
using UnityEngine;
using UnityEngine.AI;
using UnityUtils.StateMachine;
using ImprovedTimers;

namespace Platformer
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(PlayerDetector))]
    public class GroundAI : Entity
    {
        [field: Header("Component References")]
        [field: SerializeField, Self] public NavMeshAgent agent { get; private set; }
        [field: SerializeField, Self] public PlayerDetector playerDetector;
        [field: SerializeField, Child] public Animator animator;
        [field: SerializeField, Self] public Health enemyHealth;

        [field: Header("Movement Settings")]
        [field: SerializeField] public float wanderRadius = 10f;
        [field: SerializeField] public float curiousRange = 8f;
        [field: SerializeField] public float fleeRange = 3f;
        [field: SerializeField] public float moveSpeed = 3.5f;
        [field: SerializeField] public float fleeSpeed = 6f;
        [field: SerializeField] public float rotationSpeed = 10f;

        [field: Header("Knockback Settings")]
        [SerializeField] float knockbackForce = 8f;

        public bool isScared = false;
        [System.NonSerialized] public CountdownTimer knockbackTimer;

        StateMachine stateMachine;
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        void OnValidate() => this.ValidateRefs();

        void Start()
        {
            agent.speed = moveSpeed;
            knockbackTimer = new CountdownTimer(0.5f);

            stateMachine = new StateMachine();

            var wanderState = new ChomperEnemyWanderState(this, animator, agent, wanderRadius);
            var curiousState = new ChomperEnemyCuriousState(this, animator, agent);
            var fleeState = new ChomperEnemyFleeState(this, animator, agent);
            var knockbackState = new ChomperEnemyKnockbackState(this, animator);
            var dieState = new ChomperEnemyDieState(this, animator, enemyHealth);

            At(wanderState, curiousState, new FuncPredicate(() => PlayerInRange(curiousRange)));
            At(curiousState, wanderState, new FuncPredicate(() => !PlayerInRange(curiousRange)));
            At(curiousState, fleeState, new FuncPredicate(() => PlayerInRange(fleeRange)));
            At(fleeState, wanderState, new FuncPredicate(() => !PlayerInRange(curiousRange) && !isScared));
            Any(knockbackState, new FuncPredicate(() => knockbackTimer.IsRunning));
            At(knockbackState, fleeState, new FuncPredicate(() => !knockbackTimer.IsRunning));
            Any(dieState, new FuncPredicate(() => enemyHealth.isDead));

            stateMachine.SetState(wanderState);
        }

        void At(IState from, IState to, FuncPredicate condition) => stateMachine.AddTransition(from, to, condition);
        void Any(IState to, FuncPredicate condition) => stateMachine.AddAnyTransition(to, condition);

        void Update()
        {
            stateMachine.Update();
            knockbackTimer.Tick();
        }

        void FixedUpdate() => stateMachine.FixedUpdate();

        public bool PlayerInRange(float range)
        {
            if (playerDetector.Player == null) return false;
            return Vector3.Distance(transform.position, playerDetector.Player.position) <= range;
        }

        public void UpdateAnimation()
        {
            float speed = new Vector3(agent.velocity.x, 0, agent.velocity.z).magnitude;
            animator.SetFloat(SpeedHash, speed, 0.1f, Time.deltaTime);
        }

        public void FacePlayer()
        {
            if (playerDetector.Player != null) {
                Vector3 dir = (playerDetector.Player.position - transform.position).normalized;
                dir.y = 0;
                if (dir != Vector3.zero)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * rotationSpeed);
            }
        }

        public void FleeFromPlayer()
        {
            if (playerDetector.Player == null) return;
            agent.speed = fleeSpeed;
            Vector3 fleeDir = (transform.position - playerDetector.Player.position).normalized;
            Vector3 fleeTarget = transform.position + fleeDir * 10f;
            agent.SetDestination(fleeTarget);
        }

        private void OnEnable() => enemyHealth.OnHit += HandleOnHit;
        private void OnDisable() => enemyHealth.OnHit -= HandleOnHit;

        void HandleOnHit(float stunDuration)
        {
            isScared = true;
            knockbackTimer.Reset(stunDuration);
            knockbackTimer.Start();

            if (playerDetector.Player != null)
            {
                Vector3 pushDir = (transform.position - playerDetector.Player.position).normalized;
                pushDir.y = 0;
                agent.velocity = pushDir * knockbackForce;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, curiousRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, fleeRange);
        }
    }
}