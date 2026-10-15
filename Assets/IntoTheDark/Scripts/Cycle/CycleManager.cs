using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IntoTheDark.Cycle
{
    /// <summary>
    /// Fait avancer le temps du jeu : cycles de calme, tempêtes et apocalypse.
    /// Les autres systèmes écoutent ses événements (statiques) ou lisent son état.
    /// Un seul par scène.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class CycleManager : MonoBehaviour
    {
        [SerializeField, Tooltip("Cycles raccourcis (environ 2 min) pour les tests")]
        private bool testMode = true;
        [SerializeField] private CycleTimings normalTimings = CycleTimings.Normal();
        [SerializeField] private CycleTimings testTimings = CycleTimings.Test();
        [SerializeField, Tooltip("0 = tirage différent à chaque partie")] private int seed;

        [Header("Debug")]
        [SerializeField, Tooltip("Passer directement à la phase suivante")] private Key skipPhaseKey = Key.F6;

        [Header("État (lecture seule)")]
        [SerializeField] private int cycle;
        [SerializeField] private CyclePhase phase;
        [SerializeField] private CyclePhase upcomingPhase;
        [SerializeField] private float timeInCycle;
        [SerializeField] private float secondsToNextPhase;
        [SerializeField] private int stormsThisCycle;

        private System.Random random;
        private CycleSchedule schedule;
        private int spanIndex;
        private bool warningSent;

        public static CycleManager Instance { get; private set; }

        /// <summary>Début d'un cycle (numéro, à partir de 1).</summary>
        public static event Action<int> CycleStarted;
        /// <summary>Fin d'un cycle (numéro), juste avant le début du suivant.</summary>
        public static event Action<int> CycleEnded;
        public static event Action<CyclePhase> PhaseChanged;
        /// <summary>Une tempête ou l'apocalypse approche (début des signes avant-coureurs).</summary>
        public static event Action<CyclePhase> PhaseWarning;

        public int Cycle => cycle;
        public CyclePhase Phase => phase;
        public CyclePhase UpcomingPhase => upcomingPhase;
        public float SecondsToNextPhase => secondsToNextPhase;
        public float TimeInCycle => timeInCycle;
        public CycleTimings Timings => testMode ? testTimings : normalTimings;
        public CycleSchedule Schedule => schedule;

        /// <summary>Durée réelle du cycle en cours (secondes), tempêtes comprises.</summary>
        public float CycleDuration => schedule != null ? schedule.Duration : 0f;

        /// <summary>
        /// Part du cycle en cours écoulée pendant deltaTime : sert à étaler une consommation
        /// "par cycle" en continu, quelle que soit la durée réelle du cycle.
        /// Sans gestionnaire de cycle, utilise la durée de secours fournie.
        /// </summary>
        public static float CycleFraction(float deltaTime, float fallbackCycleDuration)
        {
            float duration = Instance != null && Instance.CycleDuration > 0f ? Instance.CycleDuration : fallbackCycleDuration;
            return duration > 0f ? deltaTime / duration : 0f;
        }

        /// <summary>0 hors préavis ; monte jusqu'à 1 juste avant une tempête ou l'apocalypse.</summary>
        public float WarningIntensity
        {
            get
            {
                float lead = Timings.warningLead;
                if (lead <= 0f || phase == upcomingPhase || upcomingPhase == CyclePhase.Calm)
                    return 0f;
                return Mathf.Clamp01(1f - secondsToNextPhase / lead);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[CycleManager] Un seul gestionnaire de cycle par scène : celui-ci est désactivé.");
                enabled = false;
                return;
            }
            Instance = this;
            random = seed == 0 ? new System.Random() : new System.Random(seed);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start() => BeginCycle(1, 0f);

        private void Update()
        {
            if (schedule == null) return;

            timeInCycle += Time.deltaTime;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[skipPhaseKey].wasPressedThisFrame)
                timeInCycle = Mathf.Max(timeInCycle, schedule.Spans[spanIndex].End);

            while (timeInCycle >= schedule.Spans[spanIndex].End)
            {
                if (spanIndex + 1 < schedule.Spans.Count)
                {
                    spanIndex++;
                    SetPhase(schedule.Spans[spanIndex].Phase);
                }
                else
                {
                    float overflow = timeInCycle - schedule.Duration;
                    CycleEnded?.Invoke(cycle);
                    BeginCycle(cycle + 1, overflow);
                }
            }

            UpdateUpcoming();
        }

        private void BeginCycle(int number, float startTime)
        {
            cycle = number;
            schedule = CycleSchedule.Build(Timings, random);
            stormsThisCycle = schedule.StormCount;
            timeInCycle = Mathf.Max(0f, startTime);
            spanIndex = 0;

            CycleStarted?.Invoke(cycle);
            SetPhase(schedule.Spans[0].Phase, force: true);
            UpdateUpcoming();
        }

        private void SetPhase(CyclePhase newPhase, bool force = false)
        {
            warningSent = false;
            if (!force && newPhase == phase)
                return;
            phase = newPhase;
            PhaseChanged?.Invoke(phase);
        }

        private void UpdateUpcoming()
        {
            var current = schedule.Spans[spanIndex];
            secondsToNextPhase = Mathf.Max(0f, current.End - timeInCycle);
            // Après l'apocalypse vient le calme du cycle suivant.
            upcomingPhase = spanIndex + 1 < schedule.Spans.Count
                ? schedule.Spans[spanIndex + 1].Phase
                : CyclePhase.Calm;

            if (!warningSent && upcomingPhase != CyclePhase.Calm && upcomingPhase != phase
                && secondsToNextPhase <= Timings.warningLead)
            {
                warningSent = true;
                PhaseWarning?.Invoke(upcomingPhase);
            }
        }
    }
}
