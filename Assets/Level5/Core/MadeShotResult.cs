namespace Level5.Core
{
    /// <summary>A made-shot result that scene systems can publish without exposing basketball objects.</summary>
    public readonly struct MadeShotResult
    {
        public MadeShotResult(
            int playerId,
            bool isCpu,
            ShotKind kind,
            ShotScore score,
            float shotDistance,
            int totalPointsAfter)
        {
            PlayerId = playerId;
            IsCpu = isCpu;
            Kind = kind;
            Score = score;
            ShotDistance = shotDistance;
            TotalPointsAfter = totalPointsAfter;
        }

        public int PlayerId { get; }

        public bool IsCpu { get; }

        public ShotKind Kind { get; }

        public ShotScore Score { get; }

        public float ShotDistance { get; }

        public float ShotDistanceFeet => ShotDistance * ShotScoring.DistancePointsMultiplier;

        public int TotalPointsAfter { get; }
    }
}
