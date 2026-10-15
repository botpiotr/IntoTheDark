using System.Collections;
using IntoTheDark.Cycle;
using IntoTheDark.Lighting;
using IntoTheDark.Player;
using IntoTheDark.UI;
using UnityEngine;

namespace IntoTheDark.Game
{
    /// <summary>
    /// Règles de fin de partie. Le joueur meurt :
    /// - s'il reste au moins un phare allumé, il réapparaît près du plus proche ;
    /// - sinon, la partie est perdue : écran de game over, puis retour au menu principal.
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        [SerializeField] private PlayerHealth player;
        [SerializeField, Min(0f)] private float fadeOutDuration = 1f;
        [SerializeField, Min(0f)] private float blackDuration = 0.8f;
        [SerializeField, Min(0f)] private float fadeInDuration = 1.2f;

        private ScreenFader fader;
        private bool busy;

        public bool IsGameOver { get; private set; }

        private void Awake()
        {
            Time.timeScale = 1f;
            if (player == null)
                player = FindObjectOfType<PlayerHealth>();
        }

        private void OnEnable()
        {
            if (player != null) player.Died += OnPlayerDied;
        }

        private void OnDisable()
        {
            if (player != null) player.Died -= OnPlayerDied;
        }

        private void OnPlayerDied()
        {
            if (busy || IsGameOver) return;

            var beacon = Beacon.FindNearestLit(player.transform.position);
            if (beacon != null)
                StartCoroutine(Respawn(beacon));
            else
                GameOver();
        }

        private IEnumerator Respawn(Beacon beacon)
        {
            busy = true;
            SetControls(false);
            if (fader == null) fader = ScreenFader.Create();

            yield return fader.FadeTo(1f, fadeOutDuration);
            yield return new WaitForSecondsRealtime(blackDuration);

            // Le phare a pu s'éteindre pendant le fondu : on vérifie à nouveau.
            if (!beacon.IsLit)
                beacon = Beacon.FindNearestLit(player.transform.position);
            if (beacon == null)
            {
                busy = false;
                GameOver();
                yield break;
            }

            Teleport(beacon.RespawnPoint);
            player.Revive();
            SetControls(true);

            yield return fader.FadeTo(0f, fadeInDuration);
            busy = false;
        }

        private void GameOver()
        {
            IsGameOver = true;
            SetControls(false);
            int survived = CycleManager.Instance != null ? Mathf.Max(0, CycleManager.Instance.Cycle - 1) : 0;
            GameOverScreen.Show(survived);
            Time.timeScale = 0f;
        }

        private void SetControls(bool on)
        {
            foreach (var behaviour in new MonoBehaviour[]
                     {
                         player.GetComponent<PlayerInputHandler>(),
                         player.GetComponent<PlayerMovement>(),
                         player.GetComponent<PlayerLook>(),
                         player.GetComponent<PlayerInteractor>(),
                     })
            {
                if (behaviour != null) behaviour.enabled = on;
            }
        }

        private void Teleport(Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.position = position;
            if (controller != null) controller.enabled = true;
        }
    }
}
