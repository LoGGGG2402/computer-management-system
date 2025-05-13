using System;
using Xunit;
using CMSAgent.Common;

namespace CMSAgent.UnitTests.Common
{
    public class DateTimeProviderTests
    {
        [Fact]
        public void UtcNow_ReturnsCurrentUtcTime()
        {
            // Arrange
            var provider = new DateTimeProvider();
            var systemUtcNow = DateTime.UtcNow;

            // Act
            var result = provider.UtcNow;

            // Assert
            Assert.True((result - systemUtcNow).TotalSeconds < 1, "Thời gian UtcNow phải gần với thời gian hệ thống");
        }

        [Fact]
        public void Now_ReturnsCurrentLocalTime()
        {
            // Arrange
            var provider = new DateTimeProvider();
            var systemNow = DateTime.Now;

            // Act
            var result = provider.Now;

            // Assert
            Assert.True((result - systemNow).TotalSeconds < 1, "Thời gian Now phải gần với thời gian hệ thống");
        }

        [Fact]
        public void Today_ReturnsCurrentLocalDate()
        {
            // Arrange
            var provider = new DateTimeProvider();
            var systemToday = DateTime.Today;

            // Act
            var result = provider.Today;

            // Assert
            Assert.Equal(systemToday, result);
            Assert.Equal(0, result.Hour);
            Assert.Equal(0, result.Minute);
            Assert.Equal(0, result.Second);
            Assert.Equal(0, result.Millisecond);
        }
    }
} 