using System.Collections;
using System.Collections.Generic;
using Platformer;
using UnityEngine;

public class FractureObject : MonoBehaviour, IDamageable
{
    [field: Header("Health")]
    [field: SerializeField] float maxHealth = 30f;
    float currentHealth;

    void Awake() => currentHealth = maxHealth;

    bool exploded;

    public void TakeDamage(float damage, float knockBackTime = 0f)
    {
        if (exploded) return;
        currentHealth -= damage;
        if (currentHealth <= 0f)
            Explode();
    }

    [field: Header("Refs")]
    [field:SerializeField] GameObject originalObject;
    [field: SerializeField] MeshRenderer meshRenderer;
    [field:SerializeField] GameObject fracturedObject;
    [field:SerializeField] GameObject explosionVFX;

    [field: Header("Explosion")]
    [field:SerializeField] float epxlosionMinForce = 5;
    [field:SerializeField] float explosionMaxForce = 100;
    [field:SerializeField] float explosionForceRadius = 10;
    [field:SerializeField] float fragScaleFactor = 1;

    private GameObject fractObj;
    public void Explode()
    {
        if (exploded) return;
        exploded = true;

        if (originalObject != null)
        {
            if (meshRenderer != null) meshRenderer.enabled = false;

            // Dead props can't be hit again (the object lingers while the pieces shrink).
            foreach (var col in originalObject.GetComponentsInChildren<Collider>())
                col.enabled = false;

            if (explosionVFX != null)
            {
                GameObject exploVFX = Instantiate(explosionVFX, transform.position, Quaternion.identity);
                Destroy(exploVFX, 7);
            }

            // The fractured prefab must be a pile of pieces, not another breakable
            // prop: spawning a copy of ourselves would respawn the target forever.
            if (fracturedObject != null && fracturedObject.GetComponentInChildren<FractureObject>(true) != null)
            {
                Debug.LogWarning($"[FractureObject] '{name}': Fractured Object points at a breakable object ('{fracturedObject.name}'), not at broken pieces. Ignoring it.", this);
                fracturedObject = null;
            }

            if (fracturedObject != null)
            {
                fractObj = Instantiate(fracturedObject, transform.position, Quaternion.identity);

                foreach (Transform t in fractObj.transform)
                {
                    var rb = t.GetComponent<Rigidbody>();
                    if (rb != null)
                        rb.AddExplosionForce(Random.Range(epxlosionMinForce, explosionMaxForce), originalObject.transform.position, explosionForceRadius);

                    StartCoroutine(Shrink(t, 2));
                }

                AudioManager.instance.PlayOneShot(FMODEvents.instance.explode, transform.position);
                Destroy(fractObj, 5f);
                Destroy(originalObject, 5f);
            }
            else
            {
                // No fracture prefab — just destroy the rock cleanly
                AudioManager.instance.PlayOneShot(FMODEvents.instance.explode, transform.position);
                Destroy(originalObject);
            }
        }
    }
    IEnumerator Shrink (Transform t, float delay)
    {
        yield return new WaitForSeconds(delay);

        Vector3 newScale = t.localScale;

        while(newScale.x >= 0)
        {
            newScale -= new Vector3(fragScaleFactor, fragScaleFactor, fragScaleFactor);

            t.localScale = newScale;
            yield return new WaitForSeconds (0.05f);
        }
    }
}
