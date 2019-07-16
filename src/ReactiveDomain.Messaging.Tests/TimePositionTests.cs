using System;
using System.Diagnostics.CodeAnalysis;
using ReactiveDomain.Messaging.Bus;
using Xunit;


namespace ReactiveDomain.Messaging.Tests {
    public class TimePositionTests {
        [Fact]
        public void CanCreateTimePositions() {
            var p0 = new TimePosition(0);
            var pMax = new TimePosition(long.MaxValue);
            Assert.IsType<TimePosition>(p0);
            Assert.IsType<TimePosition>(pMax);
            Assert.IsType<TimePosition>(TimePosition.StartPosition);
            Assert.Equal(p0, TimePosition.StartPosition);
            Assert.Throws<ArgumentOutOfRangeException>(() => new TimePosition(-1));
        }
        [Fact]
        [SuppressMessage("ReSharper", "RedundantCast")]
        [SuppressMessage("ReSharper", "EqualExpressionComparison")]
        public void TimePositionsEquateByValue() {
            var p1 = new TimePosition(50);
            var p2 = new TimePosition(50);
            var p3 = new TimePosition(49);
            
            Assert.Equal(p1, p2);
            Assert.Equal(p1, (object)p2);
            Assert.True(p1 == p2);
            Assert.True(p1 != p3);
            Assert.True(p1.Equals(p2));
            Assert.True(p2.Equals(p1));
            Assert.False(p1.Equals(null));
            Assert.False(p1 == (TimePosition)null);
            Assert.False((TimePosition)null == p1);
            Assert.True((TimePosition)null == (TimePosition)null);
            Assert.False(p1 == p3);
#pragma warning disable CS1718 // Comparison made to same variable
            Assert.True(p1 == p1);
#pragma warning restore CS1718 // Comparison made to same variable
            Assert.True(p1.Equals(p1));
            Assert.True(p1.GetHashCode() == p2.GetHashCode());
        }
        [Fact]
        [SuppressMessage("ReSharper", "RedundantCast")]
        public void TimePositionsCanBeCompared() {
            var p1 = new TimePosition(50);
            var p2 = new TimePosition(50);
            var p3 = new TimePosition(51);
            var p4 = new TimePosition(51);
            var p5 = new TimePosition(49);
            Assert.True(p1 < p3);
            Assert.True(p1 <= p3);
            Assert.True(p1 <= p2);
            Assert.True(p3 > p2);
            Assert.True(p3 >= p4);
            Assert.True(p3 >= p1);
            Assert.True(p1.CompareTo(p3) < 0);
            Assert.True(p1.CompareTo(p2) == 0);
            Assert.True(p1.CompareTo(p5) > 0);
            Assert.True(TimePosition.StartPosition.CompareTo((TimePosition)null) < 0 );

            Assert.Throws<ArgumentNullException>(()=> p1 > (TimePosition) null);
            Assert.Throws<ArgumentNullException>(()=> (TimePosition) null > p1);
            Assert.Throws<ArgumentNullException>(()=> p2 < (TimePosition) null);
            Assert.Throws<ArgumentNullException>(()=> (TimePosition) null < p1);
            Assert.Throws<ArgumentNullException>(()=> p1 >= (TimePosition) null);
            Assert.Throws<ArgumentNullException>(()=> (TimePosition) null >= p1);
            Assert.Throws<ArgumentNullException>(()=> p2 <= (TimePosition) null);
            Assert.Throws<ArgumentNullException>(()=> (TimePosition) null <= p1);
        }
        [Fact]
        public void TimePositionsCanAddAndSubtract() {
            var p1 = new TimePosition(50);
            var p2 = new TimePosition(52);
            var twoMs = TimeSpan.FromMilliseconds(2);
            Assert.True(p1 + twoMs == p2);
            var subTime = p2 - twoMs;
            Assert.True(subTime == p1);
            Assert.Throws<ArgumentNullException>(()=> (TimePosition) null + twoMs);
            Assert.Throws<ArgumentNullException>(()=> (TimePosition) null - twoMs);
        }
        [Fact]
        public void TimePositionsCanMeasureDistanceForward() {
            var p1 = new TimePosition(50);
            var p2 = new TimePosition(52);

            Assert.True(p1.DistanceUntil(p2) == TimeSpan.FromMilliseconds(2));
            Assert.True(p2.DistanceUntil(p1) == TimeSpan.Zero);
        }
        [Fact]
        public void StartPositionIsTimePositionZero() {
            Assert.Equal(new TimePosition(0L), TimePosition.StartPosition);
        }


    }
}
