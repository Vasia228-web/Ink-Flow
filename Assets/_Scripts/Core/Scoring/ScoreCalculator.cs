namespace InkFlow.Core
{
    /// <summary>Очки за хід (майстер-док §5: очки за злиття та вибухи, ланцюги дають комбо).</summary>
    public static class ScoreCalculator
    {
        public static int MergeScore(int mergedDensity, BalanceData balance) =>
            mergedDensity * balance.ScorePerMergedDensity;

        public static int BurstScore(int force, BalanceData balance) =>
            force * balance.ScorePerBurstForce;

        /// <summary>
        /// Комбо-множник ланцюга: 1 вибух — ×1, 2 — ×2, 3 — ×3... Множник застосовується
        /// до підсумку ходу, тому довгий ланцюг платить нелінійно — це і є «кайфовий момент».
        /// </summary>
        public static int ComboMultiplier(int chainDepth) => chainDepth < 1 ? 1 : chainDepth;

        public static int ApplyCombo(int rawScore, int chainDepth) => rawScore * ComboMultiplier(chainDepth);
    }

    /// <summary>
    /// Зірки рівня (майстер-док §4): ★ — пройшов; ★★ — лишилось ≥20% ліміту ходів;
    /// ★★★ — лишилось ≥40% (а на бос-рівні ще й жодного власного перефарбування сегмента).
    /// Пороги — з BalanceConfig; симулятор Фази 4 уточнює їх під кожен рівень.
    /// </summary>
    public static class StarCalculator
    {
        public static int Stars(int movesLeft, int maxMoves, BalanceData balance, bool bossCleanRun = true)
        {
            if (maxMoves <= 0)
                return 0;

            var fraction = (float)movesLeft / maxMoves;

            if (fraction >= balance.ThreeStarMovesLeftFraction && bossCleanRun)
                return 3;
            if (fraction >= balance.TwoStarMovesLeftFraction)
                return 2;
            return 1;
        }
    }
}
