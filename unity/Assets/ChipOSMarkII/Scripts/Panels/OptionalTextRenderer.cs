using System;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace ChipOSMarkII
{
    internal static class OptionalTextRenderer
    {
        private static readonly Type TmpTextType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
        private static readonly Type TmpSettingsType = Type.GetType("TMPro.TMP_Settings, Unity.TextMeshPro");
        private static readonly Type TmpAlignmentType = Type.GetType("TMPro.TextAlignmentOptions, Unity.TextMeshPro");
        private static readonly Type TmpWrappingType = Type.GetType("TMPro.TextWrappingModes, Unity.TextMeshPro");

        public static bool IsTextMeshProAvailable => TmpTextType != null && TmpSettingsType != null;
        public static bool IsTextMeshProReady => IsTextMeshProAvailable && GetDefaultFontAsset() != null;
        public static string BackendSummary => IsTextMeshProReady ? "TMP" : "Legacy UI Text";

        public static bool TryCreatePreferredText(
            GameObject host,
            int fontSize,
            Color color,
            TextAnchor alignment,
            out Component richTextComponent,
            out PropertyInfo richTextProperty)
        {
            richTextComponent = null;
            richTextProperty = null;

            if (!IsTextMeshProReady || host == null)
            {
                return false;
            }

            try
            {
                richTextComponent = host.AddComponent(TmpTextType);
                richTextProperty = TmpTextType.GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
                ConfigureRichText(richTextComponent, fontSize, color, alignment);
                return richTextProperty != null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[OptionalTextRenderer] TMP setup failed and will fall back to UI Text. {exception.Message}");

                if (richTextComponent != null)
                {
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(richTextComponent);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(richTextComponent);
                    }
                }

                richTextComponent = null;
                richTextProperty = null;
                return false;
            }
        }

        public static void SetText(Component richTextComponent, PropertyInfo richTextProperty, string value)
        {
            if (richTextComponent == null || richTextProperty == null)
            {
                return;
            }

            richTextProperty.SetValue(richTextComponent, value ?? string.Empty, null);
        }

        private static void ConfigureRichText(Component richTextComponent, int fontSize, Color color, TextAnchor alignment)
        {
            if (richTextComponent == null)
            {
                return;
            }

            SetProperty(richTextComponent, "fontSize", Convert.ToSingle(fontSize, CultureInfo.InvariantCulture));
            SetProperty(richTextComponent, "color", color);
            SetProperty(richTextComponent, "margin", Vector4.zero);
            SetEnumProperty(richTextComponent, "alignment", TmpAlignmentType, ConvertAlignmentName(alignment));

            if (!SetEnumProperty(richTextComponent, "textWrappingMode", TmpWrappingType, "Normal"))
            {
                SetProperty(richTextComponent, "enableWordWrapping", true);
            }
        }

        private static UnityEngine.Object GetDefaultFontAsset()
        {
            if (TmpSettingsType == null)
            {
                return null;
            }

            try
            {
                var property = TmpSettingsType.GetProperty("defaultFontAsset", BindingFlags.Static | BindingFlags.Public);
                if (property != null)
                {
                    return property.GetValue(null, null) as UnityEngine.Object;
                }

                var field = TmpSettingsType.GetField("defaultFontAsset", BindingFlags.Static | BindingFlags.Public);
                return field != null ? field.GetValue(null) as UnityEngine.Object : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[OptionalTextRenderer] Failed to resolve TMP default font asset: {ex.Message}");
                return null;
            }
        }

        private static bool SetProperty(Component component, string propertyName, object value)
        {
            var property = component.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !property.CanWrite)
            {
                return false;
            }

            var convertedValue = ConvertValue(property.PropertyType, value);
            property.SetValue(component, convertedValue, null);
            return true;
        }

        private static bool SetEnumProperty(Component component, string propertyName, Type enumType, string enumName)
        {
            if (component == null || enumType == null || string.IsNullOrWhiteSpace(enumName))
            {
                return false;
            }

            if (!enumType.IsEnum)
            {
                return false;
            }

            try
            {
                var enumValue = Enum.Parse(enumType, enumName, true);
                return SetProperty(component, propertyName, enumValue);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[OptionalTextRenderer] Failed to set enum property '{propertyName}' to '{enumName}': {ex.Message}");
                return false;
            }
        }

        private static object ConvertValue(Type targetType, object value)
        {
            if (targetType == null)
            {
                return value;
            }

            if (value == null)
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }

            var valueType = value.GetType();
            if (targetType.IsAssignableFrom(valueType))
            {
                return value;
            }

            if (targetType.IsEnum && value is string stringValue)
            {
                return Enum.Parse(targetType, stringValue, true);
            }

            if (targetType == typeof(float))
            {
                return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(int))
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(bool))
            {
                return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            }

            return value;
        }

        private static string ConvertAlignmentName(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft:
                    return "TopLeft";
                case TextAnchor.UpperCenter:
                    return "Top";
                case TextAnchor.UpperRight:
                    return "TopRight";
                case TextAnchor.MiddleLeft:
                    return "Left";
                case TextAnchor.MiddleCenter:
                    return "Center";
                case TextAnchor.MiddleRight:
                    return "Right";
                case TextAnchor.LowerLeft:
                    return "BottomLeft";
                case TextAnchor.LowerCenter:
                    return "Bottom";
                case TextAnchor.LowerRight:
                    return "BottomRight";
                default:
                    return "TopLeft";
            }
        }
    }
}
