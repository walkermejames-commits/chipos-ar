using System.Text;
using UnityEngine;

namespace ChipOSMarkII
{
    public sealed partial class LayoutManager
    {
        /// <summary>
        /// Runs a lightweight self-test and prints a PASS/FAIL summary without throwing runtime errors.
        /// </summary>
        public bool RunSelfTest()
        {
            EnsureProfiles();

            var passed = 0;
            var failed = 0;
            var notes = new StringBuilder();

            RecordCheck(app != null, "App binding", ref passed, ref failed, notes);
            RecordCheck(layoutProfiles.Count > 0, "Layout profiles loaded", ref passed, ref failed, notes);
            RecordCheck(transitionDuration >= 0f, "Transition duration valid", ref passed, ref failed, notes);
            RecordCheck(CurrentMode == ResolveLayoutMode(CurrentMode), "Current layout resolves cleanly", ref passed, ref failed, notes);

            var registeredCount = 0;
            foreach (var entry in registeredPanels)
            {
                if (entry.Value == null)
                {
                    RecordCheck(false, $"Panel '{entry.Key}' reference alive", ref passed, ref failed, notes);
                    continue;
                }

                registeredCount++;
                RecordCheck(entry.Value.ValidateReferences(), $"Panel '{entry.Key}' references valid", ref passed, ref failed, notes);
            }

            RecordCheck(registeredCount > 0, "At least one panel registered", ref passed, ref failed, notes);

            if (!OptionalTextRenderer.IsTextMeshProReady)
            {
                notes.AppendLine($"INFO Optional text backend: {OptionalTextRenderer.BackendSummary}.");
            }

            var result = failed == 0 ? "PASS" : "FAIL";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[LayoutManager SelfTest] {result} | Passed: {passed} | Failed: {failed}\n{notes}", this);
#endif
            UpdateInspectorStatus($"SelfTest {result}");
            return failed == 0;
        }

        private static void RecordCheck(bool condition, string label, ref int passed, ref int failed, StringBuilder notes)
        {
            if (condition)
            {
                passed++;
                notes.AppendLine($"PASS {label}");
                return;
            }

            failed++;
            notes.AppendLine($"FAIL {label}");
        }
    }
}
