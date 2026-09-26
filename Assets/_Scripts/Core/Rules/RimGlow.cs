namespace InkFlow.Core
{
    /// <summary>
    /// Профіль світіння-ранту по краю панелі (пульсація «мало місця», §11): пік рівно на краю,
    /// квадратичне згасання всередину на <c>inner</c> і назовні на <c>outer</c>. Функція
    /// неперервна всюди — жодного розриву альфи на межі силуету (урок `card-glow`, CLAUDE.md):
    /// розрив малює лінію, скільки не знижуй прозорість.
    ///
    /// Живе в Core, щоб неперервність і межі тримав headless-тест; спрайт з нього будує в'юха.
    /// </summary>
    public static class RimGlow
    {
        /// <param name="distance">Відстань до краю: від'ємна — усередині панелі, додатна — назовні.</param>
        public static float Alpha(float distance, float inner, float outer)
        {
            if (distance <= 0f)
            {
                if (inner <= 0f)
                    return distance == 0f ? 1f : 0f;
                var t = 1f + distance / inner;
                return t <= 0f ? 0f : t * t;
            }
            if (outer <= 0f)
                return 0f;
            var o = 1f - distance / outer;
            return o <= 0f ? 0f : o * o;
        }

        /// <summary>
        /// Знакова відстань від точки до заокругленого прямокутника (центр 0, півширина/піввисота,
        /// радіус кута). Від'ємна — усередині.
        /// </summary>
        public static float RoundedRectDistance(float x, float y, float halfWidth, float halfHeight, float radius)
        {
            var qx = System.Math.Abs(x) - (halfWidth - radius);
            var qy = System.Math.Abs(y) - (halfHeight - radius);
            var ox = qx > 0f ? qx : 0f;
            var oy = qy > 0f ? qy : 0f;
            var outside = (float)System.Math.Sqrt(ox * ox + oy * oy);
            var inside = System.Math.Min(System.Math.Max(qx, qy), 0f);
            return outside + inside - radius;
        }
    }
}
