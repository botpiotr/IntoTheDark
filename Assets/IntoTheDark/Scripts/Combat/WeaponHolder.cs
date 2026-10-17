using IntoTheDark.Core;
using IntoTheDark.Items;
using IntoTheDark.Player;
using UnityEngine;

namespace IntoTheDark.Combat
{
    /// <summary>
    /// Armes du joueur : molette pour passer de l'une à l'autre (l'arme se range puis l'autre se lève),
    /// clic gauche pour l'utiliser. Pas d'attaque pendant un choc de stress, la mort ou le jeu en pause.
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    public class WeaponHolder : MonoBehaviour
    {
        [SerializeField, Tooltip("Caméra du joueur : origine de la visée")] private Transform aim;
        [SerializeField] private Weapon[] weapons = new Weapon[0];
        [SerializeField, Min(0.01f), Tooltip("Durée pour ranger, puis pour sortir une arme (s)")] private float switchTime = 0.18f;
        [SerializeField, Tooltip("Abaissement de l'arme rangée (m)")] private float loweredOffset = 0.35f;

        private PlayerInputHandler input;
        private PlayerHealth health;
        private PlayerMovement movement;
        private Vector3[] restPositions;
        private int current;
        private int pending = -1;
        private float switchTimer;
        private bool lowering;

        public Transform Aim => aim;
        public Inventory Inventory { get; private set; }
        /// <summary>Endurance du joueur (partagée avec la course et le saut), invisible.</summary>
        public Stamina Stamina => movement != null ? movement.Stamina : null;
        public Weapon Current => weapons.Length > 0 ? weapons[current] : null;
        public bool IsSwitching => switchTimer > 0f;

        private void Awake()
        {
            input = GetComponent<PlayerInputHandler>();
            health = GetComponent<PlayerHealth>();
            movement = GetComponent<PlayerMovement>();
            Inventory = GetComponent<Inventory>();
            if (aim == null && Camera.main != null) aim = Camera.main.transform;

            restPositions = new Vector3[weapons.Length];
            for (int i = 0; i < weapons.Length; i++)
            {
                restPositions[i] = weapons[i].transform.localPosition;
                weapons[i].Init(this);
                weapons[i].gameObject.SetActive(i == current);
            }
        }

        private void Start()
        {
            if (Current != null)
            {
                BeginRaise();
                Current.OnEquipped();
            }
        }

        private void Update()
        {
            if (weapons.Length == 0) return;
            bool canAct = Time.timeScale > 0f && (health == null || !health.IsDead);

            if (canAct)
            {
                int step = input.WeaponScroll;
                if (step != 0 && weapons.Length > 1)
                {
                    int from = pending >= 0 ? pending : current;
                    pending = ((from + step) % weapons.Length + weapons.Length) % weapons.Length;
                    if (pending == current && !lowering) pending = -1;
                    else if (!lowering) { lowering = true; switchTimer = switchTime; }
                }
            }

            UpdateSwitch(Time.deltaTime);

            bool frozen = movement != null && movement.IsFrozen;
            if (canAct && !frozen && !IsSwitching)
            {
                if (input.AttackPressed) Current.Use();
                else if (input.AttackHeld) Current.UseHeld();
            }
        }

        private void UpdateSwitch(float dt)
        {
            if (switchTimer <= 0f) return;
            switchTimer = Mathf.Max(0f, switchTimer - dt);
            float t = 1f - switchTimer / switchTime;
            float down = lowering ? t : 1f - t;
            var weapon = weapons[current].transform;
            weapon.localPosition = restPositions[current] + Vector3.down * (loweredOffset * Mathf.SmoothStep(0f, 1f, down));

            if (switchTimer > 0f || !lowering) return;

            // Arme rangée : on sort la suivante.
            weapons[current].OnUnequipped();
            weapons[current].transform.localPosition = restPositions[current];
            weapons[current].gameObject.SetActive(false);
            current = pending >= 0 ? pending : current;
            pending = -1;
            weapons[current].gameObject.SetActive(true);
            BeginRaise();
            weapons[current].OnEquipped();
        }

        private void BeginRaise()
        {
            lowering = false;
            switchTimer = switchTime;
            weapons[current].transform.localPosition = restPositions[current] + Vector3.down * loweredOffset;
        }

        /// <summary>Le collider appartient-il au joueur ? (les armes ne se touchent pas elles-mêmes)</summary>
        public bool IsOwnCollider(Collider other) => other != null && other.transform.IsChildOf(transform);
    }
}
