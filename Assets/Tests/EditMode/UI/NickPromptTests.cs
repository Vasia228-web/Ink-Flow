using InkFlow.Editor;
using InkFlow.Meta;
using NUnit.Framework;
using UnityEngine;

namespace InkFlow.UI.Tests
{
    /// <summary>
    /// Діалог ніка (§14) на живому об'єкті, зібраному тією самою фабрикою, що й головна сцена: невдалий нік
    /// не закриває діалог, а каже чому; добрий — віддає нормалізований нік і закривається.
    /// </summary>
    public sealed class NickPromptTests
    {
        [Test]
        public void Confirm_RefusesWithAMessage_AndAcceptsAGoodNick()
        {
            using var rig = MetaScreenRig<ProfileScreen>.Create(ScreenRigBase.Devices[0], "ProfileScreen");
            var prompt = BuildMainScene.BuildNickPrompt(rig.Safe.gameObject, rig.Design);
            prompt.Bind(NickRules.Default);

            string? confirmed = null;
            prompt.Show("Гравець", nick => confirmed = nick);
            Assert.IsTrue(prompt.IsOpen);

            prompt.PreviewSetText("Ян");
            prompt.Confirm();
            Assert.IsTrue(prompt.IsOpen, "закороткий нік не закриває діалог");
            Assert.AreEqual(NickPrompt.Message(NickVerdict.TooShort, NickRules.Default), prompt.Error);
            Assert.IsNull(confirmed);

            prompt.PreviewSetText("xyйло");
            prompt.Confirm();
            Assert.AreEqual(NickPrompt.Message(NickVerdict.Offensive, NickRules.Default), prompt.Error);
            Assert.IsNull(confirmed);

            Assert.IsFalse(prompt.CanSave, "S10_03: заборонене слово — «Зберегти» вимкнена");
            Assert.AreEqual("5 / 16", prompt.Counter, "лічильник символів");

            prompt.PreviewSetText("Космічний_Маляр2");
            Assert.IsTrue(prompt.CanSave, "S10_04: межа довжини — нік валідний");
            Assert.AreEqual("Максимум 16 символів", prompt.Hint, "бурштинове попередження, не помилка");
            Assert.AreEqual(string.Empty, prompt.Error);

            prompt.PreviewSetText("Гравець");
            Assert.AreEqual(NickPrompt.HintText, prompt.Hint, "S10_02: звичайна підказка");

            prompt.PreviewSetText("  Нова   Зоря ");
            prompt.Confirm();
            Assert.AreEqual("Нова Зоря", confirmed, "нормалізований нік іде в колбек");
            Assert.IsFalse(prompt.IsOpen, "добрий нік закриває діалог");

            Object.DestroyImmediate(prompt.gameObject);
        }
    }
}
