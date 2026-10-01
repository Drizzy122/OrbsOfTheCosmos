using KBCore.Refs;
using UnityEngine;

namespace Platformer
{
    public class PlayerInteraction : ValidatedMonoBehaviour
    {
        [field: Header("References")]
        [field: SerializeField, Anywhere] InputReader input;

        [Header("Interact Settings")]
        [SerializeField] float interactDistance = 5f;

        [Header("Teleport Settings")]
        public bool isTeleporting;

      
        void OnInteract(bool performed)
        {
            if (!performed) return;

            foreach (var interactable in FindObjectsByType<Interactable>())
            {
                if (Vector3.Distance(transform.position, interactable.transform.position) < interactDistance)
                {
                    interactable.Interact();
                    break;
                }
            }
        }
        
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.ghostWhite;
            Gizmos.DrawWireSphere(transform.position, interactDistance);
        }
        
        void OnEnable() => input.interact += OnInteract;
        void OnDisable() => input.interact -= OnInteract;
    }
}
