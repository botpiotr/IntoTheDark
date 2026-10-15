using IntoTheDark.Cycle;
using UnityEngine;

namespace IntoTheDark.Aether
{
    /// <summary>
    /// Gisement d'éther (tuyère). Se régénère en continu : 1 éther par cycle à la base,
    /// réparti sur la durée réelle du cycle en cours.
    /// Un phare posé dessus y puise son éther, et la lampe du joueur s'y recharge.
    /// </summary>
    public class AetherDeposit : MonoBehaviour
    {
        [SerializeField] private AetherReserve reserve = new AetherReserve();
        [SerializeField, Min(1f), Tooltip("Durée de cycle utilisée s'il n'y a pas de gestionnaire de cycle (s)")]
        private float fallbackCycleDuration = 1020f;

        [Header("État (lecture seule)")]
        [SerializeField] private float currentAmount;

        public AetherReserve Reserve => reserve;
        public float FallbackCycleDuration => fallbackCycleDuration;

        private void Update()
        {
            reserve.Regenerate(CycleManager.CycleFraction(Time.deltaTime, fallbackCycleDuration));
            currentAmount = reserve.Amount;
        }
    }
}
