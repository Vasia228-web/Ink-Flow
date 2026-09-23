using System.Collections;
using System.Collections.Generic;
using InkFlow.Core;
using InkFlow.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InkFlow.UI
{
    /// <summary>
    /// Змішувач (документ §4): четвертий бак, у який зливаються три. Живе на РЯДКУ баків,
    /// а не на самій посудині — струмені й виплеск літають між баками й посудиною, і їм
    /// потрібна одна система координат.
    ///
    /// Що видно без слів: у посудині кружляють три краплі — по одній на пігмент, розміром
    /// із частку в баках, — а заливка вже має колір майбутнього виплеску. Спрацювання: три
    /// струмені стікаються з баків, краплі закручуються й зливаються в центрі, посудина
    /// спалахує відтінком — і виплеск летить угору, на полотно (крок 4). Усе це не блокує
    /// поле: черга спрацювань програється сама, ходи йдуть далі.
    ///
    /// Щокадру пишуться лише localPosition / localScale / localRotation і CanvasRenderer.
    /// </summary>
    public sealed class MixerView : MonoBehaviour
    {
        /// <summary>Одне спрацювання: що злилось, що лишилось, що вийшло.</summary>
        public struct Shot
        {
            public Hue Hue;
            public int Taken0, Taken1, Taken2;
            public int After0, After1, After2;
            public int Capacity;

            /// <summary>Куди летить виплеск (світова точка зони картинки); нуль — просто вгору.</summary>
            public Vector3 Target;

            /// <summary>Виплеску нікуди лягти — він згасає в польоті.</summary>
            public bool Missed;

            /// <summary>Спрацьовує, коли виплеск долетів: тут картинка грає мазок.</summary>
            public System.Action? Arrived;

            public int Taken(int i) => i == 0 ? Taken0 : i == 1 ? Taken1 : Taken2;
            public int After(int i) => i == 0 ? After0 : i == 1 ? After1 : After2;
        }

        [SerializeField] private DesignSystem design;
        [SerializeField] private TankView[] tanks = System.Array.Empty<TankView>();
        [SerializeField] private RectTransform vessel;
        [SerializeField] private Image track;
        [SerializeField] private Image stroke;
        [SerializeField] private Image fill;
        [SerializeField] private RectTransform fillRect;
        [SerializeField] private RectTransform orbit;
        [SerializeField] private Image[] drops = System.Array.Empty<Image>();
        [SerializeField] private Image[] streams = System.Array.Empty<Image>();
        [SerializeField] private Image splash;
        [SerializeField] private TMP_Text hueName;

        private readonly Queue<Shot> _queue = new Queue<Shot>(4);
        private readonly Vector3[] _dropHome = new Vector3[Pigments.Count];
        private Coroutine? _playing;
        private float _fill;
        private float _spin;
        private float _spinSpeed;
        private BalanceData? _balance;

        public void Apply()
        {
            if (design == null)
                return;

            if (track != null) track.color = design.MixerTrackFill;
            if (stroke != null) stroke.color = design.MixerTrackStroke;
            if (fill != null) fill.color = design.MixerTrackFill;
            if (fillRect != null) fillRect.localScale = new Vector3(1f, _fill, 1f);

            for (var i = 0; i < drops.Length && i < Pigments.Count; i++)
            {
                if (drops[i] == null)
                    continue;
                drops[i].color = design.PigmentColor(Pigments.Base[i]);
                var angle = i * Mathf.PI * 2f / Pigments.Count;
                _dropHome[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * design.MixerOrbitRadius;
                drops[i].rectTransform.localPosition = _dropHome[i];
                drops[i].rectTransform.localScale = Vector3.zero;
            }

            for (var i = 0; i < streams.Length && i < Pigments.Count; i++)
            {
                if (streams[i] == null)
                    continue;
                streams[i].color = design.PigmentColor(Pigments.Base[i]);
                streams[i].gameObject.SetActive(false);
            }

            if (splash != null)
                splash.gameObject.SetActive(false);

            if (hueName != null)
            {
                hueName.fontSize = design.FontSizeHueName;
                hueName.color = design.TextPrimary;
                hueName.fontStyle = FontStyles.Bold;
                hueName.characterSpacing = design.LetterSpacingWide;
                if (design.Font != null)
                    hueName.font = design.Font;
                hueName.gameObject.SetActive(false);
            }
        }

        /// <summary>Стан між ходами: заповнення, колір майбутнього виплеску, краплі за частками.</summary>
        public void Show(IReadOnlyList<int> levels, int capacity, BalanceData balance, bool animate)
        {
            if (design == null)
                return;
            _balance = balance;

            var total = 0;
            for (var i = 0; i < Pigments.Count; i++)
                total += levels[i];

            _fill = capacity > 0 ? Mathf.Clamp01((float)total / capacity) : 0f;
            var preview = Mixer.Resolve(levels[0], levels[1], levels[2], balance);

            if (fill != null)
                fill.color = preview == Hue.None ? design.MixerTrackFill : design.HueColor(preview);
            if (fillRect != null)
                fillRect.localScale = new Vector3(1f, _fill, 1f);
            _spinSpeed = _fill > 0f ? design.MixerIdleSpin : 0f;

            for (var i = 0; i < drops.Length && i < Pigments.Count; i++)
            {
                if (drops[i] == null)
                    continue;
                var share = total > 0 ? (float)levels[i] / total : 0f;
                var scale = levels[i] > 0 ? design.MixerDropMinScale + (1f - design.MixerDropMinScale) * share : 0f;
                drops[i].rectTransform.localScale = Vector3.one * scale;
                drops[i].rectTransform.localPosition = _dropHome[i];
            }

            _ = animate;
        }

        /// <summary>Ставить спрацювання в чергу; програється саме, без блокування поля.</summary>
        public void Fire(Shot shot)
        {
            _queue.Enqueue(shot);
            if (_playing == null && isActiveAndEnabled)
                _playing = StartCoroutine(PlayQueue());
        }

        public void StopAll()
        {
            if (_playing != null)
                StopCoroutine(_playing);
            _playing = null;
            _queue.Clear();
            for (var i = 0; i < streams.Length; i++)
                if (streams[i] != null)
                    streams[i].gameObject.SetActive(false);
            if (splash != null)
                splash.gameObject.SetActive(false);
            if (hueName != null)
                hueName.gameObject.SetActive(false);
            if (vessel != null)
                vessel.localScale = Vector3.one;
            if (orbit != null)
                orbit.localScale = Vector3.one;
        }

        private void LateUpdate()
        {
            if (orbit == null || _spinSpeed <= 0f)
                return;
            _spin += _spinSpeed * Time.deltaTime;
            orbit.localRotation = Quaternion.Euler(0f, 0f, _spin);
        }

        private IEnumerator PlayQueue()
        {
            while (_queue.Count > 0)
                yield return PlayShot(_queue.Dequeue());
            _playing = null;
        }

        private IEnumerator PlayShot(Shot shot)
        {
            // 1. Три струмені з баків у посудину — товщина за часткою.
            var streamDuration = design.MixerStreamDuration;
            var target = vessel != null ? vessel.localPosition : Vector3.zero;
            var origins = new Vector3[Pigments.Count];
            var active = 0;
            for (var i = 0; i < Pigments.Count; i++)
            {
                var stream = i < streams.Length ? streams[i] : null;
                var tank = i < tanks.Length ? tanks[i] : null;
                if (stream == null || tank == null || shot.Taken(i) <= 0)
                    continue;
                origins[i] = transform.InverseTransformPoint(tank.Rect.position);
                stream.rectTransform.localPosition = origins[i];
                var thickness = design.MixerDropMinScale
                                + (1f - design.MixerDropMinScale) * Mathf.Clamp01((float)shot.Taken(i) / Mathf.Max(1, shot.Capacity));
                stream.rectTransform.localScale = Vector3.one * thickness;
                stream.canvasRenderer.SetAlpha(1f);
                stream.gameObject.SetActive(true);
                active++;
            }

            for (var t = 0f; t < streamDuration && active > 0; t += Time.deltaTime)
            {
                var k = Mathf.Clamp01(t / streamDuration);
                var eased = design.CurveEaseInOut.Evaluate(k);
                var arc = Mathf.Sin(k * Mathf.PI) * design.MixerStreamArc;
                for (var i = 0; i < Pigments.Count; i++)
                {
                    var stream = i < streams.Length ? streams[i] : null;
                    if (stream == null || !stream.gameObject.activeSelf)
                        continue;
                    var p = Vector3.LerpUnclamped(origins[i], target, eased);
                    p.y += arc;
                    stream.rectTransform.localPosition = p;
                }
                yield return null;
            }

            for (var i = 0; i < streams.Length; i++)
                if (streams[i] != null)
                    streams[i].gameObject.SetActive(false);

            // Баки віддали своє.
            for (var i = 0; i < tanks.Length && i < Pigments.Count; i++)
                tanks[i]?.Show(shot.After(i), shot.Capacity, animate: true);

            // 2. Краплі закручуються й зливаються в центрі; посудина стискається й спалахує відтінком.
            var swirlDuration = design.MixerSwirlDuration;
            var hueColor = design.HueColor(shot.Hue);
            var fillFrom = fill != null ? fill.color : hueColor;
            var startSpin = _spin;
            for (var t = 0f; t < swirlDuration; t += Time.deltaTime)
            {
                var k = Mathf.Clamp01(t / swirlDuration);
                _spin = startSpin + design.MixerFireTurns * 360f * design.CurveEaseInOut.Evaluate(k);
                if (orbit != null)
                    orbit.localRotation = Quaternion.Euler(0f, 0f, _spin);
                var gather = 1f - k;
                for (var i = 0; i < drops.Length && i < Pigments.Count; i++)
                    if (drops[i] != null)
                        drops[i].rectTransform.localPosition = _dropHome[i] * gather;
                if (fillRect != null)
                    fillRect.localScale = new Vector3(1f, Mathf.Lerp(_fill, 1f, k), 1f);
                if (fill != null)
                    fill.canvasRenderer.SetColor(Color.Lerp(fillFrom, hueColor, k));
                var squeeze = 1f - design.MixerSqueeze * Mathf.Sin(k * Mathf.PI);
                if (vessel != null)
                    vessel.localScale = new Vector3(squeeze, 2f - squeeze, 1f);
                yield return null;
            }

            // 3. Виплеск летить угору; посудина повертається до того, що лишилось.
            var splashDuration = design.MixerSplashDuration;
            var levelsAfter = new[] { shot.After0, shot.After1, shot.After2 };
            var remaining = levelsAfter[0] + levelsAfter[1] + levelsAfter[2];
            var fillAfter = shot.Capacity > 0 ? Mathf.Clamp01((float)remaining / shot.Capacity) : 0f;
            var previewAfter = _balance != null
                ? Mixer.Resolve(levelsAfter[0], levelsAfter[1], levelsAfter[2], _balance)
                : Hue.None;
            var colorAfter = previewAfter == Hue.None ? design.MixerTrackFill : design.HueColor(previewAfter);

            var flyTo = shot.Target == Vector3.zero
                ? target + new Vector3(0f, design.MixerSplashRise, 0f)
                : transform.InverseTransformPoint(shot.Target);
            flyTo.z = 0f;
            var arrived = false;

            if (splash != null)
            {
                splash.canvasRenderer.SetColor(hueColor);
                splash.rectTransform.localPosition = target;
                splash.rectTransform.localScale = Vector3.zero;
                splash.gameObject.SetActive(true);
            }
            if (hueName != null)
            {
                hueName.text = shot.Missed ? HueNames.Of(shot.Hue) + " · МИМО" : HueNames.Of(shot.Hue);
                hueName.canvasRenderer.SetAlpha(1f);
                hueName.rectTransform.localPosition = target + new Vector3(0f, design.MixerNameOffset, 0f);
                hueName.rectTransform.localScale = Vector3.one * 0.6f;
                hueName.gameObject.SetActive(true);
            }

            for (var t = 0f; t < splashDuration; t += Time.deltaTime)
            {
                var k = Mathf.Clamp01(t / splashDuration);
                var pop = design.CurveBackOut.Evaluate(Mathf.Clamp01(k / 0.45f));
                // Мимо — згасає на півдорозі; влучив — живе до самої зони й гасне вже там.
                var fade = shot.Missed
                    ? (k < 0.35f ? 1f : Mathf.Max(0f, 1f - (k - 0.35f) / 0.3f))
                    : (k < 0.75f ? 1f : 1f - (k - 0.75f) / 0.25f);
                var flight = design.CurveEaseInOut.Evaluate(Mathf.Clamp01(k / 0.8f));
                if (!arrived && !shot.Missed && k >= 0.8f)
                {
                    arrived = true;
                    shot.Arrived?.Invoke();
                }
                if (splash != null)
                {
                    var p = Vector3.LerpUnclamped(target, flyTo, flight);
                    p.y += Mathf.Sin(flight * Mathf.PI) * design.MixerStreamArc;
                    splash.rectTransform.localScale = Vector3.one * (shot.Missed ? pop * (1f - k * 0.6f) : pop);
                    splash.rectTransform.localPosition = p;
                    splash.canvasRenderer.SetAlpha(fade);
                }
                if (hueName != null)
                {
                    hueName.rectTransform.localScale = Vector3.one * (0.6f + 0.4f * pop);
                    hueName.rectTransform.localPosition = target + new Vector3(0f, design.MixerNameOffset + design.MixerSplashRise * 0.5f * k, 0f);
                    hueName.canvasRenderer.SetAlpha(fade);
                }
                if (fillRect != null)
                    fillRect.localScale = new Vector3(1f, Mathf.Lerp(1f, fillAfter, Mathf.Clamp01(k / 0.4f)), 1f);
                if (fill != null)
                    fill.canvasRenderer.SetColor(Color.Lerp(hueColor, colorAfter, Mathf.Clamp01(k / 0.4f)));
                for (var i = 0; i < drops.Length && i < Pigments.Count; i++)
                {
                    if (drops[i] == null)
                        continue;
                    var share = remaining > 0 ? (float)levelsAfter[i] / remaining : 0f;
                    var scale = levelsAfter[i] > 0 ? design.MixerDropMinScale + (1f - design.MixerDropMinScale) * share : 0f;
                    drops[i].rectTransform.localPosition = _dropHome[i] * Mathf.Clamp01(k / 0.4f);
                    drops[i].rectTransform.localScale = Vector3.one * scale;
                }
                yield return null;
            }

            if (!arrived && !shot.Missed)
                shot.Arrived?.Invoke();

            if (splash != null)
                splash.gameObject.SetActive(false);
            if (hueName != null)
                hueName.gameObject.SetActive(false);
            if (vessel != null)
                vessel.localScale = Vector3.one;

            _fill = fillAfter;
            _spinSpeed = _fill > 0f ? design.MixerIdleSpin : 0f;
            CommitFillColor(colorAfter);
        }

        /// <summary>
        /// Разовий запис у Graphic ПІСЛЯ анімації: CanvasRenderer.SetColor живе лише до
        /// наступної перебудови, тож остаточний колір має лягти в Image.color.
        /// </summary>
        private void CommitFillColor(Color color)
        {
            if (fill != null)
                fill.color = color;
        }
    }
}
