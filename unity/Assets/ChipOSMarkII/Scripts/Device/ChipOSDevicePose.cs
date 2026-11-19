using System;
using UnityEngine;

namespace ChipOSMarkII
{
    /// <summary>
    /// Compact pose model used by simulated and future XR device adapters.
    /// </summary>
    [Serializable]
    public struct ChipOSDevicePose
    {
        public Vector3 position;
        public Vector3 eulerAngles;

        public ChipOSDevicePose(Vector3 position, Vector3 eulerAngles)
        {
            this.position = position;
            this.eulerAngles = eulerAngles;
        }

        public float Pitch => NormalizeAngle(eulerAngles.x);
        public float Yaw => NormalizeAngle(eulerAngles.y);

        public void Sanitize()
        {
            position.x = SanitizeFloat(position.x);
            position.y = SanitizeFloat(position.y);
            position.z = SanitizeFloat(position.z);

            eulerAngles.x = SanitizeFloat(eulerAngles.x);
            eulerAngles.y = SanitizeFloat(eulerAngles.y);
            eulerAngles.z = SanitizeFloat(eulerAngles.z);
        }

        public string ToCompactSummary()
        {
            return $"Yaw {Yaw:0} | Pitch {Pitch:0} | Pos {position.x:0.00},{position.y:0.00},{position.z:0.00}";
        }

        private static float SanitizeFloat(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }

        private static float NormalizeAngle(float degrees)
        {
            var normalized = degrees;
            while (normalized > 180f)
            {
                normalized -= 360f;
            }

            while (normalized < -180f)
            {
                normalized += 360f;
            }

            return normalized;
        }
    }
}
