using System;
using System.Collections.Generic;
using UnityEngine;

namespace FaraAccField.Services
{
    /// <summary>
    /// Manages visual glow effects on notes when pre-swing threshold is reached.
    /// Creates a semi-transparent cube slightly larger than the note, parented to it.
    /// </summary>
    internal class NoteGlowService : IDisposable
    {
        private readonly Dictionary<NoteController, GlowData> _activeGlows = new();

        private const float GlowScale = 1.01f;
        private const float GlowAlpha = 0.5f;

        private Color _leftColor = new Color(0.8f, 0.2f, 0.2f);
        private Color _rightColor = new Color(0.2f, 0.4f, 0.9f);

        private Material? _leftMaterial;
        private Material? _rightMaterial;
        private bool _initialized;

        public void Initialize(Color? leftColor = null, Color? rightColor = null)
        {
            if (_initialized)
                return;

            if (leftColor.HasValue)
                _leftColor = leftColor.Value;
            if (rightColor.HasValue)
                _rightColor = rightColor.Value;

            string[] shaderNames =
            {
                "Particles/Additive",
                "Sprites/Default",
                "UI/Default",
                "Legacy Shaders/Transparent/Diffuse",
                "Standard"
            };

            Shader? shader = null;
            string usedName = "";
            foreach (var name in shaderNames)
            {
                shader = Shader.Find(name);
                if (shader != null)
                {
                    usedName = name;
                    break;
                }
            }

            if (shader == null)
            {
                Plugin.Log?.Warn("NoteGlowService: No suitable shader found");
                return;
            }

            _leftMaterial = CreateGlowMaterial(shader, _leftColor);
            _rightMaterial = CreateGlowMaterial(shader, _rightColor);
            _initialized = true;

            Plugin.Log?.Info($"NoteGlowService initialized: shader={usedName}");
        }

        private static Material CreateGlowMaterial(Shader shader, Color color)
        {
            var material = new Material(shader);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = 3100;
            material.color = new Color(color.r, color.g, color.b, GlowAlpha);
            return material;
        }

        /// <summary>
        /// Creates a glow cube matching the note's visual mesh size * 1.01,
        /// parented to noteTransform so it follows the note.
        /// </summary>
        public void ApplyGlow(NoteController note)
        {
            if (!_initialized || note?.noteTransform == null || note.noteData == null)
                return;

            if (_activeGlows.ContainsKey(note))
                return;

            try
            {
                // Find the visual mesh to determine actual note size
                var meshRenderer = note.noteTransform.GetComponentInChildren<MeshRenderer>();
                if (meshRenderer == null)
                    return;

                var meshFilter = meshRenderer.GetComponent<MeshFilter>();
                if (meshFilter?.sharedMesh == null)
                    return;

                bool isLeft = note.noteData.colorType == ColorType.ColorA;
                var material = isLeft ? _leftMaterial : _rightMaterial;
                if (material == null)
                    return;

                // Calculate visual size from mesh local bounds * transform scale
                Bounds meshBounds = meshFilter.sharedMesh.bounds;
                Vector3 meshScale = meshRenderer.transform.lossyScale;
                Vector3 visualSize = new Vector3(
                    meshBounds.size.x * Mathf.Abs(meshScale.x),
                    meshBounds.size.y * Mathf.Abs(meshScale.y),
                    meshBounds.size.z * Mathf.Abs(meshScale.z));

                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "AccField_NoteGlow";
                cube.layer = 2;

                var collider = cube.GetComponent<Collider>();
                if (collider != null)
                    UnityEngine.Object.DestroyImmediate(collider);

                // Position at the visual mesh center, match rotation, scale to visual size * 1.01
                Vector3 meshWorldCenter = meshRenderer.transform.TransformPoint(meshBounds.center);
                cube.transform.position = meshWorldCenter;
                cube.transform.rotation = meshRenderer.transform.rotation;
                cube.transform.localScale = visualSize * GlowScale;

                // Parent to note for automatic position tracking (preserve world transform)
                cube.transform.SetParent(note.noteTransform, true);

                var glowRenderer = cube.GetComponent<MeshRenderer>();
                if (glowRenderer != null)
                {
                    glowRenderer.sharedMaterial = material;
                    glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    glowRenderer.receiveShadows = false;
                }

                _activeGlows[note] = new GlowData { GlowCube = cube };
            }
            catch (Exception ex)
            {
                Plugin.Log?.Warn($"Failed to apply note glow: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes stale entries for destroyed notes.
        /// </summary>
        public void Update()
        {
            if (_activeGlows.Count == 0)
                return;

            List<NoteController>? staleKeys = null;

            foreach (var kvp in _activeGlows)
            {
                if (kvp.Key == null || kvp.Value.GlowCube == null)
                {
                    staleKeys ??= new List<NoteController>();
                    staleKeys.Add(kvp.Key!);
                }
            }

            if (staleKeys != null)
            {
                foreach (var key in staleKeys)
                {
                    if (_activeGlows.TryGetValue(key, out var data) && data.GlowCube != null)
                        UnityEngine.Object.Destroy(data.GlowCube);
                    _activeGlows.Remove(key);
                }
            }
        }

        /// <summary>
        /// Removes glow effect from a note.
        /// </summary>
        public void RemoveGlow(NoteController note)
        {
            if (note == null || !_activeGlows.TryGetValue(note, out var data))
                return;

            if (data.GlowCube != null)
                UnityEngine.Object.Destroy(data.GlowCube);

            _activeGlows.Remove(note);
        }

        public bool HasGlow(NoteController note)
        {
            return note != null && _activeGlows.ContainsKey(note);
        }

        /// <summary>
        /// Clears all glow effects.
        /// </summary>
        public void Clear()
        {
            foreach (var kvp in _activeGlows)
            {
                if (kvp.Value.GlowCube != null)
                    UnityEngine.Object.Destroy(kvp.Value.GlowCube);
            }
            _activeGlows.Clear();
        }

        public void Dispose()
        {
            Clear();

            if (_leftMaterial != null)
            {
                UnityEngine.Object.Destroy(_leftMaterial);
                _leftMaterial = null;
            }
            if (_rightMaterial != null)
            {
                UnityEngine.Object.Destroy(_rightMaterial);
                _rightMaterial = null;
            }

            _initialized = false;
        }

        private class GlowData
        {
            public GameObject GlowCube = null!;
        }
    }
}
