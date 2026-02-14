using UnityEngine;
using Xunit;
using FaraAccField.Models;

namespace FaraAccField.Tests.Models
{
    public class SaberStateTests
    {
        #region UpdateState

        [Fact]
        public void UpdateState_SetsCurrentPositionAndRotation()
        {
            var state = new SaberState();
            var position = new Vector3(1f, 2f, 3f);
            var rotation = EulerToQuaternion(45f, 0f, 0f);

            state.UpdateState(position, rotation, 0.1f);

            Assert.Equal(position, state.CurrentPosition);
            Assert.Equal(rotation, state.CurrentRotation);
        }

        [Fact]
        public void UpdateState_MultipleUpdates_ReflectsLatest()
        {
            var state = new SaberState();
            state.UpdateState(new Vector3(1f, 0f, 0f), Quaternion.identity, 0.1f);

            var newPos = new Vector3(5f, 6f, 7f);
            var newRot = EulerToQuaternion(90f, 0f, 0f);
            state.UpdateState(newPos, newRot, 0.2f);

            Assert.Equal(newPos, state.CurrentPosition);
            Assert.Equal(newRot, state.CurrentRotation);
        }

        #endregion

        #region BladeDirection

        [Fact]
        public void BladeDirection_IdentityRotation_ReturnsUp()
        {
            var state = new SaberState();
            state.UpdateState(Vector3.zero, Quaternion.identity, 0f);

            AssertVector3Approximately(Vector3.up, state.BladeDirection);
        }

        [Fact]
        public void BladeDirection_90DegreeRotationAroundZ_ReturnsLeft()
        {
            var state = new SaberState();
            // Rotating 90 degrees around Z axis: Vector3.up becomes Vector3.left
            state.UpdateState(Vector3.zero, EulerToQuaternion(0f, 0f, 90f), 0f);

            AssertVector3Approximately(Vector3.left, state.BladeDirection);
        }

        [Fact]
        public void BladeDirection_180DegreeRotationAroundZ_ReturnsDown()
        {
            var state = new SaberState();
            state.UpdateState(Vector3.zero, EulerToQuaternion(0f, 0f, 180f), 0f);

            AssertVector3Approximately(Vector3.down, state.BladeDirection);
        }

        #endregion

        #region TipVelocity and TipSpeed

        [Fact]
        public void TipVelocity_SingleUpdate_ReturnsZero()
        {
            var state = new SaberState();
            state.UpdateState(new Vector3(1f, 0f, 0f), Quaternion.identity, 0.1f);

            Assert.Equal(Vector3.zero, state.TipVelocity);
        }

        [Fact]
        public void TipVelocity_TwoUpdates_CalculatesCorrectly()
        {
            var state = new SaberState();
            state.UpdateState(new Vector3(0f, 0f, 0f), Quaternion.identity, 0.0f);
            state.UpdateState(new Vector3(1f, 0f, 0f), Quaternion.identity, 1.0f);

            // Moved 1 unit in X over 1 second => velocity = (1, 0, 0)
            AssertVector3Approximately(new Vector3(1f, 0f, 0f), state.TipVelocity);
        }

        [Fact]
        public void TipVelocity_FastMovement_HighVelocity()
        {
            var state = new SaberState();
            state.UpdateState(new Vector3(0f, 0f, 0f), Quaternion.identity, 0.0f);
            state.UpdateState(new Vector3(10f, 0f, 0f), Quaternion.identity, 0.1f);

            // Moved 10 units in 0.1 seconds => velocity = (100, 0, 0)
            AssertVector3Approximately(new Vector3(100f, 0f, 0f), state.TipVelocity);
        }

        [Fact]
        public void TipSpeed_ReturnsMagnitudeOfVelocity()
        {
            var state = new SaberState();
            state.UpdateState(new Vector3(0f, 0f, 0f), Quaternion.identity, 0.0f);
            state.UpdateState(new Vector3(3f, 4f, 0f), Quaternion.identity, 1.0f);

            // Velocity = (3, 4, 0), magnitude = 5
            Assert.InRange(state.TipSpeed, 4.9f, 5.1f);
        }

        [Fact]
        public void TipVelocity_ZeroDeltaTime_DoesNotDivideByZero()
        {
            var state = new SaberState();
            state.UpdateState(new Vector3(0f, 0f, 0f), Quaternion.identity, 1.0f);
            state.UpdateState(new Vector3(1f, 0f, 0f), Quaternion.identity, 1.0f);

            // deltaTime = 0, should not update velocity (stays at zero)
            Assert.Equal(Vector3.zero, state.TipVelocity);
        }

        #endregion

        #region GetAverageBladeDirection

        [Fact]
        public void GetAverageBladeDirection_NoHistory_ReturnsCurrentBladeDirection()
        {
            var state = new SaberState();

            // Before any updates, BladeDirection is zero so average should be zero
            Assert.Equal(Vector3.zero, state.GetAverageBladeDirection());
        }

        [Fact]
        public void GetAverageBladeDirection_SingleFrame_ReturnsCurrentDirection()
        {
            var state = new SaberState();
            state.UpdateState(Vector3.zero, Quaternion.identity, 0f);

            AssertVector3Approximately(Vector3.up, state.GetAverageBladeDirection(3));
        }

        [Fact]
        public void GetAverageBladeDirection_MultipleFramesSameDirection_ReturnsSameDirection()
        {
            var state = new SaberState();
            for (int i = 0; i < 5; i++)
            {
                state.UpdateState(Vector3.zero, Quaternion.identity, i * 0.1f);
            }

            AssertVector3Approximately(Vector3.up, state.GetAverageBladeDirection(3));
        }

        [Fact]
        public void GetAverageBladeDirection_RequestMoreFramesThanAvailable_ClampsToAvailable()
        {
            var state = new SaberState();
            state.UpdateState(Vector3.zero, Quaternion.identity, 0f);
            state.UpdateState(Vector3.zero, Quaternion.identity, 0.1f);

            // Requesting 5 frames but only 2 available - should not throw
            var direction = state.GetAverageBladeDirection(5);
            AssertVector3Approximately(Vector3.up, direction);
        }

        #endregion

        #region Reset

        [Fact]
        public void Reset_ClearsVelocity()
        {
            var state = new SaberState();
            state.UpdateState(new Vector3(0f, 0f, 0f), Quaternion.identity, 0f);
            state.UpdateState(new Vector3(10f, 0f, 0f), Quaternion.identity, 0.1f);

            Assert.NotEqual(Vector3.zero, state.TipVelocity);

            state.Reset();

            Assert.Equal(Vector3.zero, state.TipVelocity);
        }

        [Fact]
        public void Reset_NextUpdateDoesNotUseOldHistory()
        {
            var state = new SaberState();
            state.UpdateState(new Vector3(0f, 0f, 0f), Quaternion.identity, 0f);
            state.UpdateState(new Vector3(100f, 0f, 0f), Quaternion.identity, 0.1f);

            state.Reset();

            // After reset, first update should not compute velocity from old data
            state.UpdateState(new Vector3(1f, 0f, 0f), Quaternion.identity, 1.0f);
            Assert.Equal(Vector3.zero, state.TipVelocity);
        }

        #endregion

        #region History Buffer Cycling

        [Fact]
        public void HistoryBuffer_MoreThan10Updates_DoesNotThrow()
        {
            var state = new SaberState();
            for (int i = 0; i < 20; i++)
            {
                state.UpdateState(new Vector3(i, 0f, 0f), Quaternion.identity, i * 0.016f);
            }

            // Should still work correctly after cycling through the buffer
            Assert.True(state.TipSpeed > 0f);
        }

        [Fact]
        public void HistoryBuffer_CycledBuffer_VelocityStillCorrect()
        {
            var state = new SaberState();
            // Fill buffer beyond capacity (10)
            for (int i = 0; i < 15; i++)
            {
                state.UpdateState(new Vector3(i * 2f, 0f, 0f), Quaternion.identity, i * 1.0f);
            }

            // Last two updates: pos=26 at t=13, pos=28 at t=14
            // velocity = (28-26)/(14-13) = (2, 0, 0)
            AssertVector3Approximately(new Vector3(2f, 0f, 0f), state.TipVelocity);
        }

        [Fact]
        public void HistoryBuffer_CycledBuffer_AverageDirectionStillWorks()
        {
            var state = new SaberState();
            for (int i = 0; i < 15; i++)
            {
                state.UpdateState(Vector3.zero, Quaternion.identity, i * 0.016f);
            }

            // All frames had identity rotation => all directions are Vector3.up
            AssertVector3Approximately(Vector3.up, state.GetAverageBladeDirection(3));
        }

        #endregion

        #region Helpers

        private static void AssertVector3Approximately(Vector3 expected, Vector3 actual, float tolerance = 0.01f)
        {
            Assert.InRange(actual.x, expected.x - tolerance, expected.x + tolerance);
            Assert.InRange(actual.y, expected.y - tolerance, expected.y + tolerance);
            Assert.InRange(actual.z, expected.z - tolerance, expected.z + tolerance);
        }

        /// <summary>
        /// Pure C# Euler-to-Quaternion conversion (Unity's Quaternion.Euler uses native calls).
        /// </summary>
        private static Quaternion EulerToQuaternion(float xDeg, float yDeg, float zDeg)
        {
            float xRad = xDeg * Mathf.Deg2Rad * 0.5f;
            float yRad = yDeg * Mathf.Deg2Rad * 0.5f;
            float zRad = zDeg * Mathf.Deg2Rad * 0.5f;

            float cx = Mathf.Cos(xRad), sx = Mathf.Sin(xRad);
            float cy = Mathf.Cos(yRad), sy = Mathf.Sin(yRad);
            float cz = Mathf.Cos(zRad), sz = Mathf.Sin(zRad);

            // Unity uses ZXY intrinsic rotation order
            return new Quaternion(
                cy * sx * cz + sy * cx * sz,
                sy * cx * cz - cy * sx * sz,
                cy * cx * sz - sy * sx * cz,
                cy * cx * cz + sy * sx * sz
            );
        }

        #endregion
    }
}
