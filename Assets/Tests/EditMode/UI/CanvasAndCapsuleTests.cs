using System;
using System.Reflection;
using InkFlow.Core;
using InkFlow.Meta;
using InkFlow.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace InkFlow.Tests.UI
{
    /// <summary>
    /// Ревізія кроку 0 U1Glaze: дві вади фундаменту, які видно лише в грі, а не на знімках вітрини.
    /// (1) Канвас на будь-якому співвідношенні сторін не менший за 1080×1920 — лінійна рампа match між
    /// 9:16 і 3:4 давала на iPad mini висоту 1778, і низ екрана відрізало б. (2) Капсула нафти не губить
    /// гаманець, коли роутер ховає екран, — інакше після першого ж SetActive(false) баланс не оновлювався.
    /// </summary>
    public sealed class CanvasAndCapsuleTests
    {
        [Test]
        public void MatchFor_NeverGivesACanvasSmallerThanTheReference_OnAnyAspect()
        {
            // Пристрої стендів, iPad mini / Air, планшет 16:10, розкладний, split-screen, альбомний.
            var screens = new (int w, int h)[]
            {
                (750, 1334), (1179, 2556), (1080, 2400), (1206, 2622), (1080, 1920), (1536, 2048),
                (1488, 2266), (1640, 2360), (1200, 1920), (1812, 2176), (1080, 1180), (2400, 1080)
            };
            foreach (var (w, h) in screens)
            {
                var match = UIRoot.MatchFor(w, h);
                // Формула CanvasScaler (MatchWidthOrHeight): масштаб — лог-інтерполяція між шириною й висотою.
                var log = Mathf.Lerp(Mathf.Log(w / UIRoot.ReferenceWidth, 2f), Mathf.Log(h / UIRoot.ReferenceHeight, 2f), match);
                var scale = Mathf.Pow(2f, log);
                var units = new Vector2(w / scale, h / scale);
                Assert.GreaterOrEqual(units.x, UIRoot.ReferenceWidth - 0.5f, $"{w}×{h}: ширина канваса {units.x:0} < 1080 (match {match})");
                Assert.GreaterOrEqual(units.y, UIRoot.ReferenceHeight - 0.5f, $"{w}×{h}: висота канваса {units.y:0} < 1920 (match {match})");
            }

            // Телефони — прив'язка до ширини: 1 px макета = 2.687 одиниці на будь-якому телефоні.
            Assert.AreEqual(0f, UIRoot.MatchFor(750, 1334));
            Assert.AreEqual(0f, UIRoot.MatchFor(1080, 2400));
            Assert.AreEqual(1f, UIRoot.MatchFor(1536, 2048));
        }

        [Test]
        public void Capsule_KeepsItsWallet_AcrossHideAndShow_WithoutDoubleSubscription()
        {
            var go = new GameObject("Capsule", typeof(RectTransform));
            try
            {
                var widget = go.AddComponent<CurrencyWidget>();
                var labelGo = new GameObject("Amount", typeof(RectTransform));
                labelGo.transform.SetParent(go.transform, false);
                var label = labelGo.AddComponent<TextMeshProUGUI>();
                var so = new SerializedObject(widget);
                so.FindProperty("amountLabel").objectReferenceValue = label;
                so.ApplyModifiedPropertiesWithoutUndo();

                var wallet = new Wallet(1000);
                widget.Bind(wallet);
                Call(widget, "OnEnable");                 // екран показали (у Edit Mode рушій сам не кличе)
                Call(widget, "OnDisable");                // роутер сховав екран (HideAll)
                Call(widget, "OnEnable");                 // і показав знову (SetRoot)

                wallet.Add(240, RewardSource.Debug);
                Assert.AreEqual(ScoreFormat.Full(1240), label.text, "капсула оновлюється після SetActive(false) → (true)");
                Assert.AreEqual(1, Subscribers(wallet), "одна підписка, без дубля після повторних OnEnable");

                Call(widget, "OnDisable");
                Assert.AreEqual(0, Subscribers(wallet), "схована капсула не тримає підписку");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void Call(object target, string method) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, null);

        private static int Subscribers(Wallet wallet)
        {
            var field = typeof(Wallet).GetField("Changed", BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(wallet) is Delegate d ? d.GetInvocationList().Length : 0;
        }
    }
}
