using UnityEngine;

namespace IntoTheDark.Creatures
{
    /// <summary>
    /// Voix du rôdeur : grognement en errance, sifflement en traque, aboiement en fuite,
    /// panique et assaut. Un cri à chaque changement de mode, puis de temps en temps.
    /// </summary>
    [RequireComponent(typeof(Lurker), typeof(AudioSource))]
    public class CreatureVoice : MonoBehaviour
    {
        [SerializeField, Tooltip("Pause entre deux cris dans un même mode (s)")]
        private Vector2 interval = new Vector2(3f, 7f);
        [SerializeField, Min(0f), Tooltip("Délai minimal entre deux cris (s)")] private float minGap = 0.8f;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
        [SerializeField, Range(0f, 0.5f)] private float pitchVariation = 0.12f;

        private Lurker lurker;
        private AudioSource source;
        private float nextCall;
        private float lastCall = -10f;

        private void Awake()
        {
            lurker = GetComponent<Lurker>();
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = 28f;
            nextCall = Time.time + Random.Range(0.5f, interval.y);
        }

        private void OnEnable() => GetComponent<Lurker>().StateChanged += OnStateChanged;
        private void OnDisable() => GetComponent<Lurker>().StateChanged -= OnStateChanged;

        private void OnStateChanged(Lurker.State previous, Lurker.State next)
        {
            if (next == Lurker.State.Dying) return;
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

            AudioClip clip;
            switch (state)
            {
                case Lurker.State.Wander: clip = CreatureSounds.Growl(); break;
                case Lurker.State.Stalk: clip = CreatureSounds.Hiss(); break;
                default: clip = CreatureSounds.Bark(); break;
            }

            source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            source.PlayOneShot(clip, volume);
            lastCall = Time.time;
            nextCall = Time.time + Random.Range(interval.x, interval.y);
        }
    }
}
