using System;
using IntoTheDark.Cycle;
using IntoTheDark.Lighting;
using IntoTheDark.Player;
using UnityEngine;
using UnityEngine.AI;

namespace IntoTheDark.Creatures
{
    /// <summary>
    /// Rôdeur mutant. Seuls ses yeux rouges se voient dans le noir. Plus rapide que le joueur.
    ///
    /// Créature errante (calme) :
    /// - Errance : marche lentement dans une direction au hasard, en change, récupère vite sa vie.
    /// - Traque (vie > 50 %) : s'approche du joueur et le fixe au bord de sa lumière ; si le joueur
    ///   est dans le noir, il fonce et frappe.
    /// - Fuite (vie ≤ 50 %) : s'enfuit ; repasse en errance après 5 s à plus de 10 m du joueur.
    /// - Panique (vie ≤ 50 %, fuite bloquée) : attaque le joueur, même éclairé, puis fuit ailleurs.
    ///
    /// Assaut (créature de tempête, ou toutes pendant l'apocalypse) : fonce sur le joueur, ou sur un
    /// phare allumé nettement plus proche, et frappe même dans la lumière (lampe ou phare), jusqu'à la mort.
    ///
    /// Hors assaut, la lumière des phares est infranchissable : il la contourne, s'arrête à son bord
    /// et, s'il s'y retrouve quand même, en ressort au plus vite.
    ///
    /// Lumière : au plus un coup par seconde. Lampe : 1 % de la vie max à chaque seconde passée
    /// dans sa lumière. Phare : 70 % de la vie max par seconde (mort en 2 s).
    /// Chaque coup reçu : flash blanc et petit bond en l'air et en arrière.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class Lurker : MonoBehaviour
    {
        public enum State { Wander, Stalk, Flee, Panic, Assault, Dying }

        [Header("Vie")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField, Range(0f, 1f), Tooltip("Part de la vie max perdue à chaque coup de la lumière d'un phare (un coup par intervalle)")]
        private float beaconDamageFraction = 0.7f;
        [SerializeField, Range(0f, 1f), Tooltip("Part de la vie perdue à chaque seconde passée dans la lumière de la lampe")]
        private float lanternDamagePerSecond = 0.01f;
        [SerializeField, Min(0.1f), Tooltip("Dans la lumière, au plus un coup par intervalle (s)")]
        private float lightHitInterval = 1f;
        [SerializeField, Min(0f), Tooltip("Vie récupérée par seconde en errance, dans le noir")]
        private float wanderRegenPerSecond = 15f;
        [SerializeField, Range(0f, 1f), Tooltip("En dessous (ou égal) : fuite")] private float fleeThreshold = 0.5f;

        [Header("Vitesses (m/s) — le sprint du joueur est à 6")]
        [SerializeField] private float wanderSpeed = 1.8f;
        [SerializeField] private float stalkSpeed = 7f;
        [SerializeField] private float fleeSpeed = 7.5f;
        [SerializeField] private float panicSpeed = 8f;
        [SerializeField] private float assaultSpeed = 7.5f;
        [SerializeField, Tooltip("Multiplicateur de vitesse et de dégâts dans le noir pendant l'apocalypse")]
        private float apocalypseBoost = 1.25f;

        [Header("Perception")]
        [SerializeField] private float detectionRange = 22f;
        [SerializeField, Tooltip("Distance gardée au-delà du bord de la lumière en traque")] private float stalkMargin = 1.2f;
        [SerializeField, Tooltip("Secondes entre deux recalculs de trajet")] private float repathInterval = 0.1f;
        [SerializeField, Tooltip("Le joueur est préféré à un phare, sauf si le phare est au moins autant de fois plus proche")]
        private float playerPriority = 1.5f;
        [SerializeField, Tooltip("Distance gardée autour de la lumière des phares quand il la contourne")]
        private float beaconAvoidMargin = 1f;
        [SerializeField, Tooltip("Avance (degrés) du point de contournement autour de la lumière d'un phare")]
        private float detourStep = 25f;

        [Header("Fuite")]
        [SerializeField] private float safeDistance = 10f;
        [SerializeField] private float safeDuration = 5f;
        [SerializeField] private float fleeStep = 10f;

        [Header("Attaque")]
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private float attackDamage = 5f;
        [SerializeField, Min(1f), Tooltip("Au plus un coup par seconde et par monstre")] private float attackCooldown = 1f;
        [SerializeField, Tooltip("Durée max de la charge de panique avant d'abandonner")] private float panicTimeout = 3f;

        [Header("Apparence")]
        [SerializeField] private float deathDuration = 0.5f;
        [SerializeField, Tooltip("Partie visible (bondit et flashe quand il est touché)")] private Transform bodyVisual;
        [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.15f;
        [SerializeField, ColorUsage(false, true)] private Color hitFlashColor = new Color(2.5f, 2.5f, 2.5f);
        [SerializeField] private float hitHopHeight = 0.35f;
        [SerializeField, Tooltip("Recul en mètres")] private float hitKnockback = 0.8f;
        [SerializeField, Min(0.05f)] private float hitHopDuration = 0.3f;

        [Header("État (lecture seule)")]
        [SerializeField] private State state = State.Wander;
        [SerializeField] private float health;

        private NavMeshAgent agent;
        private PlayerHealth player;
        private PlayerStress playerStress;
        private PlayerLightState playerLight;

        private float attackTimer;
        private float repathTimer;
        private float wanderTimer;
        private Vector3 wanderDirection;
        private float safeTimer;
        private float panicTimer;
        private float stuckTimer;
        private float beaconHitTimer;
        private float detourSign;
        private NavMeshPath scratchPath;
        private readonly Vector3[] corners = new Vector3[32];
        private float deathTimer;
        private Vector3 baseScale;
        private Vector3 lastFleeDirection;
        private float lightHitTimer;
        private float hitFlashTimer;
        private float hopTimer;
        private Vector3 knockDirection;
        private Vector3 bodyBasePosition;
        private Renderer[] bodyRenderers;
        private Color[] baseEmissions;
        private MaterialPropertyBlock propertyBlock;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        /// <summary>Créature de tempête : toujours à l'assaut.</summary>
        public bool ForcedAssault { get; set; }
        public State CurrentState => state;
        public bool IsDead => state == State.Dying;
        public float HealthNormalized => health / maxHealth;
        public event Action<Lurker> Died;
        /// <summary>(ancien mode, nouveau mode)</summary>
        public event Action<State, State> StateChanged;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            scratchPath = new NavMeshPath();
            health = maxHealth;
            baseScale = transform.localScale;
            wanderDirection = RandomFlatDirection();

            if (bodyVisual == null) bodyVisual = transform.Find("Body");
            if (bodyVisual != null)
            {
                bodyBasePosition = bodyVisual.localPosition;
                bodyRenderers = bodyVisual.GetComponentsInChildren<Renderer>();
            }
            else
            {
                bodyRenderers = GetComponentsInChildren<Renderer>();
            }
            propertyBlock = new MaterialPropertyBlock();
            baseEmissions = new Color[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                var material = bodyRenderers[i].sharedMaterial;
                baseEmissions[i] = material != null && material.HasProperty(EmissionColorId)
                    ? material.GetColor(EmissionColorId)
                    : Color.black;
            }
        }

        public void Init(PlayerHealth target)
        {
            player = target;
            if (player != null)
            {
                playerStress = player.GetComponent<PlayerStress>();
                playerLight = player.GetComponent<PlayerLightState>();
            }
        }

        private Vector3 BodyPoint => transform.position + Vector3.up;
        private bool Wounded => health <= maxHealth * fleeThreshold;

        private void Update()
        {
            float dt = Time.deltaTime;
            UpdateHitFeedback(dt);
            if (state == State.Dying)
            {
                UpdateDeath(dt);
                return;
            }

            ApplyLight(dt);
            if (state == State.Dying) return;

            attackTimer -= dt;
            repathTimer -= dt;

            var cycle = CycleManager.Instance;
            bool apocalypse = cycle != null && cycle.Phase == CyclePhase.Apocalypse;
            float boost = apocalypse && !LightZone.IsLit(BodyPoint) ? apocalypseBoost : 1f;

            if (ForcedAssault || apocalypse)
            {
                UpdateAssault(boost);
                return;
            }

            // Hors assaut, il n'entre jamais volontairement dans la lumière d'un phare :
            // s'il s'y trouve quand même (phare qui s'allume, recul…), il en sort au plus vite.
            var beacon = Beacon.FindCovering(BodyPoint);
            if (beacon != null) EscapeBeacon(beacon);
            else UpdateWanderer(dt);
        }

        // ---------- Lumière ----------

        private void ApplyLight(float dt)
        {
            Vector3 body = BodyPoint;

            // Phare : 70 % de la vie max par coup, au plus un coup par intervalle (mort en 2 s).
            var beacon = Beacon.FindCovering(body);
            if (beacon != null)
            {
                lightHitTimer = 0f;
                beaconHitTimer += dt;
                if (beaconHitTimer >= lightHitInterval)
                {
                    beaconHitTimer -= lightHitInterval;
                    TakeDamage(maxHealth * beaconDamageFraction, beacon.Zone.Center);
                }
                return;
            }
            beaconHitTimer = 0f;

            // Lampe : 1 % de la vie par seconde passée dans sa lumière, en un coup par seconde.
            var zone = LightZone.FindCovering(body);
            if (zone == null)
            {
                lightHitTimer = 0f;
                return;
            }
            lightHitTimer += dt;
            if (lightHitTimer >= lightHitInterval)
            {
                lightHitTimer -= lightHitInterval;
                TakeDamage(maxHealth * lanternDamagePerSecond * lightHitInterval, zone.Center);
            }
        }

        /// <summary>Coup reçu, venant de derrière la créature (source inconnue).</summary>
        public void TakeDamage(float amount) => TakeDamage(amount, transform.position + transform.forward);

        /// <summary>Coup reçu depuis une source : flash blanc, petit bond à l'opposé de la source.</summary>
        public void TakeDamage(float amount, Vector3 source)
        {
            if (state == State.Dying || amount <= 0f) return;
            health -= amount;

            hitFlashTimer = hitFlashDuration;
            hopTimer = hitHopDuration;
            knockDirection = transform.position - source;
            knockDirection.y = 0f;
            knockDirection = knockDirection.sqrMagnitude > 0.001f ? knockDirection.normalized : -transform.forward;

            if (health <= 0f) Die();
        }

        private void UpdateHitFeedback(float dt)
        {
            if (hitFlashTimer > 0f || hitFlashTimer > -dt)
            {
                hitFlashTimer -= dt;
                float k = Mathf.Clamp01(hitFlashTimer / hitFlashDuration);
                for (int i = 0; i < bodyRenderers.Length; i++)
                {
                    if (bodyRenderers[i] == null) continue;
                    bodyRenderers[i].GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor(EmissionColorId, Color.Lerp(baseEmissions[i], hitFlashColor, k));
                    bodyRenderers[i].SetPropertyBlock(propertyBlock);
                }
            }

            if (hopTimer > 0f)
            {
                hopTimer -= dt;
                float t = 1f - Mathf.Clamp01(hopTimer / hitHopDuration);
                if (bodyVisual != null)
                    bodyVisual.localPosition = bodyBasePosition + Vector3.up * (Mathf.Sin(t * Mathf.PI) * hitHopHeight);
                if (agent.enabled && agent.isOnNavMesh)
                    agent.Move(knockDirection * (hitKnockback / hitHopDuration) * dt);
            }
            else if (bodyVisual != null)
            {
                bodyVisual.localPosition = bodyBasePosition;
            }
        }

        // ---------- Créature errante ----------

        private void UpdateWanderer(float dt)
        {
            float distance = HasLivingPlayer() ? Vector3.Distance(transform.position, player.transform.position) : float.MaxValue;

            switch (state)
            {
                case State.Panic:
                    UpdatePanic(dt);
                    return;

                case State.Flee:
                    // Retour à l'errance après être resté loin du joueur assez longtemps.
                    safeTimer = distance > safeDistance ? safeTimer + dt : 0f;
                    if (safeTimer >= safeDuration) { SetState(State.Wander); break; }
                    UpdateFlee(distance, dt);
                    return;
            }

            if (Wounded && distance <= safeDistance)
            {
                BeginFlee(Vector3.zero);
                return;
            }

            if (!Wounded && distance <= detectionRange)
            {
                SetState(State.Stalk);
                UpdateStalk(distance);
                return;
            }

            SetState(State.Wander);
            UpdateWander(dt);
        }

        private void UpdateWander(float dt)
        {
            agent.speed = wanderSpeed;
            agent.updateRotation = true;
            if (!LightZone.IsLit(BodyPoint))
                health = Mathf.Min(maxHealth, health + wanderRegenPerSecond * dt);

            wanderTimer -= dt;
            bool arrived = !agent.pathPending && agent.remainingDistance < 0.5f;
            if (wanderTimer > 0f && !arrived) return;

            // Nouvelle direction : souvent proche de la précédente, parfois franchement différente.
            float turn = UnityEngine.Random.Range(-100f, 100f);
            wanderDirection = Quaternion.Euler(0f, turn, 0f) * wanderDirection;
            wanderTimer = UnityEngine.Random.Range(3f, 6f);

            Vector3 target = transform.position + wanderDirection * 6f;
            if (!TryGetDarkPoint(target, out Vector3 point))
            {
                wanderDirection = -wanderDirection;
                target = transform.position + wanderDirection * 6f;
                if (!TryGetDarkPoint(target, out point)) return;
            }
            MoveTo(point, force: true);
        }

        private void UpdateStalk(float distance)
        {
            if (!PlayerIsLit())
            {
                // Joueur dans le noir : il fonce et frappe.
                agent.speed = stalkSpeed;
                agent.updateRotation = true;
                MoveTo(player.transform.position);
                TryAttack(1f, ignoreLight: false);
                return;
            }

            agent.speed = stalkSpeed;
            Vector3 point = StalkPoint();
            MoveTo(point);

            // Une fois en place, il fixe le joueur.
            bool settled = !agent.pathPending && agent.remainingDistance < 1f;
            agent.updateRotation = !settled;
            if (settled) FacePlayer();
        }

        /// <param name="preferredDirection">Zéro : à l'opposé du joueur ; sinon cette direction en priorité.</param>
        private void BeginFlee(Vector3 preferredDirection)
        {
            SetState(State.Flee);
            safeTimer = 0f;
            stuckTimer = 0f;
            if (!TryFindFleePoint(preferredDirection, out Vector3 point))
            {
                BeginPanic();
                return;
            }
            MoveTo(point, force: true);
        }

        private void UpdateFlee(float distance, float dt)
        {
            agent.speed = fleeSpeed;
            agent.updateRotation = true;

            // Coincé contre un mur avec le joueur tout près : panique.
            bool slow = agent.velocity.sqrMagnitude < 0.5f;
            stuckTimer = slow && distance < 4f ? stuckTimer + dt : 0f;
            if (stuckTimer > 0.4f)
            {
                BeginPanic();
                return;
            }

            bool arrived = !agent.pathPending && agent.remainingDistance < 1f;
            if (arrived && distance <= safeDistance + 2f)
            {
                if (TryFindFleePoint(Vector3.zero, out Vector3 point)) MoveTo(point, force: true);
                else BeginPanic();
            }
        }

        private void BeginPanic()
        {
            SetState(State.Panic);
            panicTimer = panicTimeout;
        }

        private void UpdatePanic(float dt)
        {
            agent.speed = panicSpeed;
            agent.updateRotation = true;
            panicTimer -= dt;

            if (!HasLivingPlayer()) { SetState(State.Wander); return; }
            MoveTo(player.transform.position);

            // Acculé, il frappe même si le joueur est éclairé, puis repart dans une autre direction.
            if (TryAttack(1f, ignoreLight: true) || panicTimer <= 0f)
            {
                // Autre direction : de côté par rapport au joueur, et pas celle qui était bloquée.
                Vector3 away = transform.position - player.transform.position;
                away.y = 0f;
                Vector3 side = Quaternion.Euler(0f, UnityEngine.Random.value < 0.5f ? 90f : -90f, 0f) * away;
                if (Vector3.Dot(side, lastFleeDirection) > 0.7f * side.magnitude) side = -side;
                BeginFlee(side);
            }
        }

        // ---------- Assaut ----------

        private void UpdateAssault(float boost)
        {
            SetState(State.Assault);
            agent.speed = assaultSpeed * boost;
            agent.updateRotation = true;

            float playerDistance = HasLivingPlayer() ? Vector3.Distance(transform.position, player.transform.position) : -1f;
            var beacon = Beacon.FindNearestLit(transform.position);
            float beaconDistance = beacon != null ? Vector3.Distance(transform.position, beacon.transform.position) : -1f;

            switch (CreatureTargeting.Choose(playerDistance, beaconDistance, playerPriority))
            {
                case TargetKind.Player:
                    MoveTo(player.transform.position);
                    TryAttack(boost, ignoreLight: true); // à l'assaut, il frappe même dans la lumière
                    break;
                case TargetKind.Beacon:
                    MoveTo(beacon.transform.position);
                    break;
                default:
                    UpdateWander(Time.deltaTime);
                    break;
            }
        }

        // ---------- Attaque ----------

        /// <returns>true si un coup a été porté.</returns>
        private bool TryAttack(float boost, bool ignoreLight)
        {
            if (attackTimer > 0f || !HasLivingPlayer()) return false;
            if (!ignoreLight && PlayerIsLit()) return false;
            if (Vector3.Distance(transform.position, player.transform.position) > attackRange) return false;

            attackTimer = attackCooldown;
            player.TakeDamage(attackDamage * boost); // le stress du coup est ajouté par PlayerStress
            return true;
        }

        // ---------- Points de destination ----------

        /// <summary>Au bord de la lumière du joueur, du côté de la créature, hors de la lumière des phares.</summary>
        private Vector3 StalkPoint()
        {
            Vector3 playerPos = player.transform.position;
            Vector3 away = transform.position - playerPos;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            away.Normalize();

            // Rayon de la lumière autour du joueur (zones qui le couvrent).
            Vector3 playerBody = playerLight != null ? playerLight.BodyPoint : playerPos + Vector3.up;
            float radius = 3f;
            foreach (var zone in LightZone.Active)
                if (zone.Contains(playerBody))
                    radius = Mathf.Max(radius, zone.Radius + Vector3.Distance(zone.Center, playerBody));

            Vector3 point = playerPos + away * (radius + stalkMargin);

            var beacon = Beacon.FindCovering(point + Vector3.up);
            if (beacon != null && beacon.Zone != null)
            {
                Vector3 fromBeacon = point - beacon.Zone.Center;
                fromBeacon.y = 0f;
                if (fromBeacon.sqrMagnitude < 0.01f) fromBeacon = away;
                point = beacon.Zone.Center + fromBeacon.normalized * (beacon.Zone.Radius + stalkMargin);
                point.y = playerPos.y;
            }
            return point;
        }

        /// <summary>
        /// Cherche un point de fuite accessible qui l'éloigne vraiment du joueur.
        /// preferredDirection = zéro : à l'opposé du joueur ; sinon, cette direction en priorité.
        /// </summary>
        private bool TryFindFleePoint(Vector3 preferredDirection, out Vector3 result)
        {
            result = transform.position;
            Vector3 from = HasLivingPlayer() ? player.transform.position : transform.position - transform.forward;
            Vector3 away = transform.position - from;
            away.y = 0f;
            Vector3 baseDirection = preferredDirection.sqrMagnitude > 0.01f ? preferredDirection : away;
            baseDirection.y = 0f;
            if (baseDirection.sqrMagnitude < 0.01f) baseDirection = RandomFlatDirection();
            baseDirection.Normalize();

            float currentDistance = away.magnitude;
            var path = new NavMeshPath();
            float[] angles = { 0f, 35f, -35f, 70f, -70f, 105f, -105f };
            foreach (float angle in angles)
            {
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * baseDirection;
                Vector3 candidate = transform.position + direction * fleeStep;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
                if (Vector3.Distance(hit.position, from) < currentDistance + 3f) continue;
                if (Beacon.AnyLitBeaconCovers(hit.position + Vector3.up)) continue;
                if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                result = hit.position;
                lastFleeDirection = direction;
                return true;
            }
            return false;
        }

        private bool TryGetDarkPoint(Vector3 target, out Vector3 point)
        {
            point = target;
            if (!NavMesh.SamplePosition(target, out NavMeshHit hit, 2.5f, NavMesh.AllAreas)) return false;
            if (LightZone.IsLit(hit.position + Vector3.up)) return false;
            point = hit.position;
            return true;
        }

        // ---------- Mort ----------

        private void Die()
        {
            if (state == State.Dying) return;
            SetState(State.Dying);
            health = 0f;
            deathTimer = deathDuration;
            if (agent.isOnNavMesh) agent.isStopped = true;
            agent.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            Died?.Invoke(this);
        }

        private void UpdateDeath(float dt)
        {
            deathTimer -= dt;
            transform.localScale = baseScale * Mathf.Clamp01(deathTimer / deathDuration);
            if (deathTimer <= 0f) Destroy(gameObject);
        }

        // ---------- Aides ----------

        private void SetState(State next)
        {
            if (next == state) return;
            var previous = state;
            state = next;
            StateChanged?.Invoke(previous, next);
        }

        private bool HasLivingPlayer() => player != null && !player.IsDead;

        private bool PlayerIsLit() => playerLight != null && playerLight.IsLit;

        private void FacePlayer()
        {
            Vector3 look = player.transform.position - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), 540f * Time.deltaTime);
        }

        private static Vector3 RandomFlatDirection()
        {
            Vector2 d = UnityEngine.Random.insideUnitCircle.normalized;
            return d.sqrMagnitude > 0f ? new Vector3(d.x, 0f, d.y) : Vector3.forward;
        }

        private void MoveTo(Vector3 destination, bool force = false)
        {
            if (!force && repathTimer > 0f) return;
            repathTimer = repathInterval;
            if (!agent.isOnNavMesh) return;

            // Hors assaut, la lumière des phares est infranchissable : destination repoussée au bord,
            // et trajet qui la contourne.
            bool avoidBeacons = state != State.Assault;
            if (avoidBeacons) destination = PushOutOfBeaconLight(destination);

            // Point hors du maillage (derrière un mur…) : on vise le point accessible le plus proche.
            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                destination = hit.position;

            if (!avoidBeacons || !agent.CalculatePath(destination, scratchPath))
            {
                agent.SetDestination(destination);
                return;
            }

            if (TryGetDetour(scratchPath, destination, out Vector3 detour)) agent.SetDestination(detour);
            else
            {
                detourSign = 0f;
                agent.SetPath(scratchPath);
            }
        }

        // ---------- Contournement de la lumière des phares ----------

        /// <summary>Rayon, à hauteur du corps, du disque de lumière d'un phare, marge comprise (0 si éteint).</summary>
        private float AvoidRadius(Beacon beacon, float groundY)
        {
            if (!beacon.IsLit || beacon.Zone == null) return 0f;
            float dy = beacon.Zone.Center.y - (groundY + 1f);
            float r2 = beacon.Zone.Radius * beacon.Zone.Radius - dy * dy;
            return r2 > 0f ? Mathf.Sqrt(r2) + beaconAvoidMargin : 0f;
        }

        /// <summary>Ramène un point situé dans la lumière d'un phare juste au-delà de son bord.</summary>
        private Vector3 PushOutOfBeaconLight(Vector3 point)
        {
            foreach (var beacon in Beacon.All)
            {
                float radius = AvoidRadius(beacon, point.y);
                if (radius <= 0f) continue;
                Vector3 offset = point - beacon.Zone.Center;
                offset.y = 0f;
                if (offset.sqrMagnitude >= radius * radius) continue;
                if (offset.sqrMagnitude < 0.01f) offset = transform.position - beacon.Zone.Center;
                offset.y = 0f;
                if (offset.sqrMagnitude < 0.01f) offset = Vector3.forward;
                Vector3 pushed = beacon.Zone.Center + offset.normalized * radius;
                pushed.y = point.y;
                point = pushed;
            }
            return point;
        }

        /// <summary>
        /// Si le trajet traverse la lumière d'un phare, renvoie un point de contournement : autour du disque
        /// lumineux, du côté de la destination, un peu en avance sur la créature. Recalculé à chaque repath,
        /// ce point glisse le long du bord jusqu'à ce que la voie soit libre.
        /// </summary>
        private bool TryGetDetour(NavMeshPath path, Vector3 destination, out Vector3 detour)
        {
            detour = destination;
            int count = path.GetCornersNonAlloc(corners);
            for (int i = 0; i + 1 < count; i++)
            {
                foreach (var beacon in Beacon.All)
                {
                    float radius = AvoidRadius(beacon, corners[i].y);
                    if (radius <= 0f) continue;
                    if (!SegmentCrossesDisc(corners[i], corners[i + 1], beacon.Zone.Center, radius)) continue;
                    return TryDetourPoint(beacon.Zone.Center, radius, destination, out detour);
                }
            }
            return false;
        }

        private bool TryDetourPoint(Vector3 center, float radius, Vector3 destination, out Vector3 detour)
        {
            detour = destination;
            Vector3 fromCenter = transform.position - center;
            fromCenter.y = 0f;
            float distance = fromCenter.magnitude;
            if (distance < 0.01f) return false;

            // Sens du contournement : le plus court vers la destination, puis conservé pour éviter d'hésiter.
            if (detourSign == 0f)
            {
                Vector3 toDestination = destination - center;
                float cross = fromCenter.x * toDestination.z - fromCenter.z * toDestination.x;
                detourSign = cross >= 0f ? -1f : 1f;
            }

            // Point tangent au disque vu depuis la créature, plus une avance pour longer le bord.
            float tangent = Mathf.Acos(Mathf.Clamp01(radius / distance)) * Mathf.Rad2Deg;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                float angle = detourSign * (tangent + detourStep);
                Vector3 candidate = center + Quaternion.Euler(0f, angle, 0f) * (fromCenter / distance) * (radius + 0.5f);
                candidate.y = transform.position.y;
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas) &&
                    !Beacon.AnyLitBeaconCovers(hit.position + Vector3.up) &&
                    agent.CalculatePath(hit.position, scratchPath) &&
                    scratchPath.status == NavMeshPathStatus.PathComplete)
                {
                    detour = hit.position;
                    return true;
                }
                detourSign = -detourSign; // bloqué par un mur de ce côté : on tente l'autre.
            }

            // Aucun passage : il attend au bord plutôt que d'entrer dans la lumière.
            Vector3 edge = center + (fromCenter / distance) * radius;
            edge.y = transform.position.y;
            detour = distance > radius ? transform.position : edge;
            return true;
        }

        private static bool SegmentCrossesDisc(Vector3 a, Vector3 b, Vector3 center, float radius)
        {
            Vector2 p = new Vector2(a.x - center.x, a.z - center.z);
            Vector2 d = new Vector2(b.x - a.x, b.z - a.z);
            float lengthSq = d.sqrMagnitude;
            float t = lengthSq > 0.0001f ? Mathf.Clamp01(-Vector2.Dot(p, d) / lengthSq) : 0f;
            return (p + d * t).sqrMagnitude < radius * radius;
        }

        /// <summary>Pris dans la lumière d'un phare hors assaut : fuite vers le bord le plus proche.</summary>
        private void EscapeBeacon(Beacon beacon)
        {
            SetState(State.Flee);
            safeTimer = 0f;
            stuckTimer = 0f;
            agent.speed = fleeSpeed;
            agent.updateRotation = true;
            if (repathTimer > 0f || !agent.isOnNavMesh) return;
            repathTimer = repathInterval;

            Vector3 outward = transform.position - beacon.Zone.Center;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.01f) outward = RandomFlatDirection();
            float radius = AvoidRadius(beacon, transform.position.y) + 0.5f;
            Vector3 exit = beacon.Zone.Center + outward.normalized * radius;
            exit.y = transform.position.y;
            if (NavMesh.SamplePosition(exit, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }
    }
}
