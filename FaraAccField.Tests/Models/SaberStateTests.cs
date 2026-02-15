using UnityEngine;
using Xunit;
using FaraAccField.Models;

namespace FaraAccField.Tests.Models
{
    public class SaberStateTests
    {
        #region UpdateState

        [Fact]
        public void UpdateState_SetsCurrentPosition()
        {
            var state = new SaberState();
            var tip = new Vector3(1f, 2f, 3f);
            var handle = new Vector3(1f, 1f, 3f);

            state.UpdateState(tip, handle, 0.1f);

            Assert.Equal(tip, state.CurrentPosition);
        }

        [Fact]
        public void UpdateState_MultipleUpdates_ReflectsLatest()
        {
            var state = new SaberState();
            state.UpdateState(new Vector3(1f, 1f, 0f), Vector3.zero, 0.1f);

            var newTip = new Vector3(5f, 6f, 7f);
            var newHandle = new Vector3(5f, 5f, 7f);
            state.UpdateState(newTip, newHandle, 0.2f);

            Assert.Equal(newTip, state.CurrentPosition);
        }

        #endregion

        #region BladeDirection

        [Fact]
        public void BladeDirection_TipAboveHandle_ReturnsUp()
        {
            var state = new SaberState();
            state.UpdateState(new Vector3(0f, 1f, 0f), Vector3.zero, 0f);

            AssertVector3Approximately(Vector3.up, state.BladeDirection);
        }

        [Fact]
        public void BladeDirection_TipLeftOfHandle_ReturnsLeft()
        {
            var state = new SaberState();
            state.UpdateState(new Vector3(-1f, 0f, 0f), Vector3.zero, 0f);

            AssertVector3Approximately(Vector3.left, state.BladeDirection);
        }

        [Fact]
        public void BladeDirection_TipBelowHandle_ReturnsDown()
        {
            var state = new SaberState();
            state.UpdateState(new Vector3(0f, -1f, 0f), Vector3.zero, 0f);

            AssertVector3Approximately(Vector3.down, state.BladeDirection);
        }

        [Fact]
        public void BladeDirection_IsNormalized()
        {
            var state = new SaberState();
            state.UpdateState(new Vector3(3f, 4f, 0f), Vector3.zero, 0f);

            Assert.InRange(state.BladeDirection.magnitude, 0.99f, 1.01f);
        }

        #endregion

        #region TipVelocity and TipSpeed

        [Fact]
        public void TipVelocity_SingleUpdate_ReturnsZero()
        {
            var state = new SaberState();
            state.UpdateState(new Vector3(1f, 1f, 0f), Vector3.zero, 0.1f);

            Assert.Equal(Vector3.zero, state.TipVelocity);
        }

        [Fact]
        public void TipVelocity_TwoUpdates_CalculatesCorrectly()
        {
            var state = new SaberState();
            state.UpdateState(new Vector3(0f, 1f, 0f), Vector3.zero, 0.0f);
            state.UpdateState(new Vector3(1f, 1f, 0f), Vector3.zero, 1.0f);

            AssertVector3Approximately(new Vector3(1f, 0f, 0f), state.TipVelocity);
        }

        [Fact]
        public void TipVelocity_FastMovement_HighVelocity()
        {
            var state = new SaberState();
            state.UpdateState(new Vector3(0f, 1f, 0f), Vector3.zero, 0.0f);
            state.UpdateState(new Vector3(10f, 1f, 0f), Vector3.zero, 0.1f);

            AssertVector3Approximately(new Vector3(100f, 0f, 0f), state.TipVelocity);
        }

        [Fact]
        public void TipSpeed_ReturnsMagnitudeOfVelocity()
        {
            var state = new SaberState();
            state.UpdateState(new Vector3(0f, 1f, 0f), Vector3.zero, 0.0f);
            state.UpdateState(new Vector3(3f, 5f, 0f), Vector3.zero, 1.0f);

            // Velocity = (3, 4, 0), magnitude = 5
            Assert.InRange(state.TipSpeed, 4.9f, 5.1f);
        }

        [Fact]
        public void TipVelocity_ZeroDeltaTime_DoesNotDivideByZero()
        {
            var state = new SaberState();
            state.UpdateState(new Vector3(0f, 1f, 0f), Vector3.zero, 1.0f);
            state.UpdateState(new Vector3(1f, 1f, 0f), Vector3.zero, 1.0f);

            Assert.Equal(Vector3.zero, state.TipVelocity);
        }

        #endregion

        #region BladeAngularSpeed

        [Fact]
        public void BladeAngularSpeed_SingleUpdate_ReturnsZero()
        {
            var state = new SaberState();
            state.UpdateState(new Vector3(0f, 1f, 0f), Vector3.zero, 0f);

            Assert.Equal(0f, state.BladeAngularSpeed);
        }

        [Fact]
        public void BladeAngularSpeed_NoRotation_ReturnsZero()
        {
            var state = new SaberState();
            // Both updates: blade points up
            state.UpdateState(new Vector3(0f, 1f, 0f), Vector3.zero, 0.0f);
            state.UpdateState(new Vector3(0f, 2f, 0f), Vector3.zero, 1.0f);

            Assert.InRange(state.BladeAngularSpeed, -0.1f, 0.1f);
        }

        [Fact]
        public void BladeAngularSpeed_90DegreeRotation_ReturnsHighValue()
        {
            var state = new SaberState();
            // Blade points up, then points right
            state.UpdateState(new Vector3(0f, 1f, 0f), Vector3.zero, 0.0f);
            state.UpdateState(new Vector3(1f, 0f, 0f), Vector3.zero, 1.0f);

            // 90 degrees in 1 second = 90 deg/s
            Assert.InRange(state.BladeAngularSpeed, 85f, 95f);
        }

        #endregion

        #region GetAverageBladeDirection

        [Fact]
        public void GetAverageBladeDirection_NoHistory_ReturnsCurrentBladeDirection()
        {
            var state = new SaberState();
            Assert.Equal(Vector3.zero, state.GetAverageBladeDirection());
        }

        [Fact]
        public void GetAverageBladeDirection_SingleFrame_ReturnsCurrentDirection()
        {
            var state = new SaberState();
            state.UpdateState(new Vector3(0f, 1f, 0f), Vector3.zero, 0f);

            AssertVector3Approximately(Vector3.up, state.GetAverageBladeDirection(3));
        }

        [Fact]
        public void GetAverageBladeDirection_MultipleFramesSameDirection_ReturnsSameDirection()
        {
            var state = new SaberState();
            for (int i = 0; i < 5; i++)
            {
                state.UpdateState(new Vector3(0f, 1f + i, 0f), new Vector3(0f, (float)i, 0f), i * 0.1f);
            }

            AssertVector3Approximately(Vector3.up, state.GetAverageBladeDirection(3));
        }

        [Fact]
        public void GetAverageBladeDirection_RequestMoreFramesThanAvailable_ClampsToAvailable()
        {
            var state = new SaberState();
            state.UpdateState(new Vector3(0f, 1f, 0f), Vector3.zero, 0f);
            state.UpdateState(new Vector3(0f, 2f, 0f), new Vector3(0f, 1f, 0f), 0.1f);

            var direction = state.GetAverageBladeDirection(5);
            AssertVector3Approximately(Vector3.up, direction);
        }

        #endregion

        #region Reset

        [Fact]
        public void Reset_ClearsVelocity()
        {
            var state = new SaberState();
            state.UpdateState(new Vector3(0f, 1f, 0f), Vector3.zero, 0f);
            state.UpdateState(new Vector3(10f, 1f, 0f), Vector3.zero, 0.1f);

            Assert.NotEqual(Vector3.zero, state.TipVelocity);

            state.Reset();

            Assert.Equal(Vector3.zero, state.TipVelocity);
            Assert.Equal(0f, state.BladeAngularSpeed);
        }

        [Fact]
        public void Reset_NextUpdateDoesNotUseOldHistory()
        {
            var state = new SaberState();
            state.UpdateState(new Vector3(0f, 1f, 0f), Vector3.zero, 0f);
            state.UpdateState(new Vector3(100f, 1f, 0f), Vector3.zero, 0.1f);

            state.Reset();

            state.UpdateState(new Vector3(1f, 1f, 0f), Vector3.zero, 1.0f);
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
                state.UpdateState(new Vector3(i, 1f, 0f), Vector3.zero, i * 0.016f);
            }

            Assert.True(state.TipSpeed > 0f);
        }

        [Fact]
        public void HistoryBuffer_CycledBuffer_VelocityStillCorrect()
        {
            var state = new SaberState();
            for (int i = 0; i < 15; i++)
            {
                state.UpdateState(new Vector3(i * 2f, 1f, 0f), Vector3.zero, i * 1.0f);
            }

            AssertVector3Approximately(new Vector3(2f, 0f, 0f), state.TipVelocity);
        }

        [Fact]
        public void HistoryBuffer_CycledBuffer_AverageDirectionStillWorks()
        {
            var state = new SaberState();
            for (int i = 0; i < 15; i++)
            {
                state.UpdateState(new Vector3(0f, 1f + i, 0f), new Vector3(0f, (float)i, 0f), i * 0.016f);
            }

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

        #endregion
    }
}
