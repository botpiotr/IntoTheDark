using System;
using System.Collections.Generic;
using IntoTheDark.Core;
using UnityEngine;

namespace IntoTheDark.Combat
{
    /// <summary>
    /// Barre de fer : enchaînement de 3 coups rapides — taille horizontale, taille verticale, fente.
    /// Chaque coup touche tous les ennemis de sa zone et les repousse.
    /// Les coups s'enchaînent sans interruption : un clic pendant un coup lance le suivant dès la frappe
    /// terminée (le retour en garde est coupé). L'enchaînement repart du premier coup si on n'attaque pas
    /// dans les 0,5 s qui suivent la fin d'un coup. Maintenir le clic enchaîne les coups.
    /// Endurance (invisible, partagée avec la course) : 2 enchaînements complets la vident ; épuisé,
    /// chaque coup prend deux fois plus de temps, jusqu'à ce qu'elle remonte.
    /// </summary>
    public class IronBar : Weapon
    {
        [Serializable]
        private struct Move
        {
            public string name;
            [Tooltip("Direction de la barre au début du coup (repère de la main)")] public Vector3 from;
            [Tooltip("Direction de la barre à la fin du coup")] public Vector3 to;
            [Tooltip("Avancée de la main pendant le coup (m)")] public float lunge;
            [Tooltip("Portée depuis les yeux (m)")] public float range;
            [Tooltip("Demi-angle de la zone touchée (degrés)")] public float halfAngle;
            [Tooltip("Recul infligé (m)")] public float knockback;
        }

        [SerializeField] private float damage = 5f;
        [SerializeField, Min(1), Tooltip("Nombre d'enchaînements complets pour vider l'endurance")] private int combosToExhaust = 2;
        [SerializeField, Min(1f), Tooltip("Durée des coups multipliée par ce facteur quand l'endurance est épuisée")]
        private float exhaustedSlowdown = 2f;
        [SerializeField, Min(0f), Tooltip("Sans nouvelle attaque pendant ce délai après la fin d'un coup, l'enchaînement repart du début")]
        private float comboResetDelay = 0.5f;
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("Animation (s)")]
        [SerializeField] private Vector3 restDirection = new Vector3(-0.25f, 1f, 0.45f);
        [SerializeField] private float windupTime = 0.05f;
        [SerializeField] private float strikeTime = 0.09f;
        [SerializeField, Tooltip("Fin de coup ; le coup suivant peut partir dès la fin de ce temps")] private float holdTime = 0.06f;
        [SerializeField] private float recoverTime = 0.14f;

        [SerializeField] private Move[] moves =
        {
            new Move { name = "Taille horizontale", from = new Vector3(1f, 0.6f, 0.5f), to = new Vector3(-1f, 0.45f, 0.5f),
                lunge = 0.05f, range = 2.1f, halfAngle = 75f, knockback = 1.5f },
            new Move { name = "Taille verticale", from = new Vector3(0.1f, 1f, -0.25f), to = new Vector3(0f, -0.1f, 1f),
                lunge = 0.08f, range = 2.1f, halfAngle = 35f, knockback = 1.5f },
            new Move { name = "Fente", from = new Vector3(0.05f, 0.08f, 1f), to = new Vector3(0.02f, 0.05f, 1f),
                lunge = 0.4f, range = 2.7f, halfAngle = 22f, knockback = 2f },
        };

        private readonly Collider[] overlaps = new Collider[64];
        private readonly HashSet<IDamageable> struck = new HashSet<IDamageable>();
        private Vector3 restPosition;
        private Vector3 currentDirection;
        private float currentLunge;
        private Vector3 startDirection;
        private float startLunge;
        private float tempo = 1f;
        private bool queued;
        private float swingTime = -1f;
        private float animationEnd = float.NegativeInfinity;
        private int nextMove;
        private int currentMove;
        private bool hitDone;

        private float SwingDuration => windupTime + strikeTime + holdTime + recoverTime;
        /// <summary>Instant du coup à partir duquel le suivant peut partir.</summary>
        private float ChainPoint => windupTime + strikeTime + holdTime;

        public override void Init(WeaponHolder holder)
        {
            base.Init(holder);
            if (model != null) restPosition = model.localPosition;
            currentDirection = restDirection.normalized;
        }

        public override void Use()
        {
            if (moves.Length == 0) return;
            if (swingTime >= 0f && swingTime < ChainPoint)
            {
                queued = true; // clic pendant la frappe : le coup suivant partira dès que possible
                return;
            }
            StartMove();
        }

        /// <summary>Clic maintenu : enchaîne dès que possible.</summary>
        public override void UseHeld() => Use();

        private void StartMove()
        {
            bool chaining = swingTime >= 0f;
            if (!chaining && Time.time - animationEnd > comboResetDelay) nextMove = 0; // pause trop longue : on repart du début

            // Endurance : un coup coûte 1/(3 × 2) de la jauge ; épuisé, le coup est deux fois plus lent.
            var stamina = Holder != null ? Holder.Stamina : null;
            bool exhausted = stamina != null && stamina.IsExhausted;
            if (stamina != null && !exhausted)
                stamina.Drain(stamina.Max / (moves.Length * Mathf.Max(1, combosToExhaust)));
            tempo = exhausted ? 1f / exhaustedSlowdown : 1f;

            currentMove = nextMove;
            nextMove = (nextMove + 1) % moves.Length;
            startDirection = currentDirection; // part de la pose actuelle (enchaînement fluide)
            startLunge = currentLunge;
            swingTime = 0f;
            hitDone = false;
            queued = false;
        }

        public override void OnUnequipped()
        {
            swingTime = -1f;
            queued = false;
            nextMove = 0;
            Pose(restDirection, 0f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (model == null) return;

            if (swingTime < 0f)
            {
                Pose(restDirection, 0f);
                return;
            }

            swingTime += dt * tempo;
            if (queued && swingTime >= ChainPoint) StartMove();

            var move = moves[currentMove];
            float strikeStart = windupTime;
            float strikeEnd = strikeStart + strikeTime;
            float holdEnd = strikeEnd + holdTime;

            if (swingTime < strikeStart)
            {
                float t = EaseOut(swingTime / windupTime);
                Pose(Vector3.Slerp(startDirection, move.from.normalized, t), Mathf.Lerp(startLunge, 0f, t));
            }
            else if (swingTime < strikeEnd)
            {
                float t = EaseOut((swingTime - strikeStart) / strikeTime);
                Pose(Vector3.Slerp(move.from.normalized, move.to.normalized, t), move.lunge * t);
            }
            else if (swingTime < holdEnd)
            {
                Pose(move.to, move.lunge);
            }
            else
            {
                float t = EaseInOut((swingTime - holdEnd) / recoverTime);
                Pose(Vector3.Slerp(move.to.normalized, restDirection.normalized, t), move.lunge * (1f - t));
            }

            if (!hitDone && swingTime >= strikeStart + strikeTime * 0.5f)
            {
                hitDone = true;
                Strike(move);
            }

            if (swingTime >= SwingDuration)
            {
                swingTime = -1f;
                animationEnd = Time.time;
            }
        }

        private void Pose(Vector3 direction, float lunge)
        {
            currentDirection = direction.normalized;
            currentLunge = lunge;
            model.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            model.localPosition = restPosition + Vector3.forward * lunge;
        }

        /// <summary>Touche tous les ennemis dans le cône du coup, et les repousse loin du joueur.</summary>
        private void Strike(Move move)
        {
            var aim = Holder != null ? Holder.Aim : null;
            if (aim == null) return;

            Vector3 eye = aim.position;
            int count = Physics.OverlapSphereNonAlloc(eye, move.range, overlaps, hitMask, QueryTriggerInteraction.Ignore);
            struck.Clear();
            float minDot = Mathf.Cos(move.halfAngle * Mathf.Deg2Rad);
            for (int i = 0; i < count; i++)
            {
                var other = overlaps[i];
                if (Holder.IsOwnCollider(other)) continue;
                var target = other.GetComponentInParent<IDamageable>();
                if (target == null || struck.Contains(target)) continue;

                // Dans le cône du coup ? (point le plus proche du collider, pour les grandes cibles)
                Vector3 closest = other.ClosestPoint(eye);
                Vector3 toTarget = closest - eye;
                if (toTarget.sqrMagnitude > 0.0001f && Vector3.Dot(aim.forward, toTarget.normalized) < minDot)
                {
                    // Cible très proche mais légèrement hors cône : on vérifie aussi son centre.
                    Vector3 toCenter = other.bounds.center - eye;
                    if (Vector3.Dot(aim.forward, toCenter.normalized) < minDot) continue;
                }

                struck.Add(target);
                target.TakeDamage(damage, eye);

                if (target is Component component && component.TryGetComponent(out IKnockbackable knockable))
                {
                    Vector3 away = component.transform.position - eye;
                    away.y = 0f;
                    if (away.sqrMagnitude < 0.0001f) away = aim.forward;
                    away.y = 0f;
                    knockable.Knockback(away.normalized * move.knockback);
                }
            }
        }

        private static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }

        private static float EaseInOut(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
    }
}
