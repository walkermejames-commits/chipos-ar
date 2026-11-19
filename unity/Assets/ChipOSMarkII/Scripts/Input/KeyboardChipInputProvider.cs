using System.Collections.Generic;
using UnityEngine;

namespace ChipOSMarkII
{
    /// <summary>
    /// Keyboard-backed input provider for editor and desktop play mode.
    /// </summary>
    public sealed class KeyboardChipInputProvider : IChipInputProvider
    {
        private readonly IReadOnlyList<InputRouter.KeyBinding> bindings;
        private readonly Dictionary<ChipInputActionId, float> actionCooldowns;
        private readonly float repeatCooldownSeconds;

        public KeyboardChipInputProvider(
            IReadOnlyList<InputRouter.KeyBinding> bindings,
            Dictionary<ChipInputActionId, float> actionCooldowns,
            float repeatCooldownSeconds)
        {
            this.bindings = bindings;
            this.actionCooldowns = actionCooldowns;
            this.repeatCooldownSeconds = Mathf.Max(0.01f, repeatCooldownSeconds);
        }

        public ChipInputSourceKind SourceKind => ChipInputSourceKind.Keyboard;

        public bool TryReadCommand(float currentTime, out ChipInputCommand command)
        {
            command = default;

            if (bindings == null)
            {
                return false;
            }

            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (!Input.GetKeyDown(binding.key))
                {
                    continue;
                }

                if (actionCooldowns != null &&
                    actionCooldowns.TryGetValue(binding.action, out var nextActionTime) &&
                    currentTime < nextActionTime)
                {
                    continue;
                }

                if (actionCooldowns != null)
                {
                    actionCooldowns[binding.action] = currentTime + repeatCooldownSeconds;
                }

                command = new ChipInputCommand(binding.action, SourceKind, $"{binding.key} {binding.label}");
                return true;
            }

            return false;
        }
    }
}
