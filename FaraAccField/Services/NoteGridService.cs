using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FaraAccField.Configuration;
using UnityEngine;

namespace FaraAccField.Services
{
    internal class NoteGridService : IDisposable
    {
        private const int Columns = 4;
        private const int Rows = 3;
        private const int TotalCubes = Columns * Rows;

        // Beat Saber note grid X positions (lineIndex 0-3)
        private static readonly float[] ColumnPositions = { -0.9f, -0.3f, 0.3f, 0.9f };
        // Default Y positions (fallback if spawn data unavailable)
        private static readonly float[] DefaultRowPositions = { 0.85f, 1.40f, 1.95f };
        private readonly float[] _rowPositions = new float[Rows];

        private const float CubeScale = 0.4f;
        private const float DefaultZOffset = 0.5f;

        // Note proximity threshold for color highlighting
        private const float NoteHighlightZThreshold = 1.5f;

        // Highlight glow/pulse settings
        private const float HighlightEmissionIntensity = 1.5f;
        private const float HighlightPulseSpeed = 4f;
        private const float HighlightScaleMin = 1.0f;
        private const float HighlightScaleMax = 1.05f;

        // Default color (purple)
        private static readonly Color DefaultCubeColor = new Color(192f / 255f, 64f / 255f, 192f / 255f);

        // Note colors (set from ColorManager at runtime)
        private Color _leftNoteColor = new Color(0.8f, 0.2f, 0.2f);
        private Color _rightNoteColor = new Color(0.2f, 0.4f, 0.9f);

        private GameObject? _container;
        private GameObject[]? _cubes;
        private MeshRenderer[]? _cubeRenderers;
        private Material[]? _cubeMaterials;
        private string _shaderName = "Standard";
        private bool _isInitialized;

        private bool _rhythmMarkerAvailable;
        private float _lastZOffset = DefaultZOffset;
        private float _lastAlpha;

        // Runtime Y calibration from actual notes passing through grid Z
        private readonly bool[] _rowCalibrated = new bool[Rows];

        // Track which cubes are currently highlighted by notes
        private readonly bool[] _cubeHighlighted = new bool[TotalCubes];

        // Store the base highlight color per cube (before pulse brightness is applied)
        private readonly Color[] _highlightBaseColors = new Color[TotalCubes];

        /// <summary>
        /// Initializes the grid cubes. Colors must be provided here; they are used for highlight rendering.
        /// </summary>
        public void Initialize(Color? leftColor = null, Color? rightColor = null)
        {
            if (_isInitialized)
                return;

            if (!PluginConfig.Instance.ShowNotesGrid)
                return;

            if (leftColor.HasValue)
                _leftNoteColor = leftColor.Value;
            if (rightColor.HasValue)
                _rightNoteColor = rightColor.Value;

            try
            {
                // Copy default Y positions
                Array.Copy(DefaultRowPositions, _rowPositions, Rows);

                CheckRhythmMarkerAvailability();
                float zOffset = GetCurrentZOffset();
                _lastZOffset = zOffset;

                // Prefer unlit shaders so cubes are not affected by in-game lighting
                string[] shaderNames =
                {
                    "Sprites/Default",
                    "UI/Default",
                    "Unlit/Color",
                    "Legacy Shaders/Transparent/Diffuse",
                    "Standard"
                };

                Shader? shader = null;
                foreach (var name in shaderNames)
                {
                    shader = Shader.Find(name);
                    if (shader != null)
                    {
                        _shaderName = name;
                        break;
                    }
                }

                if (shader == null)
                {
                    Plugin.Log?.Warn("Could not find any suitable shader for note grid cubes");
                    return;
                }

                float alpha = PluginConfig.Instance.NotesGridAlpha;
                _lastAlpha = alpha;

                // Create container to parent all cubes for scene management safety
                _container = new GameObject("AccField_NoteGridContainer");
                _container.layer = 2; // Ignore Raycast

                _cubes = new GameObject[TotalCubes];
                _cubeRenderers = new MeshRenderer[TotalCubes];
                _cubeMaterials = new Material[TotalCubes];
                int index = 0;
                for (int row = 0; row < Rows; row++)
                {
                    for (int col = 0; col < Columns; col++)
                    {
                        var material = CreateCubeMaterial(shader, DefaultCubeColor, alpha);
                        _cubeMaterials[index] = material;
                        _cubes[index] = CreateGridCube(
                            $"AccField_NoteGrid_{row}_{col}",
                            ColumnPositions[col],
                            _rowPositions[row],
                            _lastZOffset,
                            material);
                        _cubeRenderers[index] = _cubes[index].GetComponent<MeshRenderer>();
                        index++;
                    }
                }

                _isInitialized = true;

                if (PluginConfig.Instance.NotesGridDebugLog)
                {
                    Plugin.Log?.Info($"NoteGridService initialized: {TotalCubes} cubes, Z={_lastZOffset:F2}, shader={_shaderName}, alpha={alpha:F2}, Y=[{_rowPositions[0]:F3}, {_rowPositions[1]:F3}, {_rowPositions[2]:F3}]");
                    for (int i = 0; i < TotalCubes; i++)
                    {
                        if (_cubes[i] != null)
                        {
                            var cubePosition = _cubes[i].transform.position;
                            int row = i / Columns;
                            int col = i % Columns;
                            Plugin.Log?.Info($"  Grid[row={row},col={col}] pos=({cubePosition.x:F3}, {cubePosition.y:F3}, {cubePosition.z:F3})");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"Failed to initialize NoteGridService: {ex}");
            }
        }

        private Material CreateCubeMaterial(Shader shader, Color color, float alpha)
        {
            var material = new Material(shader);

            if (_shaderName == "Standard")
            {
                // Fallback: configure Standard shader for transparency
                material.SetFloat("_Mode", 3f);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }

            // Ensure transparent rendering for all shaders
            material.SetInt("_ZWrite", 0);
            material.renderQueue = 3000;
            material.color = new Color(color.r, color.g, color.b, alpha);
            return material;
        }

        private GameObject CreateGridCube(string name, float x, float y, float z, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = new Vector3(x, y, z);
            cube.transform.localScale = new Vector3(CubeScale, CubeScale, CubeScale);

            // Immediately remove collider to prevent ANY physics interaction with notes
            var collider = cube.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);

            // Set layer to Ignore Raycast (2) so sabers and notes don't interact
            cube.layer = 2;

            // Parent to container for scene management safety
            if (_container != null)
                cube.transform.SetParent(_container.transform, true);

            var renderer = cube.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return cube;
        }

        /// <summary>
        /// Calibrates a row's Y position from an actual note passing near the grid Z.
        /// Called from AccFieldController when a note is close to grid Z.
        /// </summary>
        public void CalibrateYFromNote(int lineLayer, float noteY)
        {
            if (lineLayer < 0 || lineLayer >= Rows)
                return;

            if (_rowCalibrated[lineLayer])
                return;

            _rowCalibrated[lineLayer] = true;
            _rowPositions[lineLayer] = noteY;

            if (PluginConfig.Instance.NotesGridDebugLog)
                Plugin.Log?.Info($"NoteGrid Y runtime calibrated: layer {lineLayer} = {noteY:F3}");

            // Update cube positions for this row
            if (_cubes != null)
            {
                for (int col = 0; col < Columns; col++)
                {
                    int index = lineLayer * Columns + col;
                    if (_cubes[index] != null)
                    {
                        var pos = _cubes[index].transform.position;
                        _cubes[index].transform.position = new Vector3(pos.x, noteY, pos.z);
                    }
                }
            }
        }


        /// <summary>
        /// Highlights a grid cube with the note's saber color.
        /// </summary>
        public void HighlightCube(int lineIndex, int lineLayer, bool isLeftNote)
        {
            if (_cubeMaterials == null || lineIndex < 0 || lineIndex >= Columns || lineLayer < 0 || lineLayer >= Rows)
                return;

            int index = lineLayer * Columns + lineIndex;
            if (_cubeHighlighted[index])
                return;

            _cubeHighlighted[index] = true;
            var color = isLeftNote ? _leftNoteColor : _rightNoteColor;
            _highlightBaseColors[index] = color;
            float alpha = PluginConfig.Instance.NotesGridAlpha;
            _cubeMaterials[index].color = new Color(
                Mathf.Clamp01(color.r * HighlightEmissionIntensity),
                Mathf.Clamp01(color.g * HighlightEmissionIntensity),
                Mathf.Clamp01(color.b * HighlightEmissionIntensity),
                alpha);
        }

        /// <summary>
        /// Resets a grid cube back to default color and disables glow.
        /// </summary>
        public void ResetCube(int lineIndex, int lineLayer)
        {
            if (_cubeMaterials == null || lineIndex < 0 || lineIndex >= Columns || lineLayer < 0 || lineLayer >= Rows)
                return;

            int index = lineLayer * Columns + lineIndex;
            _cubeHighlighted[index] = false;
            float alpha = PluginConfig.Instance.NotesGridAlpha;
            _cubeMaterials[index].color = new Color(DefaultCubeColor.r, DefaultCubeColor.g, DefaultCubeColor.b, alpha);

            // Reset scale
            if (_cubes != null && _cubes[index] != null)
                _cubes[index].transform.localScale = Vector3.one * CubeScale;
        }

        private void CheckRhythmMarkerAvailability()
        {
            try
            {
                _rhythmMarkerAvailable = VisualHelper.IsRhythmMarkerLoaded();
                Plugin.Log?.Info(_rhythmMarkerAvailable
                    ? "FaraRhythmMarker detected"
                    : "FaraRhythmMarker not found; using default Z offset");
            }
            catch
            {
                _rhythmMarkerAvailable = false;
            }
        }

        private float GetCurrentZOffset()
        {
            if (PluginConfig.Instance.LinkRhythmMarkerZOffset && _rhythmMarkerAvailable)
            {
                try
                {
                    return ReadRhythmMarkerZOffset();
                }
                catch (Exception ex)
                {
                    Plugin.Log?.Debug($"Failed to read MarkerZOffset: {ex.Message}");
                }
            }

            return PluginConfig.Instance.NotesGridZOffset;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float ReadRhythmMarkerZOffset()
        {
            return FaraRhythmMarker.Configuration.PluginConfig.Instance.MarkerZOffset;
        }

        /// <summary>
        /// Returns the current grid Z position for note proximity checks.
        /// </summary>
        public float CurrentZOffset => _lastZOffset;

        public void Update()
        {
            var config = PluginConfig.Instance;
            bool shouldShow = config.ShowNotesGrid;

            if (shouldShow && !_isInitialized)
            {
                Initialize();
                return;
            }

            if (!_isInitialized || _cubes == null)
                return;

            // Check if Z offset changed
            float currentZ = GetCurrentZOffset();
            if (Math.Abs(currentZ - _lastZOffset) > 0.001f)
            {
                _lastZOffset = currentZ;
                UpdateZPositions(currentZ);

                // Reset Y calibration so it re-calibrates for the new Z
                for (int i = 0; i < Rows; i++)
                    _rowCalibrated[i] = false;
            }

            // Check if alpha changed
            float currentAlpha = config.NotesGridAlpha;
            if (Math.Abs(currentAlpha - _lastAlpha) > 0.001f)
            {
                _lastAlpha = currentAlpha;
                UpdateAlpha(currentAlpha);
            }

            // Check if any cube is highlighted to avoid unnecessary pulse computation
            bool anyHighlighted = false;
            for (int i = 0; i < TotalCubes; i++)
            {
                if (_cubeHighlighted[i]) { anyHighlighted = true; break; }
            }

            float pulseScale = CubeScale;
            float pulseBrightness = 1.0f;
            if (anyHighlighted)
            {
                float pulseT = (Mathf.Sin(Time.time * HighlightPulseSpeed) + 1f) * 0.5f;
                pulseScale = Mathf.Lerp(HighlightScaleMin, HighlightScaleMax, pulseT) * CubeScale;
                pulseBrightness = Mathf.Lerp(1.0f, HighlightEmissionIntensity, pulseT);
            }

            for (int i = 0; i < _cubes.Length; i++)
            {
                if (_cubes[i] == null)
                    continue;

                if (_cubeRenderers != null && _cubeRenderers[i] != null)
                    _cubeRenderers[i].enabled = shouldShow;

                if (_cubeHighlighted[i])
                {
                    _cubes[i].transform.localScale = Vector3.one * pulseScale;

                    if (_cubeMaterials != null && _cubeMaterials[i] != null)
                    {
                        var baseColor = _highlightBaseColors[i];
                        _cubeMaterials[i].color = new Color(
                            Mathf.Clamp01(baseColor.r * pulseBrightness),
                            Mathf.Clamp01(baseColor.g * pulseBrightness),
                            Mathf.Clamp01(baseColor.b * pulseBrightness),
                            _cubeMaterials[i].color.a);
                    }
                }
            }
        }

        private void UpdateYPositions()
        {
            if (_cubes == null)
                return;

            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    int index = row * Columns + col;
                    if (_cubes[index] != null)
                    {
                        var pos = _cubes[index].transform.position;
                        _cubes[index].transform.position = new Vector3(pos.x, _rowPositions[row], pos.z);
                    }
                }
            }
        }

        private void UpdateZPositions(float zOffset)
        {
            if (_cubes == null)
                return;

            foreach (var cube in _cubes)
            {
                if (cube != null)
                {
                    var position = cube.transform.position;
                    cube.transform.position = new Vector3(position.x, position.y, zOffset);
                }
            }
        }

        private void UpdateAlpha(float alpha)
        {
            if (_cubeMaterials == null)
                return;

            for (int i = 0; i < _cubeMaterials.Length; i++)
            {
                if (_cubeMaterials[i] == null)
                    continue;

                var color = _cubeMaterials[i].color;
                _cubeMaterials[i].color = new Color(color.r, color.g, color.b, alpha);
            }
        }

        public void Dispose()
        {
            // Destroy materials first (not auto-destroyed with GameObjects)
            if (_cubeMaterials != null)
            {
                foreach (var material in _cubeMaterials)
                {
                    if (material != null)
                        UnityEngine.Object.Destroy(material);
                }
                _cubeMaterials = null;
            }

            _cubes = null;
            _cubeRenderers = null;

            // Destroying container also destroys all child cubes
            if (_container != null)
            {
                UnityEngine.Object.Destroy(_container);
                _container = null;
            }

            Array.Clear(_cubeHighlighted, 0, TotalCubes);
            for (int i = 0; i < Rows; i++)
                _rowCalibrated[i] = false;
            _isInitialized = false;

            Plugin.Log?.Info("NoteGridService disposed");
        }
    }
}
