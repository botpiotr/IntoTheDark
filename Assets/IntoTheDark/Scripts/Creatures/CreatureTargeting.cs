namespace IntoTheDark.Creatures
{
    public enum TargetKind
    {
        None,
        Player,
        Beacon,
    }

    /// <summary>Choix de cible d'une créature à l'assaut (logique pure, testable).</summary>
    public static class CreatureTargeting
    {
        /// <summary>
        /// Le joueur est prioritaire : un phare n'est choisi que s'il est nettement plus proche
        /// (sa distance multipliée par playerPriority reste inférieure à celle du joueur).
        /// Distance négative = cible absente (joueur mort, aucun phare allumé).
        /// </summary>
        public static TargetKind Choose(float playerDistance, float beaconDistance, float playerPriority)
        {
            bool hasPlayer = playerDistance >= 0f;
            bool hasBeacon = beaconDistance >= 0f;
            if (!hasPlayer && !hasBeacon) return TargetKind.None;
            if (!hasBeacon) return TargetKind.Player;
            if (!hasPlayer) return TargetKind.Beacon;
            return beaconDistance * playerPriority < playerDistance ? TargetKind.Beacon : TargetKind.Player;
        }
    }
}
