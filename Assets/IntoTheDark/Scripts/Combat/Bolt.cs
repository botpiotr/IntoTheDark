using IntoTheDark.Core;
using IntoTheDark.Creatures;
using IntoTheDark.Items;
using UnityEngine;

namespace IntoTheDark.Combat
{
    /// <summary>
    /// Carreau d'arbalète en vol. Touche la première chose sur sa trajectoire :
    /// - une créature : dégâts, le carreau est perdu ;
    /// - autre chose (mur, mannequin…) : dégâts éventuels, il s'y plante et se ramasse avec E.
    /// </summary>
    public class Bolt : MonoBehaviour, IInteractable
    {
        [SerializeField, Tooltip("Gravité appliquée au carreau (fraction de la gravité normale)")] private float gravityScale = 0.15f;
        [SerializeField] private float maxFlightTime = 4f;
        [SerializeField, Tooltip("Longueur enfoncée dans la cible (m)")] private float embedDepth = 0.08f;
        [SerializeField] private Collider pickupCollider;
        [SerializeField] private LayerMask hitMask = ~0;

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private Vector3 velocity;
        private float damage;
        private ItemDefinition item;
        private WeaponHolder owner;
        private float flightTime;
        private bool flying;

        public bool IsStuck { get; private set; }

        private void Awake()
        {
            if (pickupCollider == null) pickupCollider = GetComponent<Collider>();
            if (pickupCollider != null) pickupCollider.enabled = false;
        }

        public void Launch(Vector3 initialVelocity, float hitDamage, ItemDefinition boltItem, WeaponHolder shooter)
        {
            velocity = initialVelocity;
            damage = hitDamage;
            item = boltItem;
            owner = shooter;
            flying = true;
        }

        private void Update()
        {
            if (!flying) return;
            float dt = Time.deltaTime;
            flightTime += dt;
            if (flightTime > maxFlightTime) { Destroy(gameObject); return; }

            velocity += Physics.gravity * (gravityScale * dt);
            Vector3 step = velocity * dt;
            float distance = step.magnitude;
            if (distance < 1e-5f) return;

            Vector3 tip = transform.position + transform.forward * Length();
            if (TryHit(tip, step / distance, distance, out RaycastHit hit))
            {
                OnImpact(hit, step / distance);
                return;
            }
            transform.position += step;
            transform.rotation = Quaternion.LookRotation(velocity);
        }

        private bool TryHit(Vector3 from, Vector3 direction, float distance, out RaycastHit result)
        {
            result = default;
            int count = Physics.RaycastNonAlloc(from, direction, hits, distance, hitMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (owner != null && owner.IsOwnCollider(hits[i].collider)) continue;
                if (hits[i].distance < best) { best = hits[i].distance; result = hits[i]; }
            }
            return best < float.MaxValue;
        }

        private void OnImpact(RaycastHit hit, Vector3 direction)
        {
            flying = false;
            var target = hit.collider.GetComponentInParent<IDamageable>();
            target?.TakeDamage(damage, hit.point - direction);

            // Dans une créature, le carreau est perdu.
            if (hit.collider.GetComponentInParent<Lurker>() != null)
            {
                Destroy(gameObject);
                return;
            }

            // Ailleurs, il se plante et peut être ramassé.
            transform.position = hit.point - direction * (Length() - embedDepth);
            transform.rotation = Quaternion.LookRotation(direction);
            // Planté dans une cible qui peut bouger : il la suit (jamais un mur, dont l'échelle déformerait le carreau).
            if (target is Component component) transform.SetParent(component.transform, true);
            IsStuck = true;
            if (pickupCollider != null) pickupCollider.enabled = true;
        }

        /// <summary>Le pivot du carreau est à sa pointe arrière : la pointe est à Length() devant.</summary>
        private static float Length() => 0.3f;

        // ---------- Ramassage (E) ----------

        public bool CanInteract(GameObject interactor)
        {
            if (!IsStuck || item == null) return false;
            var inventory = interactor.GetComponent<Inventory>();
            return inventory != null && inventory.HasRoomFor(item);
        }

        public void Interact(GameObject interactor)
        {
            var inventory = interactor.GetComponent<Inventory>();
            if (inventory == null || inventory.Add(item, 1) > 0) return;
            Destroy(gameObject);
        }
    }
}
