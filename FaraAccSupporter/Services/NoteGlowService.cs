using System;
using System.Collections.Generic;
using FaraAccSupporter.Configuration;
using UnityEngine;

namespace FaraAccSupporter.Services
{
    /// <summary>
    /// Manages visual glow effects on notes when pre-swing threshold is reached.
    /// </summary>
    internal class NoteGlowService : IDisposable
    {
        // Track notes with active glow and their original colors
        private readonly Dictionary<NoteController, GlowData> _activeGlows = new();

        // Glow colors (bright, high emission)
        private static readonly Color PreSwingGlowColor = new Color(1f, 1f, 0.5f, 1f) * 2f; // Bright yellow/white

        // Glow pulse settings
        private const float GlowPulseSpeed = 8f;
        private const float GlowMinIntensity = 1.5f;
        private const float GlowMaxIntensity = 3f;

        /// <summary>
        /// Applies a glow effect to the specified note.
        /// </summary>
        public void ApplyGlow(NoteController note)
        {
            if (note?.noteTransform == null)
                return;

            if (_activeGlows.ContainsKey(note))
                return;

            try
            {
                // Find all renderers in the note
                var renderers = note.noteTransform.GetComponentsInChildren<MeshRenderer>();
                if (renderers == null || renderers.Length == 0)
                {
                    Plugin.Log?.Debug("No MeshRenderer found on note");
                    return;
                }

                var glowData = new GlowData
                {
                    Renderers = renderers,
                    OriginalColors = new Color[renderers.Length],
                    OriginalEmissions = new Color[renderers.Length],
                    StartTime = Time.time
                };

                // Store original colors and apply glow
                for (int i = 0; i < renderers.Length; i++)
                {
                    var renderer = renderers[i];
                    if (renderer?.material == null)
                        continue;

                    var material = renderer.material;

                    // Store original values
                    if (material.HasProperty("_Color"))
                        glowData.OriginalColors[i] = material.GetColor("_Color");

                    if (material.HasProperty("_EmissionColor"))
                        glowData.OriginalEmissions[i] = material.GetColor("_EmissionColor");

                    // Apply initial glow
                    ApplyGlowToMaterial(material, GlowMaxIntensity);
                }

                _activeGlows[note] = glowData;
            }
            catch (Exception ex)
            {
                Plugin.Log?.Warn($"Failed to apply note glow: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates glow effects (pulse animation).
        /// </summary>
        public void Update()
        {
            if (_activeGlows.Count == 0)
                return;

            float time = Time.time;

            foreach (var kvp in _activeGlows)
            {
                var note = kvp.Key;
                var glowData = kvp.Value;

                if (note == null || glowData.Renderers == null)
                    continue;

                // Calculate pulse intensity
                float elapsed = time - glowData.StartTime;
                float pulse = Mathf.Lerp(GlowMinIntensity, GlowMaxIntensity,
                    (Mathf.Sin(elapsed * GlowPulseSpeed) + 1f) * 0.5f);

                // Apply pulsing glow to all renderers
                foreach (var renderer in glowData.Renderers)
                {
                    if (renderer?.material != null)
                    {
                        ApplyGlowToMaterial(renderer.material, pulse);
                    }
                }
            }
        }

        /// <summary>
        /// Removes glow effect from a note.
        /// </summary>
        public void RemoveGlow(NoteController note)
        {
            if (note == null || !_activeGlows.TryGetValue(note, out var glowData))
                return;

            try
            {
                // Restore original colors
                for (int i = 0; i < glowData.Renderers.Length; i++)
                {
                    var renderer = glowData.Renderers[i];
                    if (renderer?.material == null)
                        continue;

                    var material = renderer.material;

                    if (material.HasProperty("_Color") && i < glowData.OriginalColors.Length)
                        material.SetColor("_Color", glowData.OriginalColors[i]);

                    if (material.HasProperty("_EmissionColor") && i < glowData.OriginalEmissions.Length)
                        material.SetColor("_EmissionColor", glowData.OriginalEmissions[i]);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.Debug($"Error restoring note color: {ex.Message}");
            }

            _activeGlows.Remove(note);
        }

        /// <summary>
        /// Checks if a note currently has glow applied.
        /// </summary>
        public bool HasGlow(NoteController note)
        {
            return note != null && _activeGlows.ContainsKey(note);
        }

        private void ApplyGlowToMaterial(Material material, float intensity)
        {
            if (material == null)
                return;

            // Try different shader property names used in Beat Saber
            Color glowColor = PreSwingGlowColor * intensity;

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", glowColor);
            }

            if (material.HasProperty("_Glow"))
            {
                material.SetFloat("_Glow", intensity);
            }

            if (material.HasProperty("_Bloom"))
            {
                material.SetFloat("_Bloom", intensity * 0.5f);
            }

            // Also brighten the base color slightly
            if (material.HasProperty("_Color"))
            {
                Color baseColor = material.GetColor("_Color");
                Color brightened = Color.Lerp(baseColor, Color.white, 0.3f);
                material.SetColor("_Color", brightened);
            }
        }

        /// <summary>
        /// Clears all glow effects.
        /// </summary>
        public void Clear()
        {
            // Try to restore original colors before clearing
            foreach (var kvp in _activeGlows)
            {
                RemoveGlow(kvp.Key);
            }
            _activeGlows.Clear();
        }

        public void Dispose()
        {
            Clear();
        }

        private class GlowData
        {
            public MeshRenderer[] Renderers = null!;
            public Color[] OriginalColors = null!;
            public Color[] OriginalEmissions = null!;
            public float StartTime;
        }
    }
}
