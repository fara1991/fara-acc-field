using System;
using UnityEngine;

namespace FaraAccField.Services
{
    internal static class VisualHelper
    {
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
    }
}
