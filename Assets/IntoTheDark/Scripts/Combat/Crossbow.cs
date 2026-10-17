using IntoTheDark.Items;
using UnityEngine;

namespace IntoTheDark.Combat
{
    /// <summary>
    /// Arbalète compacte : tire un carreau, puis se recharge toute seule si l'inventaire en contient :
    /// elle plonge hors de l'écran, y reste le temps du rechargement, et remonte chargée (0,8 s en tout).
    /// </summary>
    public class Crossbow : Weapon
    {
        [SerializeField] private ItemDefinition boltItem;
        [SerializeField] private Bolt boltPrefab;
        [SerializeField, Tooltip("Point de départ du carreau")] private Transform muzzle;
        [SerializeField] private float damage = 5f;
        [SerializeField] private float boltSpeed = 45f;
        [SerializeField] private float maxAimDistance = 100f;
        [SerializeField] private LayerMask aimMask = ~0;

        [Header("Rechargement")]
        [SerializeField, Min(0.1f), Tooltip("Durée totale pendant laquelle l'arbalète disparaît")] private float reloadTime = 0.8f;
        [SerializeField, Min(0.01f), Tooltip("Durée de la descente, puis de la remontée")] private float dropTime = 0.12f;
        [SerializeField, Tooltip("Distance de la plongée hors de l'écran (m)")] private float dropDistance = 0.3f;
        [SerializeField, Tooltip("Pause entre le tir et la plongée")] private float reloadDelay = 0.08f;

        [Header("Recul")]
        [SerializeField] private float recoilDistance = 0.03f;
        [SerializeField] private float recoilAngle = 10f;
        [SerializeField] private float recoilTime = 0.1f;

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private Vector3 modelRestPosition;
        private bool loaded;
        private bool reloading;
        private float reloadTimer;
        private float delayTimer;
        private float recoilTimer;

        public bool IsLoaded => loaded;
        public bool IsReloading => reloading;

        public override void Init(WeaponHolder holder)
        {
            base.Init(holder);
            if (model != null) modelRestPosition = model.localPosition;
            loaded = TryTakeBolt(); // sort de l'inventaire déjà chargée
        }

        public override void OnEquipped()
        {
            // Rechargement interrompu par un changement d'arme : elle reste hors de l'écran et reprend
            // (le carreau est déjà pris).
            if (reloading) reloadTimer = Mathf.Min(reloadTimer, dropTime);
        }

        public override void Use()
        {
            if (!loaded || reloading) return;
            Fire();
        }

        private void Fire()
        {
            var aim = Holder.Aim;
            Vector3 origin = muzzle != null ? muzzle.position : aim.position + aim.forward * 0.4f;
            Vector3 direction = AimPoint(aim) - origin;
            if (direction.sqrMagnitude < 0.01f || Vector3.Dot(direction, aim.forward) <= 0f) direction = aim.forward;

            if (boltPrefab != null)
            {
                var bolt = Instantiate(boltPrefab, origin, Quaternion.LookRotation(direction));
                bolt.Launch(direction.normalized * boltSpeed, damage, boltItem, Holder);
            }

            loaded = false;
            recoilTimer = recoilTime;
            delayTimer = reloadDelay;
        }

        /// <summary>Point visé par le réticule (premier obstacle au centre de l'écran).</summary>
        private Vector3 AimPoint(Transform aim)
        {
            int count = Physics.RaycastNonAlloc(aim.position, aim.forward, hits, maxAimDistance, aimMask, QueryTriggerInteraction.Ignore);
            float best = maxAimDistance;
            for (int i = 0; i < count; i++)
            {
                if (Holder.IsOwnCollider(hits[i].collider)) continue;
                if (hits[i].distance < best) best = hits[i].distance;
            }
            return aim.position + aim.forward * best;
        }

        private bool TryTakeBolt()
        {
            var inventory = Holder != null ? Holder.Inventory : null;
            return inventory != null && boltItem != null && inventory.TryRemove(boltItem, 1);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (!loaded && !reloading)
            {
                if (delayTimer > 0f) delayTimer -= dt;
                else if (TryTakeBolt()) { reloading = true; reloadTimer = 0f; }
            }

            float drop = 0f;
            if (reloading)
            {
                reloadTimer += dt;
                float rise = reloadTime - dropTime;
                drop = reloadTimer < dropTime ? reloadTimer / dropTime
                    : reloadTimer < rise ? 1f
                    : 1f - (reloadTimer - rise) / dropTime;
                if (reloadTimer >= reloadTime)
                {
                    reloading = false;
                    loaded = true;
                    drop = 0f;
                }
            }

            recoilTimer = Mathf.Max(0f, recoilTimer - dt);
            float kick = recoilTime > 0f ? recoilTimer / recoilTime : 0f;
            if (model == null) return;
            model.localPosition = modelRestPosition + Vector3.back * (recoilDistance * kick)
                                  + Vector3.down * (dropDistance * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(drop)));
            model.localRotation = Quaternion.Euler(-recoilAngle * kick + 25f * Mathf.Clamp01(drop), 0f, 0f);
        }
    }
}
