using UnityEngine;

namespace Platformer
{
    [CreateAssetMenu(menuName = "Inventory/Weapon", fileName = "NewWeapon")]
    public class WeaponData : ItemData
    {
        [Header("Weapon — Slot")]
        [Tooltip("Which weapon slot this item equips into. Pick Weapon for a primary, " +
                 "SecondaryWeapon for an off-hand, or Shield.")]
        [field: SerializeField] public EquipSlot slot { get; private set; } = EquipSlot.Weapon;

        [Header("Weapon — Visual")]
        [field: SerializeField] public GameObject weaponPrefab { get; private set; }
        [Tooltip("Extra local position applied on top of the prefab's root transform when it spawns in the hand socket.")]
        [field: SerializeField] public Vector3 gripPositionOffset { get; private set; } = Vector3.zero;
        [Tooltip("Extra local rotation (euler degrees) applied on top of the prefab's root transform when it spawns in the hand socket.")]
        [field: SerializeField] public Vector3 gripRotationOffset { get; private set; } = Vector3.zero;

        [Header("Weapon — Stats")]
        [field: SerializeField, Min(0)] public int damage { get; private set; } = 10;
        [field: SerializeField, Min(0f)] public float attackSpeed { get; private set; } = 1f;
        [field: SerializeField, Min(0f)] public float range { get; private set; } = 1f;
        [field: SerializeField, Min(0f)] public float knockback { get; private set; } = 0.5f;
    }
}
