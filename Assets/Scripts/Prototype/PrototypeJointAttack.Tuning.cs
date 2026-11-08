using System;
using System.Collections.Generic;
using BehindTheCurtain.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BehindTheCurtain.Prototype
{
    public sealed partial class PrototypeJointAttack
    {
        private Dictionary<string, double> appliedPuppetTuning;
        private string puppetTuningWarning;
        private float puppetTuningWarningUntil;
        private const float PuppetTuningWarningSeconds = 3f; // Requested presentation duration.

        private void WarnPuppetTuning(string message)
        {
            puppetTuningWarning = message;
            puppetTuningWarningUntil = Time.unscaledTime + PuppetTuningWarningSeconds;
            Debug.LogWarning(message, this);
        }
        private bool TryApplyPuppetTuning(bool defaults)
        {
            try
            {
                var tuning = PrototypePuppetTuning.Build(settings, defaults, WarnPuppetTuning, out var applied);
                var created = PuppetBrain.Create(expedition.EncounterWorld, tuning,
                    new DeterministicRandom(unchecked((ulong)settings.Seed)));
                if (!created.Success)
                {
                    WarnPuppetTuning("인형 수치 적용 거부: " + created.Failure);
                    return false;
                }
                brain = created.Value;
                brainTuning = tuning;
                appliedPuppetTuning = applied;
                return true;
            }
            catch (Exception exception)
            {
                WarnPuppetTuning("인형 수치 적용 거부: " + exception.GetBaseException().Message);
                return false;
            }
        }
        private bool ApplyPuppetTuning(bool encounterStart)
        {
            if (TryApplyPuppetTuning(false)) return true;
            if (!encounterStart) return false; // Keep the previous brain and its applied values.
            return TryApplyPuppetTuning(true);
        }
        private void UpdatePuppetTuningInput()
        {
            var keyboard = Keyboard.current;
            if (!Application.isFocused || keyboard == null || brain == null) return;
            if (keyboard.f3Key.wasPressedThisFrame)
            {
                temperamentsEnabled = !temperamentsEnabled;
                ApplyTemperaments();
                ApplyPuppetTuning(false);
            }
            if (keyboard.f5Key.wasPressedThisFrame) ApplyPuppetTuning(false);
            if (keyboard.f6Key.wasPressedThisFrame)
            {
                var differences = PrototypePuppetTuning.Differences(appliedPuppetTuning);
                var text = differences.Length == 0 ? "기본값과 같음" : string.Join(", ", differences);
                GUIUtility.systemCopyBuffer = text;
                Debug.Log(text, this);
            }
        }
        private void DrawPuppetTuningWarning()
        {
            if (Time.unscaledTime >= puppetTuningWarningUntil) return;
            GUI.Box(new Rect(8f, 38f, Screen.width - 16f, 30f), puppetTuningWarning);
        }
    }
}
