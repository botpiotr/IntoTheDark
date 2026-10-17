using System.Collections.Generic;
using IntoTheDark.Cycle;
using IntoTheDark.Lighting;
using IntoTheDark.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace IntoTheDark.Creatures
{
    /// <summary>
    /// Population de créatures :
    /// - une demi-douzaine rôde sur la carte, placée au hasard dans le noir ; la population est
    ///   complétée au début de chaque cycle ;
    /// - pendant une tempête, une créature toutes les 2 s surgit du noir (8 au plus en même temps),
    ///   toujours du même côté du joueur, tiré au début de la tempête : la menace a une direction ;
    /// - à l'apocalypse, autant de créatures qu'il y en a déjà surgissent : deux fois plus d'assaillants,
    ///   mais deux fois plus lents. Ces renforts repartent dans le noir à la fin de l'apocalypse.
    /// Debug : F7 fait apparaître une créature près du joueur ; F8 inflige 5 dégâts (comme une arme) à celles à moins de 10 m.
    /// </summary>
    public class CreatureDirector : MonoBehaviour
    {
        [SerializeField] private Lurker lurkerPrefab;
        [SerializeField] private PlayerHealth player;

        [Header("Population de la carte")]
        [SerializeField, Min(0)] private int mapPopulation = 6;
        [SerializeField, Tooltip("Zone où les créatures peuvent apparaître")]
        private Bounds spawnArea = new Bounds(Vector3.zero, new Vector3(40f, 4f, 32f));
        [SerializeField, Min(0f), Tooltip("Distance minimale au joueur pour apparaître")]
        private float minSpawnDistance = 12f;

        [Header("Tempêtes")]
        [SerializeField, Min(0f)] private float stormSpawnsPerSecond = 0.5f;
        [SerializeField, Min(0), Tooltip("Plafond de créatures de tempête vivantes en même temps")]
        private int maxStormCreatures = 8;
        [SerializeField, Min(0f)] private float stormMinSpawnDistance = 15f;
        [SerializeField, Range(10f, 180f), Tooltip("Demi-angle du côté d'où arrive la tempête, vu du joueur")]
        private float stormFrontHalfAngle = 60f;

        [Header("Apocalypse")]
        [SerializeField, Min(0f), Tooltip("Renforts au début de l'apocalypse, en multiple des créatures déjà présentes")]
        private float apocalypseReinforcement = 1f;
        [SerializeField, Min(0f)] private float apocalypseMinSpawnDistance = 15f;

        [Header("Debug")]
        [SerializeField] private Key debugSpawnKey = Key.F7;
        [SerializeField] private Key debugHurtKey = Key.F8;

        [Header("État (lecture seule)")]
        [SerializeField] private int mapAlive;
        [SerializeField] private int stormAlive;

        private readonly List<Lurker> mapCreatures = new List<Lurker>();
        private readonly List<Lurker> stormCreatures = new List<Lurker>();
        private readonly List<Lurker> apocalypseCreatures = new List<Lurker>();
        private float stormSpawnBudget;
        private CyclePhase lastPhase = CyclePhase.Calm;
        private Vector3 stormFront = Vector3.forward;

        private void Awake()
        {
            if (player == null) player = FindObjectOfType<PlayerHealth>();
        }

        private void OnEnable() => CycleManager.CycleStarted += OnCycleStarted;
        private void OnDisable() => CycleManager.CycleStarted -= OnCycleStarted;

        private void Start() => FillMapPopulation();

        private void OnCycleStarted(int cycle)
        {
            if (cycle <= 1) return;
            // Fin de l'apocalypse : les renforts repartent dans le noir, la carte se repeuple.
            foreach (var creature in apocalypseCreatures)
                if (creature != null && !creature.IsDead) Destroy(creature.gameObject);
            apocalypseCreatures.Clear();
            FillMapPopulation();
        }

        private void Update()
        {
            mapCreatures.RemoveAll(c => c == null || c.IsDead);
            stormCreatures.RemoveAll(c => c == null || c.IsDead);
            mapAlive = mapCreatures.Count;
            stormAlive = stormCreatures.Count;
            apocalypseCreatures.RemoveAll(c => c == null || c.IsDead);

            var cycle = CycleManager.Instance;
            var phase = cycle != null ? cycle.Phase : CyclePhase.Calm;
            if (phase != lastPhase)
            {
                if (phase == CyclePhase.Storm) stormFront = RandomFlatDirection();
                if (phase == CyclePhase.Apocalypse) SpawnApocalypseReinforcements();
                lastPhase = phase;
            }

            if (phase == CyclePhase.Storm && stormSpawnsPerSecond > 0f)
            {
                stormSpawnBudget += stormSpawnsPerSecond * Time.deltaTime;
                while (stormSpawnBudget >= 1f)
                {
                    stormSpawnBudget -= 1f;
                    if (stormCreatures.Count >= maxStormCreatures) continue;
                    var lurker = SpawnInDarkness(stormMinSpawnDistance, stormFront, stormFrontHalfAngle);
                    if (lurker == null) continue;
                    lurker.ForcedAssault = true;
                    stormCreatures.Add(lurker);
                }
            }
            else
            {
                stormSpawnBudget = 0f;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[debugHurtKey].wasPressedThisFrame && player != null)
            {
                foreach (var list in new[] { mapCreatures, stormCreatures })
                    foreach (var creature in list)
                        if (creature != null && Vector3.Distance(creature.transform.position, player.transform.position) < 10f)
                            creature.TakeDamage(5f, player.transform.position);
            }

            if (keyboard != null && keyboard[debugSpawnKey].wasPressedThisFrame && player != null)
            {
                Vector3 near = player.transform.position + player.transform.forward * 8f;
                if (NavMesh.SamplePosition(near, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                    mapCreatures.Add(Spawn(hit.position));
            }
        }

        private void FillMapPopulation()
        {
            mapCreatures.RemoveAll(c => c == null || c.IsDead);
            for (int i = mapCreatures.Count; i < mapPopulation; i++)
            {
                var lurker = SpawnInDarkness(minSpawnDistance);
                if (lurker != null) mapCreatures.Add(lurker);
            }
        }

        /// <summary>Apocalypse : autant de nouvelles créatures que de créatures déjà présentes.</summary>
        private void SpawnApocalypseReinforcements()
        {
            int present = mapCreatures.Count + stormCreatures.Count;
            int count = Mathf.RoundToInt(present * apocalypseReinforcement);
            for (int i = 0; i < count; i++)
            {
                var lurker = SpawnInDarkness(apocalypseMinSpawnDistance);
                if (lurker != null) apocalypseCreatures.Add(lurker);
            }
        }

        private static Vector3 RandomFlatDirection()
        {
            Vector2 d = Random.insideUnitCircle.normalized;
            return d.sqrMagnitude > 0f ? new Vector3(d.x, 0f, d.y) : Vector3.forward;
        }

        /// <summary>
        /// Apparition à un point au hasard dans le noir, loin du joueur ; avec une direction,
        /// seulement de ce côté du joueur (à halfAngle près). Faute de place de ce côté, n'importe où.
        /// </summary>
        private Lurker SpawnInDarkness(float minDistance, Vector3 direction = default, float halfAngle = 180f)
        {
            if (direction != Vector3.zero && halfAngle < 180f)
            {
                var sided = TrySpawnInDarkness(minDistance, direction, halfAngle);
                if (sided != null) return sided;
            }
            return TrySpawnInDarkness(minDistance, Vector3.zero, 180f);
        }

        private Lurker TrySpawnInDarkness(float minDistance, Vector3 direction, float halfAngle)
        {
            if (lurkerPrefab == null) return null;
            Vector3 playerPos = player != null ? player.transform.position : Vector3.positiveInfinity;

            for (int attempt = 0; attempt < 40; attempt++)
            {
                Vector3 candidate = new Vector3(
                    Random.Range(spawnArea.min.x, spawnArea.max.x),
                    spawnArea.center.y,
                    Random.Range(spawnArea.min.z, spawnArea.max.z));
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
                if (LightZone.IsLit(hit.position + Vector3.up)) continue;
                if (player != null && Vector3.Distance(hit.position, playerPos) < minDistance) continue;
                if (player != null && halfAngle < 180f)
                {
                    Vector3 fromPlayer = hit.position - playerPos;
                    fromPlayer.y = 0f;
                    if (Vector3.Angle(direction, fromPlayer) > halfAngle) continue;
                }
                return Spawn(hit.position);
            }
            return null;
        }

        private Lurker Spawn(Vector3 position)
        {
            var rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var lurker = Instantiate(lurkerPrefab, position, rotation, transform);
            lurker.Init(player);
            return lurker;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
            Gizmos.DrawWireCube(spawnArea.center, spawnArea.size);
        }
    }
}
