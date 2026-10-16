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
    /// - une demi-douzaine rôde sur la carte, placée au hasard dans le noir ; elles sont toutes
    ///   attirées pendant l'apocalypse, puis la population est complétée au début du cycle suivant ;
    /// - pendant une tempête, 2 créatures par seconde surgissent du noir et foncent sur le joueur.
    /// Debug : F7 fait apparaître une créature près du joueur ; F8 blesse de 30 % celles à moins de 10 m.
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
        [SerializeField, Min(0f)] private float stormSpawnsPerSecond = 2f;
        [SerializeField, Min(0), Tooltip("Plafond de créatures de tempête vivantes en même temps")]
        private int maxStormCreatures = 40;
        [SerializeField, Min(0f)] private float stormMinSpawnDistance = 15f;

        [Header("Debug")]
        [SerializeField] private Key debugSpawnKey = Key.F7;
        [SerializeField] private Key debugHurtKey = Key.F8;

        [Header("État (lecture seule)")]
        [SerializeField] private int mapAlive;
        [SerializeField] private int stormAlive;

        private readonly List<Lurker> mapCreatures = new List<Lurker>();
        private readonly List<Lurker> stormCreatures = new List<Lurker>();
        private float stormSpawnBudget;

        private void Awake()
        {
            if (player == null) player = FindObjectOfType<PlayerHealth>();
        }

        private void OnEnable() => CycleManager.CycleStarted += OnCycleStarted;
        private void OnDisable() => CycleManager.CycleStarted -= OnCycleStarted;

        private void Start() => FillMapPopulation();

        private void OnCycleStarted(int cycle)
        {
            if (cycle > 1) FillMapPopulation(); // après l'apocalypse, la carte se repeuple
        }

        private void Update()
        {
            mapCreatures.RemoveAll(c => c == null || c.IsDead);
            stormCreatures.RemoveAll(c => c == null || c.IsDead);
            mapAlive = mapCreatures.Count;
            stormAlive = stormCreatures.Count;

            var cycle = CycleManager.Instance;
            if (cycle != null && cycle.Phase == CyclePhase.Storm && stormSpawnsPerSecond > 0f)
            {
                stormSpawnBudget += stormSpawnsPerSecond * Time.deltaTime;
                while (stormSpawnBudget >= 1f)
                {
                    stormSpawnBudget -= 1f;
                    if (stormCreatures.Count >= maxStormCreatures) continue;
                    var lurker = SpawnInDarkness(stormMinSpawnDistance);
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
                            creature.TakeDamage(30f, player.transform.position);
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

        /// <summary>Apparition à un point au hasard dans le noir, loin du joueur.</summary>
        private Lurker SpawnInDarkness(float minDistance)
        {
            if (lurkerPrefab == null) return null;
            Vector3 playerPos = player != null ? player.transform.position : Vector3.positiveInfinity;

            for (int attempt = 0; attempt < 30; attempt++)
            {
                Vector3 candidate = new Vector3(
                    Random.Range(spawnArea.min.x, spawnArea.max.x),
                    spawnArea.center.y,
                    Random.Range(spawnArea.min.z, spawnArea.max.z));
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
                if (LightZone.IsLit(hit.position + Vector3.up)) continue;
                if (player != null && Vector3.Distance(hit.position, playerPos) < minDistance) continue;
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
