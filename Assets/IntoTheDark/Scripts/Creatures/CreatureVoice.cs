using UnityEngine;

namespace IntoTheDark.Creatures
{
    /// <summary>
    /// Voix du rôdeur, discrète et spatialisée (on entend d'où elle vient) :
    /// - errance : grognement sourd et rare, seulement si le joueur est à moins de 15 m ;
    /// - traque : silence ;
    /// - fuite, panique, vengeance, assaut : aboiement en changeant de mode, puis de temps en temps.
    /// </summary>
    [RequireComponent(typeof(Lurker), typeof(AudioSource))]
    public class CreatureVoice : MonoBehaviour
    {
        [SerializeField, Tooltip("Pause entre deux cris dans un même mode (s)")]
        private Vector2 interval = new Vector2(9f, 18f);
        [SerializeField, Tooltip("Pause entre deux grognements d'errance (s)")]
        private Vector2 wanderInterval = new Vector2(20f, 40f);
        [SerializeField, Range(0f, 1f)] private float wanderVolume = 0.3f;
        [SerializeField, Min(0f), Tooltip("Le grognement d'errance n'est émis que si le joueur est plus près (m)")]
        private float wanderAudibleRange = 15f;
        [SerializeField, Min(0f), Tooltip("Délai minimal entre deux cris (s)")] private float minGap = 3f;
        [SerializeField, Range(0f, 1f)] private float volume = 0.6f;
        [SerializeField, Range(0f, 0.5f)] private float pitchVariation = 0.12f;

        private Lurker lurker;
        private AudioSource source;
        private float nextCall;
        private float lastCall = -10f;
        private Transform listener;

        private void Awake()
        {
            lurker = GetComponent<Lurker>();
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;      // son 3D : direction et distance
            source.spread = 0f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.5f;
            source.maxDistance = 22f;
            nextCall = Time.time + Random.Range(2f, wanderInterval.y);
        }

        private void OnEnable() => GetComponent<Lurker>().StateChanged += OnStateChanged;
        private void OnDisable() => GetComponent<Lurker>().StateChanged -= OnStateChanged;

        private void OnStateChanged(Lurker.State previous, Lurker.State next)
        {
            // Cri seulement en devenant agressif ou en fuyant ; jamais en errance ou en traque.
            if (next == Lurker.State.Dying || next == Lurker.State.Wander || next == Lurker.State.Stalk) return;
            Call(next);
        }

        private void Update()
        {
            if (lurker.IsDead || Time.time < nextCall) return;
            Call(lurker.CurrentState);
        }

        private void Call(Lurker.State state)
        {
            if (Time.time - lastCall < minGap) return;
            if (state == Lurker.State.Stalk)
            {
                nextCall = Time.time + Random.Range(interval.x, interval.y); // la traque est silencieuse
                return;
            }

            float callVolume = volume;
            Vector2 pause = interval;
            if (state == Lurker.State.Wander)
            {
                pause = wanderInterval;
                callVolume = wanderVolume;
                if (!ListenerWithin(wanderAudibleRange))
                {
                    nextCall = Time.time + Random.Range(2f, 5f); // trop loin : on réessaiera plus tard
                    return;
                }
            }

            AudioClip clip;
            switch (state)
            {
                case Lurker.State.Wander: clip = CreatureSounds.Growl(); break;
                default: clip = CreatureSounds.Bark(); break;
            }

            source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            source.PlayOneShot(clip, callVolume);
            lastCall = Time.time;
            nextCall = Time.time + Random.Range(pause.x, pause.y);
        }

        private bool ListenerWithin(float range)
        {
            if (listener == null)
            {
                var audioListener = FindObjectOfType<AudioListener>();
                if (audioListener == null) return false;
                listener = audioListener.transform;
            }
            return (listener.position - transform.position).sqrMagnitude <= range * range;
        }
    }
}
