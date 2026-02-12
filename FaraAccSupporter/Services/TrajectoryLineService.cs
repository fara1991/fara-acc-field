using System;
using System.Collections.Generic;
using FaraAccSupporter.Configuration;
using UnityEngine;

namespace FaraAccSupporter.Services
{
    /// <summary>
    /// Manages trajectory line rendering from sabers to their nearest notes.
    /// Each target note gets a sphere indicator at its center with X/Y/Z axis lines.
    /// </summary>
    internal class TrajectoryLineService : IDisposable
    {
        private const int MaxLinesPerSaber = 4;

        // Beat Saber center scoring: score = 15 * (1 - distance/0.3)
        private const float kMaxCenterDistance = 0.3f;

        // X/Y/Z axis lines through sphere center
        private const float AxisLineExtension = 0.05f;
        private const float AxisLineWidth = 0.005f;

        private readonly List<LineData> _leftLines = new();
        private readonly List<LineData> _rightLines = new();
        private Material? _lineMaterial;

        private bool _isInitialized = false;

        // Line colors (set from ColorManager at runtime)
        private Color _leftLineColor = new Color(1f, 0.3f, 0.3f, 0.6f);
        private Color _rightLineColor = new Color(0.3f, 0.3f, 1f, 0.6f);

        /// <summary>
        /// Initializes the trajectory line renderers, spheres, and axis lines.
        /// Colors must be provided here; they are used to create line/sphere materials.
        /// </summary>
        public void Initialize(Color? leftColor = null, Color? rightColor = null)
        {
            if (_isInitialized)
                return;

            if (!PluginConfig.Instance.ShowTrajectoryLine)
                return;

            if (leftColor.HasValue)
                _leftLineColor = new Color(leftColor.Value.r, leftColor.Value.g, leftColor.Value.b, 0.6f);
            if (rightColor.HasValue)
                _rightLineColor = new Color(rightColor.Value.r, rightColor.Value.g, rightColor.Value.b, 0.6f);

            try
            {
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

                for (int i = 0; i < MaxLinesPerSaber; i++)
                {
                    _leftLines.Add(CreateLineData($"AccSupporter_LeftTrajectory_{i}", _leftLineColor));
                    _rightLines.Add(CreateLineData($"AccSupporter_RightTrajectory_{i}", _rightLineColor));
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

            var (sphere, sphereRenderer) = CreateTargetSphere($"{name}_Sphere", color);

            return new LineData
            {
                GameObject = obj,
                LineRenderer = line,
                Sphere = sphere,
                SphereRenderer = sphereRenderer,
                AxisX = CreateAxisLine($"{name}_AxisX", color, sphere),
                AxisY = CreateAxisLine($"{name}_AxisY", color, sphere),
                AxisZ = CreateAxisLine($"{name}_AxisZ", color, sphere)
            };
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
            line.endColor = new Color(color.r, color.g, color.b, 0.2f);
            line.useWorldSpace = true;
            line.enabled = false;

            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        private (GameObject sphere, MeshRenderer? renderer) CreateTargetSphere(string name, Color color)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            sphere.transform.localScale = Vector3.one * 0.02f;

            var collider = sphere.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);

            sphere.layer = 2; // Ignore Raycast

            var renderer = sphere.GetComponent<MeshRenderer>();
            if (renderer != null && _lineMaterial != null)
            {
                var material = new Material(_lineMaterial);
                material.color = new Color(color.r, color.g, color.b, 0.8f);
                renderer.material = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false;
            }

            return (sphere, renderer);
        }

        private LineRenderer? CreateAxisLine(string name, Color color, GameObject parent)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            obj.layer = 2;

            var line = obj.AddComponent<LineRenderer>();
            if (line == null || _lineMaterial == null)
                return null;

            line.material = _lineMaterial;
            line.startWidth = AxisLineWidth;
            line.endWidth = AxisLineWidth;
            line.positionCount = 2;
            float alpha = PluginConfig.Instance.NoteGridAlpha;
            var axisColor = new Color(color.r, color.g, color.b, alpha);
            line.startColor = axisColor;
            line.endColor = axisColor;
            line.useWorldSpace = true;
            line.enabled = false;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            return line;
        }

        private class LineData
        {
            public GameObject GameObject = null!;
            public LineRenderer LineRenderer = null!;
            public GameObject Sphere = null!;
            public MeshRenderer? SphereRenderer;
            public LineRenderer? AxisX;
            public LineRenderer? AxisY;
            public LineRenderer? AxisZ;
        }

        /// <summary>
        /// Updates trajectory lines, spheres, and axis lines for a specific saber.
        /// </summary>
        public void UpdateTrajectories(SaberType saberType, Vector3 saberTipPosition, List<Vector3> targets)
        {
            var config = PluginConfig.Instance;
            if (!_isInitialized || !config.ShowTrajectoryLine)
            {
                HideAllLines(saberType);
                return;
            }

            var lines = saberType == SaberType.SaberA ? _leftLines : _rightLines;
            Color baseColor = saberType == SaberType.SaberA ? _leftLineColor : _rightLineColor;
            float axisAlpha = config.NoteGridAlpha;
            var axisColor = new Color(baseColor.r, baseColor.g, baseColor.b, axisAlpha);

            // Beat Saber awards N center points when cut distance <= 0.3*(15-N)/15.
            // We use (15.5-N) instead of (15-N) so the sphere has a visible minimum
            // radius even at target=15 (perfect center).
            int centerTarget = Mathf.Clamp(config.CenterAccuracyTarget, 1, 15);
            float sphereRadius = kMaxCenterDistance * (15.5f - centerTarget) / 15f;
            float sphereScale = sphereRadius * 2f;
            float axisHalfLen = sphereRadius + AxisLineExtension;

            for (int i = 0; i < lines.Count; i++)
            {
                var lineData = lines[i];
                if (lineData?.LineRenderer == null)
                    continue;

                if (i < targets.Count)
                {
                    var targetPos = targets[i];

                    float distance = Vector3.Distance(saberTipPosition, targetPos);
                    if (distance > 5f)
                    {
                        SetLineDataEnabled(lineData, false);
                        continue;
                    }

                    Color lineColor = baseColor;
                    if (i > 0)
                        lineColor.a *= 0.7f;

                    Color endColor = new Color(lineColor.r, lineColor.g, lineColor.b, 0.15f);

                    lineData.LineRenderer.startColor = lineColor;
                    lineData.LineRenderer.endColor = endColor;
                    lineData.LineRenderer.enabled = true;
                    lineData.LineRenderer.SetPosition(0, saberTipPosition);
                    lineData.LineRenderer.SetPosition(1, targetPos);

                    // Position and scale sphere at note center
                    if (lineData.Sphere != null)
                    {
                        lineData.Sphere.transform.position = targetPos;
                        lineData.Sphere.transform.localScale = Vector3.one * sphereScale;
                        if (lineData.SphereRenderer != null)
                            lineData.SphereRenderer.enabled = true;
                    }

                    // Update X/Y/Z axis lines through sphere center
                    UpdateAxisLine(lineData.AxisX, targetPos, Vector3.right, axisColor, axisHalfLen);
                    UpdateAxisLine(lineData.AxisY, targetPos, Vector3.up, axisColor, axisHalfLen);
                    UpdateAxisLine(lineData.AxisZ, targetPos, Vector3.forward, axisColor, axisHalfLen);
                }
                else
                {
                    SetLineDataEnabled(lineData, false);
                }
            }
        }

        private static void UpdateAxisLine(LineRenderer? axis, Vector3 center, Vector3 direction, Color color, float halfLength)
        {
            if (axis == null)
                return;

            axis.enabled = true;
            axis.startColor = color;
            axis.endColor = color;
            axis.SetPosition(0, center - direction * halfLength);
            axis.SetPosition(1, center + direction * halfLength);
        }

        private static void SetLineDataEnabled(LineData lineData, bool enabled)
        {
            if (lineData.LineRenderer != null)
                lineData.LineRenderer.enabled = enabled;
            if (lineData.SphereRenderer != null)
                lineData.SphereRenderer.enabled = enabled;
            if (lineData.AxisX != null)
                lineData.AxisX.enabled = enabled;
            if (lineData.AxisY != null)
                lineData.AxisY.enabled = enabled;
            if (lineData.AxisZ != null)
                lineData.AxisZ.enabled = enabled;
        }

        /// <summary>
        /// Hides all trajectory lines, spheres, and axis lines for a specific saber.
        /// </summary>
        public void HideAllLines(SaberType saberType)
        {
            var lines = saberType == SaberType.SaberA ? _leftLines : _rightLines;
            foreach (var lineData in lines)
            {
                if (lineData != null)
                    SetLineDataEnabled(lineData, false);
            }
        }

        /// <summary>
        /// Hides all trajectory lines, spheres, and axis lines.
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

            DisposeLines(_leftLines);
            DisposeLines(_rightLines);

            _isInitialized = false;
            Plugin.Log?.Info("TrajectoryLineService disposed");
        }

        private static void DisposeLines(List<LineData> lines)
        {
            foreach (var lineData in lines)
            {
                // Axis line GOs are children of Sphere, destroyed together
                if (lineData?.Sphere != null)
                {
                    if (lineData.SphereRenderer?.material != null)
                        UnityEngine.Object.Destroy(lineData.SphereRenderer.material);
                    UnityEngine.Object.Destroy(lineData.Sphere);
                }
                if (lineData?.GameObject != null)
                    UnityEngine.Object.Destroy(lineData.GameObject);
            }
            lines.Clear();
        }
    }
}
