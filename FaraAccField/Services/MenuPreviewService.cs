using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FaraAccField.Configuration;
using FaraAccField.Models;
using UnityEngine;
using UnityEngine.XR;

namespace FaraAccField.Services
{
    internal class MenuPreviewService : IDisposable
    {
        private const int Columns = 4;
        private const int Rows = 3;
        private const int TotalCubes = Columns * Rows;

        private static readonly float[] ColumnPositions = { -0.9f, -0.3f, 0.3f, 0.9f };
        private static readonly float[] RowPositions = { 0.85f, 1.40f, 1.95f };

        private const float CubeScale = 0.4f;
        private const float BasePreviewDistance = 1.5f;

        private const float FakeNoteInterval = 0.8f;
        private const float FakeNoteSpeed = 2.0f;
        private const float FakeNoteSpawnZBehind = 4.0f;
        private const float FakeNoteDestroyZBefore = 0.5f;
        private const float HighlightZThreshold = 1.0f;
        private const float GlowCubeSize = 0.35f;
        private const float GlowScale = 1.01f;
        private const float GlowAlpha = 0.5f;

        private const float HighlightEmissionIntensity = 1.5f;
        private const float HighlightPulseSpeed = 4f;

        private const float SaberTipOffsetX = 0.3f;
        private const float SaberTipY = 1.0f;
        private const float SaberLength = 0.8f;
        private const float MinSwingAngularSpeed = 200f;

        // Arrow indicator size is now controlled by config:
        // PluginConfig.ArrowIndicatorWidth / ArrowIndicatorHeight

        private static readonly Vector3[] ArrowDirections =
        {
            Vector3.up,
            Vector3.down,
            Vector3.left,
            Vector3.right,
            new Vector3(-1, 1, 0).normalized,
            new Vector3(1, 1, 0).normalized,
            new Vector3(-1, -1, 0).normalized,
            new Vector3(1, -1, 0).normalized,
        };

        private static readonly Color DefaultCubeColor = new Color(192f / 255f, 64f / 255f, 192f / 255f);
        private static readonly Color LeftNoteColor = new Color(0.8f, 0.2f, 0.2f);
        private static readonly Color RightNoteColor = new Color(0.2f, 0.4f, 0.9f);

        private GameObject? _container;
        private GameObject[]? _cubes;
        private Material[]? _cubeMaterials;
        private bool[]? _cubeHighlighted;
        private Color[]? _highlightBaseColors;

        private Material? _noteMaterial;
        private Material? _lineMaterial;
        private readonly List<FakeNote> _fakeNotes = new();
        private float _spawnTimer;
        private float _cameraZ;
        private float _gridZ;

        private GameObject? _leftTrajectoryObj;
        private LineRenderer? _leftTrajectoryLine;
        private GameObject? _rightTrajectoryObj;
        private LineRenderer? _rightTrajectoryLine;

        private bool _isShowing;
        private Mesh? _triangleMesh;
        private bool _rhythmMarkerAvailable;
        private Material? _glowMaterialLeft;
        private Material? _glowMaterialRight;
        private float _noteScale = 1f;
        private Transform? _xrOrigin;
        private readonly SaberState _menuLeftSaber = new();
        private readonly SaberState _menuRightSaber = new();

        private class FakeNote
        {
            public GameObject GameObject = null!;
            public Material Material = null!;
            public Material? SphereMaterial;
            public GameObject? CenterSphere;
            public LineRenderer? AxisX;
            public LineRenderer? AxisY;
            public LineRenderer? AxisZ;
            public GameObject? ArrowIndicator;
            public Vector3 ArrowDirection;
            public int LineIndex;
            public int LineLayer;
            public bool IsLeft;
            public bool HasHighlighted;
            public bool HasGlow;
            public GameObject? GlowCube;
        }

        private class PreviewUpdater : MonoBehaviour
        {
            public MenuPreviewService? Service;

            private void Update()
            {
                Service?.Tick();
            }
        }

        public void Show()
        {
            if (_isShowing)
                return;

            try
            {
                _container = new GameObject("AccField_MenuPreviewContainer");
                _container.layer = 2;

                var updater = _container.AddComponent<PreviewUpdater>();
                updater.Service = this;

                var cam = Camera.main;
                _cameraZ = cam != null ? cam.transform.position.z : 0f;
                _xrOrigin = cam != null ? cam.transform.parent : null;
                CheckRhythmMarkerAvailability();
                float zOffset = GetResolvedZOffset();
                _gridZ = _cameraZ + BasePreviewDistance + zOffset;

                Shader? shader = Shader.Find("Sprites/Default")
                    ?? Shader.Find("UI/Default")
                    ?? Shader.Find("Unlit/Color");

                if (shader == null)
                {
                    Plugin.Log?.Warn("MenuPreviewService: Could not find suitable shader");
                    return;
                }

                float alpha = PluginConfig.Instance.NotesGridAlpha;
                bool showGrid = PluginConfig.Instance.ShowNotesGrid;

                _cubes = new GameObject[TotalCubes];
                _cubeMaterials = new Material[TotalCubes];
                _cubeHighlighted = new bool[TotalCubes];
                _highlightBaseColors = new Color[TotalCubes];

                int index = 0;
                for (int row = 0; row < Rows; row++)
                {
                    for (int col = 0; col < Columns; col++)
                    {
                        var material = CreateMaterial(shader, DefaultCubeColor, alpha);
                        _cubeMaterials[index] = material;

                        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cube.name = $"AccField_MenuGrid_{row}_{col}";
                        cube.transform.position = new Vector3(ColumnPositions[col], RowPositions[row], _gridZ);
                        cube.transform.localScale = Vector3.one * CubeScale;

                        var collider = cube.GetComponent<Collider>();
                        if (collider != null)
                            UnityEngine.Object.DestroyImmediate(collider);

                        cube.layer = 2;
                        cube.transform.SetParent(_container.transform, true);

                        var renderer = cube.GetComponent<MeshRenderer>();
                        if (renderer != null)
                        {
                            renderer.material = material;
                            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                            renderer.receiveShadows = false;
                        }

                        cube.SetActive(showGrid);
                        _cubes[index] = cube;
                        index++;
                    }
                }

                _noteMaterial = CreateMaterial(shader, Color.white, 0.9f);
                _lineMaterial = new Material(shader);
                _lineMaterial.SetInt("_ZWrite", 0);
                _lineMaterial.renderQueue = 3000;

                CreateTrajectoryLine("AccField_MenuLeftTrajectory", LeftNoteColor,
                    out _leftTrajectoryObj, out _leftTrajectoryLine);
                CreateTrajectoryLine("AccField_MenuRightTrajectory", RightNoteColor,
                    out _rightTrajectoryObj, out _rightTrajectoryLine);

                _triangleMesh = VisualHelper.CreateTriangleMesh();

                // Glow materials for pre-swing preview
                Shader? glowShader = Shader.Find("Particles/Additive")
                    ?? Shader.Find("Sprites/Default")
                    ?? Shader.Find("UI/Default");
                if (glowShader != null)
                {
                    _glowMaterialLeft = CreateGlowMaterial(glowShader, LeftNoteColor);
                    _glowMaterialRight = CreateGlowMaterial(glowShader, RightNoteColor);
                }

                // Menu preview always reflects configured NoteSize (not per-level AutoDisable)
                var (noteSize, _) = VisualHelper.GetCustomNotesSettings();
                _noteScale = noteSize;

                _spawnTimer = 0f;
                _isShowing = true;

                if (!PluginConfig.Instance.Enabled)
                    _container.SetActive(false);

                Plugin.Log?.Info("MenuPreviewService: Show");
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"MenuPreviewService Show failed: {ex}");
            }
        }

        private void CreateTrajectoryLine(string name, Color color,
            out GameObject? obj, out LineRenderer? line)
        {
            obj = new GameObject(name);
            obj.layer = 2;
            obj.transform.SetParent(_container!.transform, true);

            line = obj.AddComponent<LineRenderer>();
            line.material = _lineMaterial;
            line.startWidth = 0.015f;
            line.endWidth = 0.01f;
            line.positionCount = 2;
            line.startColor = new Color(color.r, color.g, color.b, 0.6f);
            line.endColor = new Color(color.r, color.g, color.b, 0.2f);
            line.useWorldSpace = true;
            line.enabled = false;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        private LineRenderer CreateAxisLine(string name, Color color, Transform parent)
        {
            var obj = new GameObject(name);
            obj.layer = 2;
            obj.transform.SetParent(parent, false);

            var line = obj.AddComponent<LineRenderer>();
            line.material = _lineMaterial;
            line.positionCount = 2;
            var axisColor = new Color(color.r, color.g, color.b, 0.5f);
            line.startColor = axisColor;
            line.endColor = axisColor;
            line.useWorldSpace = true;
            line.enabled = false;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            return line;
        }

        public void Hide()
        {
            if (!_isShowing)
                return;

            foreach (var note in _fakeNotes)
            {
                if (note.SphereMaterial != null)
                    UnityEngine.Object.Destroy(note.SphereMaterial);
                if (note.Material != null)
                    UnityEngine.Object.Destroy(note.Material);
                if (note.GameObject != null)
                    UnityEngine.Object.Destroy(note.GameObject);
            }
            _fakeNotes.Clear();

            if (_cubeMaterials != null)
            {
                foreach (var mat in _cubeMaterials)
                {
                    if (mat != null)
                        UnityEngine.Object.Destroy(mat);
                }
                _cubeMaterials = null;
            }

            if (_noteMaterial != null)
            {
                UnityEngine.Object.Destroy(_noteMaterial);
                _noteMaterial = null;
            }

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

            if (_glowMaterialLeft != null)
            {
                UnityEngine.Object.Destroy(_glowMaterialLeft);
                _glowMaterialLeft = null;
            }
            if (_glowMaterialRight != null)
            {
                UnityEngine.Object.Destroy(_glowMaterialRight);
                _glowMaterialRight = null;
            }

            _leftTrajectoryObj = null;
            _leftTrajectoryLine = null;
            _rightTrajectoryObj = null;
            _rightTrajectoryLine = null;

            _cubes = null;
            _cubeHighlighted = null;
            _highlightBaseColors = null;

            if (_container != null)
            {
                UnityEngine.Object.Destroy(_container);
                _container = null;
            }

            _isShowing = false;

            Plugin.Log?.Info("MenuPreviewService: Hide");
        }

        public void UpdateEnabled(bool enabled)
        {
            if (_container != null)
                _container.SetActive(enabled);
        }

        public void UpdateZOffset(float zOffset)
        {
            if (!_isShowing || _cubes == null)
                return;

            float oldGridZ = _gridZ;
            _gridZ = _cameraZ + BasePreviewDistance + zOffset;
            float delta = _gridZ - oldGridZ;

            foreach (var cube in _cubes)
            {
                if (cube != null)
                {
                    var pos = cube.transform.position;
                    cube.transform.position = new Vector3(pos.x, pos.y, _gridZ);
                }
            }

            foreach (var note in _fakeNotes)
            {
                if (note.GameObject != null)
                {
                    var pos = note.GameObject.transform.position;
                    note.GameObject.transform.position = new Vector3(pos.x, pos.y, pos.z + delta);
                }
            }
        }

        public void UpdateAlpha(float alpha)
        {
            if (_cubeMaterials == null)
                return;

            for (int i = 0; i < _cubeMaterials.Length; i++)
            {
                if (_cubeMaterials[i] != null)
                {
                    var color = _cubeMaterials[i].color;
                    _cubeMaterials[i].color = new Color(color.r, color.g, color.b, alpha);
                }
            }
        }

        public void UpdateShowNotesGrid(bool show)
        {
            if (_cubes == null)
                return;

            foreach (var cube in _cubes)
            {
                if (cube != null)
                    cube.SetActive(show);
            }
        }

        public void UpdateCenterAccuracyTarget(int centerTarget)
        {
            float sphereRadius = VisualHelper.CalculateSphereRadius(centerTarget);
            float sphereScale = sphereRadius * 2f;

            foreach (var note in _fakeNotes)
            {
                if (note.CenterSphere != null)
                    note.CenterSphere.transform.localScale = Vector3.one * sphereScale;
            }
        }

        private void Tick()
        {
            if (!_isShowing || _cubes == null || _cubeMaterials == null
                || _cubeHighlighted == null || _highlightBaseColors == null)
                return;

            var config = PluginConfig.Instance;
            float sphereRadius = VisualHelper.CalculateSphereRadius(config.CenterAccuracyTarget);
            float axisHalfLen = sphereRadius + config.AxisLineLength;

            float dt = Time.deltaTime;

            _spawnTimer += dt;
            if (_spawnTimer >= FakeNoteInterval)
            {
                _spawnTimer -= FakeNoteInterval;
                SpawnFakeNote();
            }

            // Update saber states from XR controllers (same as in-game SaberState tracking)
            var (leftTip, leftHandle) = GetControllerPositions(XRNode.LeftHand,
                new Vector3(-SaberTipOffsetX, SaberTipY + SaberLength, _cameraZ),
                new Vector3(-SaberTipOffsetX, SaberTipY, _cameraZ));
            var (rightTip, rightHandle) = GetControllerPositions(XRNode.RightHand,
                new Vector3(SaberTipOffsetX, SaberTipY + SaberLength, _cameraZ),
                new Vector3(SaberTipOffsetX, SaberTipY, _cameraZ));
            _menuLeftSaber.UpdateState(leftTip, leftHandle, Time.time);
            _menuRightSaber.UpdateState(rightTip, rightHandle, Time.time);

            TickNotes(dt, config, sphereRadius, axisHalfLen,
                out var nearestLeft, out var nearestRight);

            UpdateTrajectoryLine(_leftTrajectoryLine, config.ShowTrajectoryLine, nearestLeft, leftTip);
            UpdateTrajectoryLine(_rightTrajectoryLine, config.ShowTrajectoryLine, nearestRight, rightTip);

            PulseHighlightedCubes();
        }

        private void TickNotes(float dt, PluginConfig config, float sphereRadius, float axisHalfLen,
            out FakeNote? nearestLeft, out FakeNote? nearestRight)
        {
            nearestLeft = null;
            nearestRight = null;
            float nearestLeftZ = float.MaxValue;
            float nearestRightZ = float.MaxValue;
            float gridZ = _gridZ;

            for (int i = _fakeNotes.Count - 1; i >= 0; i--)
            {
                var note = _fakeNotes[i];
                if (note.GameObject == null)
                {
                    _fakeNotes.RemoveAt(i);
                    continue;
                }

                var pos = note.GameObject.transform.position;
                pos.z -= FakeNoteSpeed * dt;
                note.GameObject.transform.position = pos;

                if (!note.HasHighlighted && Mathf.Abs(pos.z - gridZ) < HighlightZThreshold)
                {
                    note.HasHighlighted = true;
                    HighlightCube(note.LineIndex, note.LineLayer, note.IsLeft);
                }

                if (pos.z < gridZ - FakeNoteDestroyZBefore)
                {
                    ResetCube(note.LineIndex, note.LineLayer);
                    if (note.SphereMaterial != null)
                        UnityEngine.Object.Destroy(note.SphereMaterial);
                    UnityEngine.Object.Destroy(note.Material);
                    UnityEngine.Object.Destroy(note.GameObject);
                    _fakeNotes.RemoveAt(i);
                    continue;
                }

                UpdateNoteVisuals(note, pos, config, sphereRadius, axisHalfLen);

                if (note.IsLeft && pos.z < nearestLeftZ)
                {
                    nearestLeftZ = pos.z;
                    nearestLeft = note;
                }
                else if (!note.IsLeft && pos.z < nearestRightZ)
                {
                    nearestRightZ = pos.z;
                    nearestRight = note;
                }
            }

            string glowCondition = config.GlowCondition;
            if (glowCondition == "Next")
            {
                if (nearestLeft != null && !nearestLeft.HasGlow)
                    ApplyPreviewGlow(nearestLeft);
                if (nearestRight != null && !nearestRight.HasGlow)
                    ApplyPreviewGlow(nearestRight);
            }
            else if (glowCondition == "PreSwing70")
            {
                ApplyGlowIfPreSwing(nearestLeft, _menuLeftSaber);
                ApplyGlowIfPreSwing(nearestRight, _menuRightSaber);
            }
        }

        private void ApplyGlowIfPreSwing(FakeNote? note, SaberState saberState)
        {
            if (note == null || note.HasGlow || note.GameObject == null)
                return;

            if (saberState.BladeAngularSpeed < MinSwingAngularSpeed)
                return;

            float angle = SwingAngleCalculator.CalculatePreSwingAngle(
                saberState.BladeDirection, note.ArrowDirection);
            if (SwingAngleCalculator.MeetsPreSwingThreshold(angle))
                ApplyPreviewGlow(note);
        }

        private static void UpdateNoteVisuals(FakeNote note, Vector3 pos,
            PluginConfig config, float sphereRadius, float axisHalfLen)
        {
            if (note.CenterSphere != null)
                note.CenterSphere.SetActive(config.ShowCenterSphere);

            if (config.ShowAxisLine)
            {
                var axisColor = note.IsLeft
                    ? new Color(LeftNoteColor.r, LeftNoteColor.g, LeftNoteColor.b, 0.5f)
                    : new Color(RightNoteColor.r, RightNoteColor.g, RightNoteColor.b, 0.5f);

                VisualHelper.UpdateAxisLine(note.AxisX, pos, Vector3.right, axisColor, axisHalfLen, config.AxisLineWidth);
                VisualHelper.UpdateAxisLine(note.AxisY, pos, Vector3.up, axisColor, axisHalfLen, config.AxisLineWidth);
                VisualHelper.UpdateAxisLine(note.AxisZ, pos, Vector3.forward, axisColor, axisHalfLen, config.AxisLineWidth);
            }
            else
            {
                SetAxisEnabled(note, false);
            }

            if (note.ArrowIndicator != null)
            {
                bool showArrow = config.ShowArrowIndicator;
                note.ArrowIndicator.SetActive(showArrow);
                if (showArrow)
                {
                    float arrowWidth = config.ArrowIndicatorWidth;
                    float arrowHeight = config.ArrowIndicatorHeight;
                    float arrowOffset = sphereRadius + 0.333f * arrowHeight;
                    note.ArrowIndicator.transform.localPosition = note.ArrowDirection * arrowOffset;
                    note.ArrowIndicator.transform.localScale = new Vector3(arrowWidth, arrowHeight, arrowWidth);
                }
            }
        }

        private void PulseHighlightedCubes()
        {
            bool anyHighlighted = false;
            for (int i = 0; i < TotalCubes; i++)
            {
                if (_cubeHighlighted![i]) { anyHighlighted = true; break; }
            }

            if (!anyHighlighted)
                return;

            float pulseT = (Mathf.Sin(Time.time * HighlightPulseSpeed) + 1f) * 0.5f;
            float pulseScale = Mathf.Lerp(1.0f, 1.05f, pulseT) * CubeScale;
            float pulseBrightness = Mathf.Lerp(1.0f, HighlightEmissionIntensity, pulseT);

            for (int i = 0; i < TotalCubes; i++)
            {
                if (!_cubeHighlighted![i] || _cubes![i] == null)
                    continue;

                _cubes[i].transform.localScale = Vector3.one * pulseScale;

                if (_cubeMaterials![i] != null)
                {
                    var baseColor = _highlightBaseColors![i];
                    _cubeMaterials[i].color = new Color(
                        Mathf.Clamp01(baseColor.r * pulseBrightness),
                        Mathf.Clamp01(baseColor.g * pulseBrightness),
                        Mathf.Clamp01(baseColor.b * pulseBrightness),
                        _cubeMaterials[i].color.a);
                }
            }
        }

        private readonly List<InputDevice> _xrDeviceBuffer = new(2);

        private (Vector3 tip, Vector3 handle) GetControllerPositions(XRNode node,
            Vector3 fallbackTip, Vector3 fallbackHandle)
        {
            try
            {
                _xrDeviceBuffer.Clear();
                InputDevices.GetDevicesAtXRNode(node, _xrDeviceBuffer);

                foreach (var device in _xrDeviceBuffer)
                {
                    if (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 localPos)
                        && device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion localRot)
                        && localPos.sqrMagnitude > 0.001f)
                    {
                        Vector3 handleLocal = localPos;
                        Vector3 tipLocal = localPos + localRot * (Vector3.forward * SaberLength);

                        if (_xrOrigin != null)
                            return (_xrOrigin.TransformPoint(tipLocal), _xrOrigin.TransformPoint(handleLocal));

                        return (tipLocal, handleLocal);
                    }
                }

                return (fallbackTip, fallbackHandle);
            }
            catch
            {
                return (fallbackTip, fallbackHandle);
            }
        }

        private static void UpdateTrajectoryLine(LineRenderer? line, bool show, FakeNote? nearest, Vector3 saberTip)
        {
            if (line == null)
                return;

            if (!show || nearest == null)
            {
                line.enabled = false;
                return;
            }

            line.enabled = true;
            line.SetPosition(0, saberTip);
            line.SetPosition(1, nearest.GameObject.transform.position);
        }

        private static void SetAxisEnabled(FakeNote note, bool enabled)
        {
            if (note.AxisX != null) note.AxisX.enabled = enabled;
            if (note.AxisY != null) note.AxisY.enabled = enabled;
            if (note.AxisZ != null) note.AxisZ.enabled = enabled;
        }

        private void SpawnFakeNote()
        {
            if (_container == null || _noteMaterial == null)
                return;

            int col = UnityEngine.Random.Range(0, Columns);
            int row = UnityEngine.Random.Range(0, Rows);
            bool isLeft = UnityEngine.Random.value > 0.5f;

            var noteColor = isLeft ? LeftNoteColor : RightNoteColor;
            float spawnZ = _gridZ + FakeNoteSpawnZBehind;

            var anchor = new GameObject($"AccField_FakeNote_{col}_{row}");
            anchor.transform.position = new Vector3(ColumnPositions[col], RowPositions[row], spawnZ);
            anchor.layer = 2;
            anchor.transform.SetParent(_container.transform, true);

            var material = new Material(_noteMaterial);
            material.color = new Color(noteColor.r, noteColor.g, noteColor.b, 0.9f);

            float sphereRadius = VisualHelper.CalculateSphereRadius(PluginConfig.Instance.CenterAccuracyTarget);

            var (centerSphere, sphereMat) = CreateNoteCenterSphere(noteColor, sphereRadius * 2f, anchor.transform);

            LineRenderer? axisX = null, axisY = null, axisZ = null;
            if (_lineMaterial != null)
            {
                axisX = CreateAxisLine($"AccField_FakeNote_AxisX_{col}_{row}", noteColor, anchor.transform);
                axisY = CreateAxisLine($"AccField_FakeNote_AxisY_{col}_{row}", noteColor, anchor.transform);
                axisZ = CreateAxisLine($"AccField_FakeNote_AxisZ_{col}_{row}", noteColor, anchor.transform);
            }

            var arrowDir = ArrowDirections[UnityEngine.Random.Range(0, ArrowDirections.Length)];
            var arrowIndicator = CreateNoteArrowIndicator(material, arrowDir, sphereRadius, anchor.transform);

            _fakeNotes.Add(new FakeNote
            {
                GameObject = anchor,
                Material = material,
                SphereMaterial = sphereMat,
                CenterSphere = centerSphere,
                AxisX = axisX,
                AxisY = axisY,
                AxisZ = axisZ,
                ArrowIndicator = arrowIndicator,
                ArrowDirection = arrowDir,
                LineIndex = col,
                LineLayer = row,
                IsLeft = isLeft,
                HasHighlighted = false
            });
        }

        private (GameObject sphere, Material? sphereMaterial) CreateNoteCenterSphere(
            Color noteColor, float sphereScale, Transform parent)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "AccField_FakeNote_Center";
            sphere.transform.SetParent(parent, false);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localScale = Vector3.one * sphereScale;

            var collider = sphere.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);

            sphere.layer = 2;

            Material? sphereMat = null;
            var renderer = sphere.GetComponent<MeshRenderer>();
            if (renderer != null && _noteMaterial != null)
            {
                sphereMat = new Material(_noteMaterial);
                sphereMat.color = new Color(noteColor.r, noteColor.g, noteColor.b, 0.7f);
                renderer.material = sphereMat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return (sphere, sphereMat);
        }

        private GameObject? CreateNoteArrowIndicator(Material material, Vector3 arrowDir,
            float sphereRadius, Transform parent)
        {
            if (_triangleMesh == null)
                return null;

            var arrow = new GameObject("AccField_FakeNote_Arrow");
            arrow.layer = 2;
            arrow.transform.SetParent(parent, false);

            var meshFilter = arrow.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = _triangleMesh;

            var renderer = arrow.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            float arrowWidth = PluginConfig.Instance.ArrowIndicatorWidth;
            float arrowHeight = PluginConfig.Instance.ArrowIndicatorHeight;
            float arrowOffset = sphereRadius + 0.333f * arrowHeight;
            arrow.transform.localPosition = arrowDir * arrowOffset;
            arrow.transform.localScale = new Vector3(arrowWidth, arrowHeight, arrowWidth);

            if (arrowDir != Vector3.zero)
                arrow.transform.localRotation = Quaternion.FromToRotation(Vector3.up, arrowDir);

            return arrow;
        }

        private void HighlightCube(int lineIndex, int lineLayer)
        {
            HighlightCube(lineIndex, lineLayer, true);
        }

        private void HighlightCube(int lineIndex, int lineLayer, bool isLeft)
        {
            if (_cubeMaterials == null || _cubeHighlighted == null || _highlightBaseColors == null)
                return;
            if (lineIndex < 0 || lineIndex >= Columns || lineLayer < 0 || lineLayer >= Rows)
                return;

            int index = lineLayer * Columns + lineIndex;
            _cubeHighlighted[index] = true;
            var color = isLeft ? LeftNoteColor : RightNoteColor;
            _highlightBaseColors[index] = color;
            float alpha = PluginConfig.Instance.NotesGridAlpha;
            _cubeMaterials[index].color = new Color(
                Mathf.Clamp01(color.r * HighlightEmissionIntensity),
                Mathf.Clamp01(color.g * HighlightEmissionIntensity),
                Mathf.Clamp01(color.b * HighlightEmissionIntensity),
                alpha);
        }

        private void ResetCube(int lineIndex, int lineLayer)
        {
            if (_cubeMaterials == null || _cubeHighlighted == null || _cubes == null)
                return;
            if (lineIndex < 0 || lineIndex >= Columns || lineLayer < 0 || lineLayer >= Rows)
                return;

            int index = lineLayer * Columns + lineIndex;
            _cubeHighlighted[index] = false;
            float alpha = PluginConfig.Instance.NotesGridAlpha;
            _cubeMaterials[index].color = new Color(DefaultCubeColor.r, DefaultCubeColor.g, DefaultCubeColor.b, alpha);

            if (_cubes[index] != null)
                _cubes[index].transform.localScale = Vector3.one * CubeScale;
        }

        private static Material CreateGlowMaterial(Shader shader, Color color)
        {
            var material = new Material(shader);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = 3100;
            material.color = new Color(color.r, color.g, color.b, GlowAlpha);
            return material;
        }

        private void ApplyPreviewGlow(FakeNote note)
        {
            if (note.HasGlow || note.GameObject == null)
                return;

            var glowMat = note.IsLeft ? _glowMaterialLeft : _glowMaterialRight;
            if (glowMat == null)
                return;

            note.HasGlow = true;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "AccField_PreviewGlow";
            cube.layer = 2;

            var collider = cube.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);

            cube.transform.SetParent(note.GameObject.transform, false);
            cube.transform.localPosition = Vector3.zero;
            cube.transform.localScale = Vector3.one * (GlowCubeSize * _noteScale * GlowScale);

            var renderer = cube.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = glowMat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            note.GlowCube = cube;
        }

        private static Material CreateMaterial(Shader shader, Color color, float alpha)
        {
            var material = new Material(shader);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = 3000;
            material.color = new Color(color.r, color.g, color.b, alpha);
            return material;
        }

        private void CheckRhythmMarkerAvailability()
        {
            try
            {
                _rhythmMarkerAvailable = VisualHelper.IsRhythmMarkerLoaded();
            }
            catch
            {
                _rhythmMarkerAvailable = false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float ReadRhythmMarkerZOffset()
        {
            return FaraRhythmMarker.Configuration.PluginConfig.Instance.MarkerZOffset;
        }

        private float GetResolvedZOffset()
        {
            var config = PluginConfig.Instance;
            if (config.LinkRhythmMarkerZOffset && _rhythmMarkerAvailable)
            {
                try
                {
                    return ReadRhythmMarkerZOffset();
                }
                catch
                {
                    // Fall through to manual offset
                }
            }
            return config.NotesGridZOffset;
        }

        public void RecalculateZOffset()
        {
            if (!_isShowing)
                return;
            UpdateZOffset(GetResolvedZOffset());
        }

        public void Dispose()
        {
            Hide();
        }
    }
}
