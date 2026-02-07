using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FaraAccSupporter.Configuration;
using UnityEngine;

namespace FaraAccSupporter.Services
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

        // Default color (white/gray)
        private static readonly Color DefaultCubeColor = new Color(0.8f, 0.8f, 0.8f);
        // Note colors
        private static readonly Color LeftNoteColor = new Color(0.8f, 0.2f, 0.2f);   // Red
        private static readonly Color RightNoteColor = new Color(0.2f, 0.4f, 0.9f);  // Blue

        private GameObject? _container;
        private GameObject[]? _cubes;
        private Material[]? _cubeMaterials; // Individual materials per cube for coloring
        private string _shaderName = "Standard";
        private bool _isInitialized;

        private bool _rhythmMarkerAvailable;
        private float _lastZOffset = DefaultZOffset;
        private float _lastAlpha;

        // Runtime Y calibration from actual notes passing through grid Z
        private readonly bool[] _rowCalibrated = new bool[Rows];

        // Track which cubes are currently highlighted by notes
        private readonly HashSet<int> _highlightedCubes = new();

        public void Initialize()
        {
            if (_isInitialized)
                return;

            if (!PluginConfig.Instance.ShowNoteGrid)
                return;

            try
            {
                // Copy default Y positions
                Array.Copy(DefaultRowPositions, _rowPositions, Rows);

                CheckRhythmMarkerAvailability();
                float zOffset = GetCurrentZOffset();
                _lastZOffset = zOffset;

                string[] shaderNames =
                {
                    "Standard",
                    "Legacy Shaders/Transparent/Diffuse",
                    "Unlit/Color",
                    "Sprites/Default",
                    "UI/Default"
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

                float alpha = PluginConfig.Instance.NoteGridAlpha;
                _lastAlpha = alpha;

                // Create container to parent all cubes for scene management safety
                _container = new GameObject("AccSupporter_NoteGridContainer");
                _container.layer = 2; // Ignore Raycast

                _cubes = new GameObject[TotalCubes];
                _cubeMaterials = new Material[TotalCubes];
                int index = 0;
                for (int row = 0; row < Rows; row++)
                {
                    for (int col = 0; col < Columns; col++)
                    {
                        var material = CreateCubeMaterial(shader, DefaultCubeColor, alpha);
                        _cubeMaterials[index] = material;
                        _cubes[index] = CreateGridCube(
                            $"AccSupporter_NoteGrid_{row}_{col}",
                            ColumnPositions[col],
                            _rowPositions[row],
                            _lastZOffset,
                            material);
                        index++;
                    }
                }

                _isInitialized = true;

                if (PluginConfig.Instance.NoteGridDebugLog)
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
                material.SetFloat("_Mode", 3f);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = 3000;
            }
            else
            {
                material.SetInt("_ZWrite", 0);
                material.renderQueue = 3000;
            }

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
        /// Called from AccSupporterController when a note is close to grid Z.
        /// </summary>
        public void CalibrateYFromNote(int lineLayer, float noteY)
        {
            if (lineLayer < 0 || lineLayer >= Rows)
                return;

            if (_rowCalibrated[lineLayer])
                return;

            _rowCalibrated[lineLayer] = true;
            _rowPositions[lineLayer] = noteY;

            if (PluginConfig.Instance.NoteGridDebugLog)
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
            if (_highlightedCubes.Contains(index))
                return;

            _highlightedCubes.Add(index);
            var color = isLeftNote ? LeftNoteColor : RightNoteColor;
            float alpha = PluginConfig.Instance.NoteGridAlpha;
            _cubeMaterials[index].color = new Color(color.r, color.g, color.b, Mathf.Min(alpha + 0.2f, 1f));
        }

        /// <summary>
        /// Resets a grid cube back to default color.
        /// </summary>
        public void ResetCube(int lineIndex, int lineLayer)
        {
            if (_cubeMaterials == null || lineIndex < 0 || lineIndex >= Columns || lineLayer < 0 || lineLayer >= Rows)
                return;

            int index = lineLayer * Columns + lineIndex;
            _highlightedCubes.Remove(index);
            float alpha = PluginConfig.Instance.NoteGridAlpha;
            _cubeMaterials[index].color = new Color(DefaultCubeColor.r, DefaultCubeColor.g, DefaultCubeColor.b, alpha);
        }

        private void CheckRhythmMarkerAvailability()
        {
            try
            {
                _rhythmMarkerAvailable = IsRhythmMarkerLoaded();
                Plugin.Log?.Info(_rhythmMarkerAvailable
                    ? "FaraRhythmMarker detected"
                    : "FaraRhythmMarker not found; using default Z offset");
            }
            catch
            {
                _rhythmMarkerAvailable = false;
            }
        }

        private static bool IsRhythmMarkerLoaded()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "FaraRhythmMarker")
                    return true;
            }
            return false;
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

            return DefaultZOffset;
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
            bool shouldShow = PluginConfig.Instance.ShowNoteGrid;

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
            float currentAlpha = PluginConfig.Instance.NoteGridAlpha;
            if (Math.Abs(currentAlpha - _lastAlpha) > 0.001f)
            {
                _lastAlpha = currentAlpha;
                UpdateAlpha(currentAlpha);
            }

            // Update visibility
            foreach (var cube in _cubes)
            {
                if (cube != null)
                {
                    var renderer = cube.GetComponent<MeshRenderer>();
                    if (renderer != null)
                        renderer.enabled = shouldShow;
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
                // Highlighted cubes get slightly higher alpha
                float cubeAlpha = _highlightedCubes.Contains(i) ? Mathf.Min(alpha + 0.2f, 1f) : alpha;
                _cubeMaterials[i].color = new Color(color.r, color.g, color.b, cubeAlpha);
            }
        }

        public void Dispose()
        {
            if (_cubes != null)
            {
                foreach (var cube in _cubes)
                {
                    if (cube != null)
                        UnityEngine.Object.Destroy(cube);
                }
                _cubes = null;
            }

            if (_cubeMaterials != null)
            {
                foreach (var material in _cubeMaterials)
                {
                    if (material != null)
                        UnityEngine.Object.Destroy(material);
                }
                _cubeMaterials = null;
            }

            if (_container != null)
            {
                UnityEngine.Object.Destroy(_container);
                _container = null;
            }

            _highlightedCubes.Clear();
            for (int i = 0; i < Rows; i++)
                _rowCalibrated[i] = false;
            _isInitialized = false;

            Plugin.Log?.Info("NoteGridService disposed");
        }
    }
}
