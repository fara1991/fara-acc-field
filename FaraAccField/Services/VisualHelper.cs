using System;
using System.Reflection;
using UnityEngine;

namespace FaraAccField.Services
{
    internal static class VisualHelper
    {
        private static bool? _customNotesAvailable;
        private const float kMaxCenterDistance = 0.3f;

        public static float CalculateSphereRadius(int centerTarget)
        {
            centerTarget = Mathf.Clamp(centerTarget, 1, 15);
            return kMaxCenterDistance * (15.5f - centerTarget) / 15f;
        }

        public static Mesh CreateTriangleMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(0, 0.667f, 0),
                new Vector3(-0.3f, -0.333f, 0),
                new Vector3(0.3f, -0.333f, 0),
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 1, 2 };
            mesh.RecalculateNormals();
            return mesh;
        }

        public static void UpdateAxisLine(LineRenderer? axis, Vector3 center, Vector3 direction,
            Color color, float halfLength, float width)
        {
            if (axis == null)
                return;

            axis.enabled = true;
            axis.startWidth = width;
            axis.endWidth = width;
            axis.startColor = color;
            axis.endColor = color;
            axis.SetPosition(0, center - direction * halfLength);
            axis.SetPosition(1, center + direction * halfLength);
        }

        public static bool IsRhythmMarkerLoaded()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "FaraRhythmMarker")
                    return true;
            }
            return false;
        }

        public static bool IsCustomNotesLoaded()
        {
            if (_customNotesAvailable.HasValue)
                return _customNotesAvailable.Value;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "CustomNotes")
                {
                    _customNotesAvailable = true;
                    return true;
                }
            }
            _customNotesAvailable = false;
            return false;
        }

        private static object? GetCustomNotesConfigInstance()
        {
            // CustomNotes config access pattern differs between versions.
            // Scan the assembly for a PluginConfig type, then find its singleton
            // via static property/field on PluginConfig itself or any other type.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != "CustomNotes")
                    continue;

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types!;
                }

                Type? configType = null;
                foreach (var t in types)
                {
                    if (t != null && t.Name == "PluginConfig")
                    {
                        configType = t;
                        break;
                    }
                }

                if (configType == null)
                    continue;

                var allFlags = BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.Static | BindingFlags.FlattenHierarchy;

                // Try static property/field on PluginConfig itself
                var prop = configType.GetProperty("Instance", allFlags)
                    ?? configType.GetProperty("instance", allFlags);
                if (prop != null)
                    return prop.GetValue(null);

                var field = configType.GetField("Instance", allFlags)
                    ?? configType.GetField("instance", allFlags)
                    ?? configType.GetField("_instance", allFlags);
                if (field != null)
                    return field.GetValue(null);

                // Scan all types for a static field/property of type PluginConfig
                // (IPA pattern: e.g. LayerUtils.pluginConfig)
                foreach (var t in types)
                {
                    if (t == null)
                        continue;

                    try
                    {
                        foreach (var fi in t.GetFields(allFlags))
                        {
                            if (fi.FieldType == configType)
                            {
                                var inst = fi.GetValue(null);
                                if (inst != null)
                                    return inst;
                            }
                        }

                        foreach (var pi in t.GetProperties(allFlags))
                        {
                            if (pi.PropertyType == configType && pi.GetGetMethod(true)?.IsStatic == true)
                            {
                                var inst = pi.GetValue(null);
                                if (inst != null)
                                    return inst;
                            }
                        }
                    }
                    catch
                    {
                        // Skip types that fail to reflect
                    }
                }
            }
            return null;
        }

        public static float ReadCustomNotesNoteSize()
        {
            var config = GetCustomNotesConfigInstance();
            if (config == null) return 1f;
            var prop = config.GetType().GetProperty("NoteSize")
                ?? config.GetType().GetProperty("noteSize");
            if (prop == null) return 1f;
            return (float)(prop.GetValue(config) ?? 1f);
        }

        public static bool ReadCustomNotesAutoDisable()
        {
            var config = GetCustomNotesConfigInstance();
            if (config == null) return false;
            var prop = config.GetType().GetProperty("AutoDisable")
                ?? config.GetType().GetProperty("autoDisable");
            if (prop == null) return false;
            return (bool)(prop.GetValue(config) ?? false);
        }

        /// <summary>
        /// Returns the effective note scale from Custom Notes.
        /// Returns 1.0 if Custom Notes is not installed.
        /// When AutoDisable is true, caller must check per-level whether custom notes are active.
        /// </summary>
        public static (float noteSize, bool autoDisable) GetCustomNotesSettings()
        {
            if (!IsCustomNotesLoaded())
                return (1f, false);

            try
            {
                float noteSize = ReadCustomNotesNoteSize();
                bool autoDisable = ReadCustomNotesAutoDisable();
                return (noteSize, autoDisable);
            }
            catch
            {
                return (1f, false);
            }
        }
    }
}
