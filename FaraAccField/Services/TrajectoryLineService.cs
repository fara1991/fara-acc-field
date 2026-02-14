using System;
using System.Collections.Generic;
using FaraAccField.Configuration;
using UnityEngine;

namespace FaraAccField.Services
{
    /// <summary>
    /// Manages trajectory line rendering and sphere indicators for notes.
    /// One trajectory line per saber (from saber tip to nearest note).
    /// One sphere with X/Y/Z axis lines per active note (dynamic pool).
    /// </summary>
    internal class TrajectoryLineService : IDisposable
    {
        private const float ArrowIndicatorScale = 0.40f;

        // Per-saber trajectory line (single line from saber to nearest note)
        private TrajectoryLine? _leftLine;
        private TrajectoryLine? _rightLine;

        // Per-saber sphere pools (one sphere per active note, grows dynamically)
        private readonly List<SphereData> _leftSpheres = new();
        private readonly List<SphereData> _rightSpheres = new();

        private Material? _lineMaterial;
        private Mesh? _triangleMesh;
        private bool _isInitialized;

        // Line colors (set from ColorManager at runtime)
        private Color _leftLineColor = new Color(1f, 0.3f, 0.3f, 0.6f);
        private Color _rightLineColor = new Color(0.3f, 0.3f, 1f, 0.6f);

        private class TrajectoryLine
        {
            public GameObject GameObject = null!;
            public LineRenderer LineRenderer = null!;
        }

        private class SphereData
        {
            public GameObject Sphere = null!;
            public MeshRenderer? SphereRenderer;
            public GameObject? AxisXObj;
            public GameObject? AxisYObj;
            public GameObject? AxisZObj;
            public LineRenderer? AxisX;
            public LineRenderer? AxisY;
            public LineRenderer? AxisZ;
            public GameObject? ArrowObj;
            public MeshRenderer? ArrowRenderer;
        }

        /// <summary>
        /// Initializes the trajectory line renderers.
        /// Spheres are created on demand as notes appear.
        /// </summary>
        public void Initialize(Color? leftColor = null, Color? rightColor = null)
        {
            if (_isInitialized)
                return;

            if (!PluginConfig.Instance.ShowTrajectoryLine
                && !PluginConfig.Instance.ShowCenterSphere
                && !PluginConfig.Instance.ShowAxisLine
                && !PluginConfig.Instance.ShowArrowIndicator)
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
                    shader = Shader.Find("Unlit/Color");
                if (shader == null)
                {
                    Plugin.Log?.Warn("Could not find suitable shader for trajectory lines");
                    return;
                }

                _lineMaterial = new Material(shader);
                _lineMaterial.SetInt("_ZWrite", 0);
                _lineMaterial.renderQueue = 3000;

                _leftLine = CreateTrajectoryLine("AccField_LeftTrajectory", _leftLineColor);
                _rightLine = CreateTrajectoryLine("AccField_RightTrajectory", _rightLineColor);

                _triangleMesh = VisualHelper.CreateTriangleMesh();

                _isInitialized = true;
                Plugin.Log?.Info("TrajectoryLineService initialized");
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"Failed to initialize TrajectoryLineService: {ex}");
            }
        }

        private TrajectoryLine CreateTrajectoryLine(string name, Color color)
        {
            var obj = new GameObject(name);
            var line = obj.AddComponent<LineRenderer>();

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

            return new TrajectoryLine { GameObject = obj, LineRenderer = line };
        }

        private SphereData CreateSphereData(string name, Color color)
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

            var axisXObj = CreateAxisLineObject($"{name}_AxisX", color);
            var axisYObj = CreateAxisLineObject($"{name}_AxisY", color);
            var axisZObj = CreateAxisLineObject($"{name}_AxisZ", color);

            GameObject? arrowObj = null;
            MeshRenderer? arrowRenderer = null;
            if (_triangleMesh != null && _lineMaterial != null)
            {
                arrowObj = new GameObject($"{name}_Arrow");
                arrowObj.layer = 2;

                var meshFilter = arrowObj.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = _triangleMesh;

                var arrowMat = new Material(_lineMaterial);
                arrowMat.color = new Color(color.r, color.g, color.b, 0.9f);
                arrowRenderer = arrowObj.AddComponent<MeshRenderer>();
                arrowRenderer.material = arrowMat;
                arrowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                arrowRenderer.receiveShadows = false;
                arrowRenderer.enabled = false;
            }

            return new SphereData
            {
                Sphere = sphere,
                SphereRenderer = renderer,
                AxisXObj = axisXObj,
                AxisYObj = axisYObj,
                AxisZObj = axisZObj,
                AxisX = axisXObj?.GetComponent<LineRenderer>(),
                AxisY = axisYObj?.GetComponent<LineRenderer>(),
                AxisZ = axisZObj?.GetComponent<LineRenderer>(),
                ArrowObj = arrowObj,
                ArrowRenderer = arrowRenderer
            };
        }

        private GameObject? CreateAxisLineObject(string name, Color color)
        {
            var obj = new GameObject(name);
            obj.layer = 2;

            var line = obj.AddComponent<LineRenderer>();
            if (line == null || _lineMaterial == null)
            {
                UnityEngine.Object.Destroy(obj);
                return null;
            }

            line.material = _lineMaterial;
            float width = PluginConfig.Instance.AxisLineWidth;
            line.startWidth = width;
            line.endWidth = width;
            line.positionCount = 2;
            var axisColor = new Color(color.r, color.g, color.b, 0.5f);
            line.startColor = axisColor;
            line.endColor = axisColor;
            line.useWorldSpace = true;
            line.enabled = false;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            return obj;
        }

        private void EnsureSpherePoolSize(List<SphereData> pool, int needed, string prefix, Color color)
        {
            while (pool.Count < needed)
            {
                int idx = pool.Count;
                pool.Add(CreateSphereData($"AccField_{prefix}Sphere_{idx}", color));
            }
        }

        /// <summary>
        /// Updates trajectory line and sphere indicators for a specific saber.
        /// Line is drawn from saber tip to the nearest note (first target).
        /// Spheres with axis lines are drawn at all target positions.
        /// </summary>
        public void UpdateTrajectories(SaberType saberType, Vector3 saberTipPosition, List<Vector3> allTargets, List<Vector3>? arrowDirections = null)
        {
            var config = PluginConfig.Instance;
            bool anyVisual = config.ShowTrajectoryLine || config.ShowCenterSphere
                || config.ShowAxisLine || config.ShowArrowIndicator;
            if (!_isInitialized || !anyVisual)
            {
                HideAllLines(saberType);
                return;
            }

            var line = saberType == SaberType.SaberA ? _leftLine : _rightLine;
            var spheres = saberType == SaberType.SaberA ? _leftSpheres : _rightSpheres;
            Color baseColor = saberType == SaberType.SaberA ? _leftLineColor : _rightLineColor;

            var axisColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.5f);

            // Beat Saber awards N center points when cut distance <= 0.3*(15-N)/15.
            // We use (15.5-N) instead of (15-N) so the sphere has a visible minimum
            // radius even at target=15 (perfect center).
            float sphereRadius = VisualHelper.CalculateSphereRadius(config.CenterAccuracyTarget);
            float sphereScale = sphereRadius * 2f;
            float axisLineLen = config.AxisLineLength;
            float axisLineWidth = config.AxisLineWidth;
            float axisHalfLen = sphereRadius + axisLineLen;

            if (allTargets.Count == 0)
            {
                HideAllLines(saberType);
                return;
            }

            // Trajectory line to nearest note only (first target)
            if (line != null)
            {
                if (config.ShowTrajectoryLine)
                {
                    line.LineRenderer.startColor = baseColor;
                    line.LineRenderer.endColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.15f);
                    line.LineRenderer.enabled = true;
                    line.LineRenderer.SetPosition(0, saberTipPosition);
                    line.LineRenderer.SetPosition(1, allTargets[0]);
                }
                else
                {
                    line.LineRenderer.enabled = false;
                }
            }

            bool showSphere = config.ShowCenterSphere;
            bool showAxis = config.ShowAxisLine;

            // Ensure enough spheres in pool
            EnsureSpherePoolSize(spheres, allTargets.Count,
                saberType == SaberType.SaberA ? "Left" : "Right", baseColor);

            // Position spheres for all targets, hide excess
            for (int i = 0; i < spheres.Count; i++)
            {
                var sd = spheres[i];
                if (i < allTargets.Count)
                {
                    var pos = allTargets[i];
                    sd.Sphere.transform.position = pos;
                    sd.Sphere.transform.localScale = Vector3.one * sphereScale;
                    if (sd.SphereRenderer != null)
                        sd.SphereRenderer.enabled = showSphere;

                    if (showAxis)
                    {
                        VisualHelper.UpdateAxisLine(sd.AxisX, pos, Vector3.right, axisColor, axisHalfLen, axisLineWidth);
                        VisualHelper.UpdateAxisLine(sd.AxisY, pos, Vector3.up, axisColor, axisHalfLen, axisLineWidth);
                        VisualHelper.UpdateAxisLine(sd.AxisZ, pos, Vector3.forward, axisColor, axisHalfLen, axisLineWidth);
                    }
                    else
                    {
                        if (sd.AxisX != null) sd.AxisX.enabled = false;
                        if (sd.AxisY != null) sd.AxisY.enabled = false;
                        if (sd.AxisZ != null) sd.AxisZ.enabled = false;
                    }

                    // Update arrow direction indicator
                    if (sd.ArrowObj != null)
                    {
                        Vector3 arrowDir = (config.ShowArrowIndicator && arrowDirections != null && i < arrowDirections.Count)
                            ? arrowDirections[i] : Vector3.zero;
                        if (arrowDir != Vector3.zero)
                        {
                            float arrowOffset = sphereRadius + 0.333f * ArrowIndicatorScale;
                            sd.ArrowObj.transform.position = pos + arrowDir * arrowOffset;
                            sd.ArrowObj.transform.localScale = Vector3.one * ArrowIndicatorScale;
                            sd.ArrowObj.transform.rotation = Quaternion.FromToRotation(Vector3.up, arrowDir);
                            if (sd.ArrowRenderer != null)
                                sd.ArrowRenderer.enabled = true;
                        }
                        else
                        {
                            if (sd.ArrowRenderer != null)
                                sd.ArrowRenderer.enabled = false;
                        }
                    }
                }
                else
                {
                    SetSphereEnabled(sd, false);
                }
            }
        }

        private static void SetSphereEnabled(SphereData sd, bool enabled)
        {
            if (sd.SphereRenderer != null)
                sd.SphereRenderer.enabled = enabled;
            if (sd.AxisX != null)
                sd.AxisX.enabled = enabled;
            if (sd.AxisY != null)
                sd.AxisY.enabled = enabled;
            if (sd.AxisZ != null)
                sd.AxisZ.enabled = enabled;
            if (sd.ArrowRenderer != null)
                sd.ArrowRenderer.enabled = enabled;
        }

        private void HideAll(SaberType saberType)
        {
            var line = saberType == SaberType.SaberA ? _leftLine : _rightLine;
            if (line != null)
                line.LineRenderer.enabled = false;

            var spheres = saberType == SaberType.SaberA ? _leftSpheres : _rightSpheres;
            foreach (var sd in spheres)
                SetSphereEnabled(sd, false);
        }

        /// <summary>
        /// Hides all trajectory lines, spheres, and axis lines for a specific saber.
        /// </summary>
        public void HideAllLines(SaberType saberType)
        {
            HideAll(saberType);
        }

        /// <summary>
        /// Hides all trajectory lines, spheres, and axis lines.
        /// </summary>
        public void HideAllLines()
        {
            HideAll(SaberType.SaberA);
            HideAll(SaberType.SaberB);
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

            if (_triangleMesh != null)
            {
                UnityEngine.Object.Destroy(_triangleMesh);
                _triangleMesh = null;
            }

            DestroyLine(_leftLine);
            DestroyLine(_rightLine);
            _leftLine = null;
            _rightLine = null;

            DisposeSpheres(_leftSpheres);
            DisposeSpheres(_rightSpheres);

            _isInitialized = false;
            Plugin.Log?.Info("TrajectoryLineService disposed");
        }

        private static void DestroyLine(TrajectoryLine? line)
        {
            if (line?.GameObject != null)
                UnityEngine.Object.Destroy(line.GameObject);
        }

        private static void DisposeSpheres(List<SphereData> spheres)
        {
            foreach (var sd in spheres)
            {
                if (sd == null)
                    continue;

                if (sd.SphereRenderer?.material != null)
                    UnityEngine.Object.Destroy(sd.SphereRenderer.material);
                if (sd.Sphere != null)
                    UnityEngine.Object.Destroy(sd.Sphere);
                if (sd.AxisXObj != null)
                    UnityEngine.Object.Destroy(sd.AxisXObj);
                if (sd.AxisYObj != null)
                    UnityEngine.Object.Destroy(sd.AxisYObj);
                if (sd.AxisZObj != null)
                    UnityEngine.Object.Destroy(sd.AxisZObj);
                if (sd.ArrowObj != null)
                {
                    if (sd.ArrowRenderer?.material != null)
                        UnityEngine.Object.Destroy(sd.ArrowRenderer.material);
                    UnityEngine.Object.Destroy(sd.ArrowObj);
                }
            }
            spheres.Clear();
        }
    }
}
