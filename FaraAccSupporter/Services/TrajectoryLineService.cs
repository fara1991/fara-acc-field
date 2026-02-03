using System;
using System.Collections.Generic;
using FaraAccSupporter.Configuration;
using UnityEngine;

namespace FaraAccSupporter.Services
{
    /// <summary>
    /// Manages trajectory line rendering from sabers to their nearest notes.
    /// Supports multiple simultaneous notes per saber.
    /// </summary>
    internal class TrajectoryLineService : IDisposable
    {
        // Maximum lines per saber (for simultaneous notes)
        private const int MaxLinesPerSaber = 4;

        private readonly List<LineData> _leftLines = new();
        private readonly List<LineData> _rightLines = new();
        private Material? _lineMaterial;

        private bool _isInitialized = false;

        // Line colors for Time Dependent notes (semi-transparent)
        private static readonly Color LeftLineColorTD = new Color(1f, 0.3f, 0.3f, 0.6f);   // Red for left saber
        private static readonly Color RightLineColorTD = new Color(0.3f, 0.3f, 1f, 0.6f);  // Blue for right saber

        // Line colors for Time Independent notes (brighter/green tint to indicate TI)
        private static readonly Color LeftLineColorTI = new Color(1f, 0.8f, 0.3f, 0.7f);   // Orange/gold for left TI
        private static readonly Color RightLineColorTI = new Color(0.3f, 1f, 0.8f, 0.7f);  // Cyan for right TI

        /// <summary>
        /// Initializes the trajectory line renderers.
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
                return;

            if (!PluginConfig.Instance.ShowTrajectoryLine)
                return;

            try
            {
                // Create material for the lines
                var shader = Shader.Find("Sprites/Default");
                if (shader == null)
                    shader = Shader.Find("UI/Default");
                if (shader == null)
                {
                    Plugin.Log?.Warn("Could not find suitable shader for trajectory lines");
                    return;
                }

                _lineMaterial = new Material(shader);
                _lineMaterial.SetInt("_ZWrite", 0);
                _lineMaterial.renderQueue = 3000;

                // Create multiple lines for each saber
                for (int i = 0; i < MaxLinesPerSaber; i++)
                {
                    _leftLines.Add(CreateLineData($"AccSupporter_LeftTrajectory_{i}", LeftLineColorTD));
                    _rightLines.Add(CreateLineData($"AccSupporter_RightTrajectory_{i}", RightLineColorTD));
                }

                _isInitialized = true;
                Plugin.Log?.Info($"TrajectoryLineService initialized with {MaxLinesPerSaber} lines per saber");
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"Failed to initialize TrajectoryLineService: {ex}");
            }
        }

        private LineData CreateLineData(string name, Color color)
        {
            var obj = new GameObject(name);
            var line = obj.AddComponent<LineRenderer>();
            SetupLineRenderer(line, color);
            return new LineData { GameObject = obj, LineRenderer = line };
        }

        private void SetupLineRenderer(LineRenderer line, Color color)
        {
            if (line == null)
                return;

            line.material = _lineMaterial;
            line.startWidth = 0.015f;
            line.endWidth = 0.01f;
            line.positionCount = 2;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0.2f); // Fade at the end
            line.useWorldSpace = true;
            line.enabled = false;

            // Make the line not cast shadows
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        /// <summary>
        /// Data class for storing line renderer and its game object
        /// </summary>
        private class LineData
        {
            public GameObject GameObject = null!;
            public LineRenderer LineRenderer = null!;
        }

        /// <summary>
        /// Target info for a trajectory line
        /// </summary>
        public struct TrajectoryTarget
        {
            public Vector3 Position;
            public bool IsTimeIndependent;
        }

        /// <summary>
        /// Updates trajectory lines for a specific saber with multiple targets.
        /// </summary>
        /// <param name="saberType">Which saber's trajectories to update</param>
        /// <param name="saberTipPosition">Current saber tip position</param>
        /// <param name="targets">List of target positions with TI info</param>
        public void UpdateTrajectories(SaberType saberType, Vector3 saberTipPosition, List<TrajectoryTarget> targets)
        {
            if (!_isInitialized || !PluginConfig.Instance.ShowTrajectoryLine)
            {
                HideAllLines(saberType);
                return;
            }

            var lines = saberType == SaberType.SaberA ? _leftLines : _rightLines;

            for (int i = 0; i < lines.Count; i++)
            {
                var lineData = lines[i];
                if (lineData?.LineRenderer == null)
                    continue;

                if (i < targets.Count)
                {
                    var target = targets[i];

                    // Check distance - only show line if note is reasonably close
                    float distance = Vector3.Distance(saberTipPosition, target.Position);
                    if (distance > 5f)
                    {
                        lineData.LineRenderer.enabled = false;
                        continue;
                    }

                    // Update line color based on TD/TI
                    Color startColor;
                    if (target.IsTimeIndependent)
                    {
                        startColor = saberType == SaberType.SaberA ? LeftLineColorTI : RightLineColorTI;
                        lineData.LineRenderer.startWidth = 0.02f;
                        lineData.LineRenderer.endWidth = 0.015f;
                    }
                    else
                    {
                        startColor = saberType == SaberType.SaberA ? LeftLineColorTD : RightLineColorTD;
                        lineData.LineRenderer.startWidth = 0.015f;
                        lineData.LineRenderer.endWidth = 0.01f;
                    }

                    // Make secondary lines slightly more transparent
                    if (i > 0)
                    {
                        startColor.a *= 0.7f;
                    }

                    Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0.15f);

                    lineData.LineRenderer.startColor = startColor;
                    lineData.LineRenderer.endColor = endColor;

                    lineData.LineRenderer.enabled = true;
                    lineData.LineRenderer.SetPosition(0, saberTipPosition);
                    lineData.LineRenderer.SetPosition(1, target.Position);
                }
                else
                {
                    // No target for this line, hide it
                    lineData.LineRenderer.enabled = false;
                }
            }
        }

        /// <summary>
        /// Updates the trajectory line for a specific saber (single target, backwards compatible).
        /// </summary>
        public void UpdateTrajectory(SaberType saberType, Vector3 saberTipPosition, Vector3? targetPosition, bool isTimeIndependent = false)
        {
            var targets = new List<TrajectoryTarget>();
            if (targetPosition.HasValue)
            {
                targets.Add(new TrajectoryTarget
                {
                    Position = targetPosition.Value,
                    IsTimeIndependent = isTimeIndependent
                });
            }
            UpdateTrajectories(saberType, saberTipPosition, targets);
        }

        /// <summary>
        /// Hides all trajectory lines for a specific saber.
        /// </summary>
        public void HideAllLines(SaberType saberType)
        {
            var lines = saberType == SaberType.SaberA ? _leftLines : _rightLines;
            foreach (var lineData in lines)
            {
                if (lineData?.LineRenderer != null)
                    lineData.LineRenderer.enabled = false;
            }
        }

        /// <summary>
        /// Hides the trajectory line for a specific saber (backwards compatible).
        /// </summary>
        public void HideLine(SaberType saberType)
        {
            HideAllLines(saberType);
        }

        /// <summary>
        /// Hides all trajectory lines.
        /// </summary>
        public void HideAllLines()
        {
            HideAllLines(SaberType.SaberA);
            HideAllLines(SaberType.SaberB);
        }

        /// <summary>
        /// Cleans up resources.
        /// </summary>
        public void Dispose()
        {
            if (_lineMaterial != null)
            {
                UnityEngine.Object.Destroy(_lineMaterial);
                _lineMaterial = null;
            }

            foreach (var lineData in _leftLines)
            {
                if (lineData?.GameObject != null)
                    UnityEngine.Object.Destroy(lineData.GameObject);
            }
            _leftLines.Clear();

            foreach (var lineData in _rightLines)
            {
                if (lineData?.GameObject != null)
                    UnityEngine.Object.Destroy(lineData.GameObject);
            }
            _rightLines.Clear();

            _isInitialized = false;
            Plugin.Log?.Info("TrajectoryLineService disposed");
        }
    }
}
