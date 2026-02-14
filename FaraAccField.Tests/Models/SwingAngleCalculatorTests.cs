using UnityEngine;
using Xunit;
using FaraAccField.Models;

namespace FaraAccField.Tests.Models
{
    public class SwingAngleCalculatorTests
    {
        private const float Tolerance = 0.1f;

        #region CalculatePreSwingAngle

        [Fact]
        public void CalculatePreSwingAngle_ZeroSaberDirection_ReturnsZero()
        {
            float angle = SwingAngleCalculator.CalculatePreSwingAngle(Vector3.zero, Vector3.down);
            Assert.Equal(0f, angle);
        }

        [Fact]
        public void CalculatePreSwingAngle_ZeroCutDirection_ReturnsZero()
        {
            float angle = SwingAngleCalculator.CalculatePreSwingAngle(Vector3.up, Vector3.zero);
            Assert.Equal(0f, angle);
        }

        [Fact]
        public void CalculatePreSwingAngle_SameDirection_Returns180()
        {
            // Saber pointing up, cut direction is down => windup = -down = up => dot=1 => angle=0 => 180-0=180
            float angle = SwingAngleCalculator.CalculatePreSwingAngle(Vector3.up, Vector3.down);
            Assert.InRange(angle, 180f - Tolerance, 180f + Tolerance);
        }

        [Fact]
        public void CalculatePreSwingAngle_OppositeDirection_ReturnsZero()
        {
            // Saber pointing up, cut direction is up => windup = -up = down => dot=-1 => angle=180 => 180-180=0
            float angle = SwingAngleCalculator.CalculatePreSwingAngle(Vector3.up, Vector3.up);
            Assert.InRange(angle, 0f - Tolerance, 0f + Tolerance);
        }

        [Fact]
        public void CalculatePreSwingAngle_Perpendicular_Returns90()
        {
            // Saber pointing up, cut direction is right => windup = -right = left => dot(up, left) = 0 => angle=90 => 180-90=90
            float angle = SwingAngleCalculator.CalculatePreSwingAngle(Vector3.up, Vector3.right);
            Assert.InRange(angle, 90f - Tolerance, 90f + Tolerance);
        }

        [Fact]
        public void CalculatePreSwingAngle_IntermediateAngle_ReturnsExpected()
        {
            // Create a 45-degree scenario
            // Saber direction: up, Cut direction: normalized(-1, -1, 0) => windup = (1, 1, 0).normalized
            // dot(up, (1,1,0).normalized) = 1/sqrt(2) => acos = 45 => 180-45 = 135
            Vector3 cutDir = new Vector3(-1f, -1f, 0f).normalized;
            float angle = SwingAngleCalculator.CalculatePreSwingAngle(Vector3.up, cutDir);
            Assert.InRange(angle, 135f - Tolerance, 135f + Tolerance);
        }

        #endregion

        #region MeetsPreSwingThreshold

        [Theory]
        [InlineData(99.9f, false)]
        [InlineData(100.0f, true)]
        [InlineData(100.1f, true)]
        [InlineData(0f, false)]
        [InlineData(180f, true)]
        public void MeetsPreSwingThreshold_BoundaryValues(float angle, bool expected)
        {
            Assert.Equal(expected, SwingAngleCalculator.MeetsPreSwingThreshold(angle));
        }

        #endregion

        #region CalculatePreSwingPoints

        [Theory]
        [InlineData(0f, 0f)]
        [InlineData(50f, 35f)]
        [InlineData(100f, 70f)]
        [InlineData(150f, 70f)] // Clamped to max
        public void CalculatePreSwingPoints_ReturnsExpected(float angle, float expectedPoints)
        {
            float points = SwingAngleCalculator.CalculatePreSwingPoints(angle);
            Assert.InRange(points, expectedPoints - Tolerance, expectedPoints + Tolerance);
        }

        [Fact]
        public void CalculatePreSwingPoints_NegativeAngle_ReturnsZero()
        {
            float points = SwingAngleCalculator.CalculatePreSwingPoints(-10f);
            Assert.InRange(points, 0f - Tolerance, 0f + Tolerance);
        }

        #endregion

        #region CalculateFollowThroughAngle

        [Fact]
        public void CalculateFollowThroughAngle_ZeroSaberDirection_ReturnsZero()
        {
            float angle = SwingAngleCalculator.CalculateFollowThroughAngle(Vector3.zero, Vector3.down);
            Assert.Equal(0f, angle);
        }

        [Fact]
        public void CalculateFollowThroughAngle_ZeroCutDirection_ReturnsZero()
        {
            float angle = SwingAngleCalculator.CalculateFollowThroughAngle(Vector3.up, Vector3.zero);
            Assert.Equal(0f, angle);
        }

        [Fact]
        public void CalculateFollowThroughAngle_SameDirection_Returns180()
        {
            // Saber pointing same as cut direction => dot=1 => angle=0 => 180-0=180
            float angle = SwingAngleCalculator.CalculateFollowThroughAngle(Vector3.down, Vector3.down);
            Assert.InRange(angle, 180f - Tolerance, 180f + Tolerance);
        }

        [Fact]
        public void CalculateFollowThroughAngle_OppositeDirection_ReturnsZero()
        {
            // Saber pointing opposite to cut direction => dot=-1 => angle=180 => 180-180=0
            float angle = SwingAngleCalculator.CalculateFollowThroughAngle(Vector3.up, Vector3.down);
            Assert.InRange(angle, 0f - Tolerance, 0f + Tolerance);
        }

        [Fact]
        public void CalculateFollowThroughAngle_Perpendicular_Returns90()
        {
            float angle = SwingAngleCalculator.CalculateFollowThroughAngle(Vector3.up, Vector3.right);
            Assert.InRange(angle, 90f - Tolerance, 90f + Tolerance);
        }

        #endregion

        #region MeetsFollowThroughThreshold

        [Theory]
        [InlineData(59.9f, false)]
        [InlineData(60.0f, true)]
        [InlineData(60.1f, true)]
        [InlineData(0f, false)]
        [InlineData(180f, true)]
        public void MeetsFollowThroughThreshold_BoundaryValues(float angle, bool expected)
        {
            Assert.Equal(expected, SwingAngleCalculator.MeetsFollowThroughThreshold(angle));
        }

        #endregion

        #region CalculateFollowThroughPoints

        [Theory]
        [InlineData(0f, 0f)]
        [InlineData(30f, 15f)]
        [InlineData(60f, 30f)]
        [InlineData(90f, 30f)] // Clamped to max
        public void CalculateFollowThroughPoints_ReturnsExpected(float angle, float expectedPoints)
        {
            float points = SwingAngleCalculator.CalculateFollowThroughPoints(angle);
            Assert.InRange(points, expectedPoints - Tolerance, expectedPoints + Tolerance);
        }

        [Fact]
        public void CalculateFollowThroughPoints_NegativeAngle_ReturnsZero()
        {
            float points = SwingAngleCalculator.CalculateFollowThroughPoints(-10f);
            Assert.InRange(points, 0f - Tolerance, 0f + Tolerance);
        }

        #endregion

        #region Constants

        [Fact]
        public void Constants_HaveCorrectValues()
        {
            Assert.Equal(100f, SwingAngleCalculator.PreSwingMaxAngle);
            Assert.Equal(70f, SwingAngleCalculator.PreSwingMaxPoints);
            Assert.Equal(60f, SwingAngleCalculator.FollowThroughMaxAngle);
            Assert.Equal(30f, SwingAngleCalculator.FollowThroughMaxPoints);
        }

        #endregion
    }
}
