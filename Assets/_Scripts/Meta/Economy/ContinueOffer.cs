namespace InkFlow.Meta
{
    /// <summary>Чим гравцю пропонують продовжити після програшу (§10, §13, §17).</summary>
    public enum ContinueKind
    {
        /// <summary>Продовжень більше немає (або немає чим платити взагалі).</summary>
        None = 0,

        /// <summary>За ролик — перше продовження забігу, коли ролик готовий.</summary>
        Ad = 1,

        /// <summary>За нафту — ролик уже був або реклами немає.</summary>
        Oil = 2
    }

    /// <summary>
    /// Правило «перше продовження за ролик, далі — за нафту» в одному місці без Unity, щоб його
    /// тримав headless-тест, а екран забігу лише показував. Чи вистачає нафти — окреме питання чипа.
    /// </summary>
    public static class ContinueOffer
    {
        /// <param name="continuesUsed">Скільки продовжень уже було в цьому забігу.</param>
        /// <param name="continuesPerRun">Стеля продовжень за забіг (баланс).</param>
        /// <param name="adReady">Ролик за нагороду готовий.</param>
        /// <param name="hasWallet">Є стан гравця з гаманцем (поза майстернею).</param>
        public static ContinueKind Decide(int continuesUsed, int continuesPerRun, bool adReady, bool hasWallet)
        {
            if (continuesUsed < 0 || continuesUsed >= continuesPerRun)
                return ContinueKind.None;
            if (continuesUsed == 0 && adReady)
                return ContinueKind.Ad;
            return hasWallet ? ContinueKind.Oil : ContinueKind.None;
        }
    }
}
