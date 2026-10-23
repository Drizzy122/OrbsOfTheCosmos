using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// Final blow moment, after Mix and Jam's freeflow combat: when the killing
    /// blow empties the arena, time drops, a dedicated Cinemachine camera cuts
    /// in close on the player and the fallen enemy and slowly orbits them, then
    /// everything blends back to the gameplay camera.
    /// Builds its own camera at runtime (no scene wiring); PlayerCombat adds
    /// this to the Player and calls Play() with the enemy's position.
    /// </summary>
    public class FinalBlowCamera : MonoBehaviour
    {
        [Header("Timing")]
        [Tooltip("Real seconds the moment lasts.")]
        [SerializeField] float duration = 1.7f;
        [Tooltip("Time scale during the moment.")]
        [SerializeField, Range(0.05f, 1f)] float timeScale = 0.3f;
        [Tooltip("Seconds the brain takes to cut in and blend back out.")]
        [SerializeField] float blendIn = 0.25f;
        [SerializeField] float blendOut = 0.8f;

        [Header("Framing")]
        [Tooltip("Field of view of the close-up (the gameplay camera is wider).")]
        [SerializeField] float fieldOfView = 36f;
        [Tooltip("Where to look, 0 = on the player, 1 = on the enemy.")]
        [SerializeField, Range(0f, 1f)] float focusTowardEnemy = 0.6f;
        [SerializeField] float focusHeight = 1.2f;
        [Tooltip("Camera distance from the focus point.")]
        [SerializeField] float distance = 3.6f;
        [SerializeField] float height = 1.1f;
        [Tooltip("Where the camera starts around the pair: 0 = behind the player, 90 = on the player's right, -90 = on the left, 180 = behind the enemy.")]
        [SerializeField, Range(-180f, 180f)] float sideAngle = 80f;
        [Tooltip("Degrees per real second the camera circles the pair.")]
        [SerializeField] float orbitSpeed = 22f;

        CinemachineCamera cam;
        CinemachineFollow follow;
        Transform focus;
        Coroutine running;

        public bool IsPlaying => running != null;

        void Awake()
        {
            focus = new GameObject("FinalBlowFocus").transform;

            var go = new GameObject("CM FinalBlow");
            cam = go.AddComponent<CinemachineCamera>();
            cam.Follow = focus;
            cam.LookAt = focus;
            cam.Priority = 50;                      // above the free-look camera while enabled
            cam.Lens.FieldOfView = fieldOfView;

            follow = go.AddComponent<CinemachineFollow>();
            follow.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
            follow.TrackerSettings.PositionDamping = new Vector3(0.4f, 0.4f, 0.4f);

            var composer = go.AddComponent<CinemachineRotationComposer>();
            composer.Damping = new Vector2(0.3f, 0.3f);

            go.SetActive(false);
        }

        void OnDestroy()
        {
            if (cam != null) Destroy(cam.gameObject);
            if (focus != null) Destroy(focus.gameObject);
        }

        /// <summary>Cut to the close-up on the fallen enemy at this world position.</summary>
        public void Play(Vector3 enemyPosition)
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(PlayRoutine(enemyPosition));
        }

        IEnumerator PlayRoutine(Vector3 enemyPosition)
        {
            // Focus between the player and the enemy, at chest height.
            Vector3 playerPos = transform.position;
            Vector3 focusPos = Vector3.Lerp(playerPos, enemyPosition, focusTowardEnemy);
            focusPos.y = Mathf.Min(playerPos.y, enemyPosition.y) + focusHeight;
            focus.position = focusPos;

            // Start the camera at sideAngle around the pair (0 = behind the player),
            // looking across at the enemy.
            Vector3 toEnemy = enemyPosition - playerPos;
            toEnemy.y = 0f;
            if (toEnemy.sqrMagnitude < 0.01f) toEnemy = transform.forward;
            toEnemy.Normalize();
            Vector3 dir = Quaternion.AngleAxis(-sideAngle, Vector3.up) * -toEnemy;   // positive angle = player's right
            Vector3 offset = dir * distance + Vector3.up * height;

            var brain = CinemachineBrain.GetActiveBrain(0);
            CinemachineBlendDefinition previousBlend = default;
            bool restoreBlend = false;
            if (brain != null)
            {
                previousBlend = brain.DefaultBlend;
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blendIn);
                restoreBlend = true;
            }

            cam.Lens.FieldOfView = fieldOfView;
            follow.FollowOffset = offset;
            cam.gameObject.SetActive(true);

            Time.timeScale = timeScale;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                // Time.timeScale may have been grabbed by a pause menu meanwhile; stay out of its way.
                if (Mathf.Approximately(Time.timeScale, 0f)) { yield return null; continue; }

                elapsed += Time.unscaledDeltaTime;
                offset = Quaternion.AngleAxis(orbitSpeed * Time.unscaledDeltaTime, Vector3.up) * offset;
                follow.FollowOffset = offset;
                yield return null;
            }

            // Only restore if nobody else (pause menu, hit-stop) changed it meanwhile.
            if (Mathf.Approximately(Time.timeScale, timeScale)) Time.timeScale = 1f;

            if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blendOut);
            cam.gameObject.SetActive(false);

            // Give the blend-out time to finish on the slower setting, then restore the brain's own blend.
            if (restoreBlend)
            {
                yield return new WaitForSecondsRealtime(blendOut);
                if (brain != null) brain.DefaultBlend = previousBlend;
            }

            running = null;
        }
    }
}
