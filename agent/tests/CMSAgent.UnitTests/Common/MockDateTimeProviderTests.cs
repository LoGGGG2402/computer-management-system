using System;
using Xunit;

namespace CMSAgent.UnitTests.Common
{
    public class MockDateTimeProviderTests
    {
        [Fact]
        public void DefaultConstructor_InitializesWithCurrentTime()
        {
            // Arrange & Act
            var provider = new MockDateTimeProvider();
            
            // Assert
            Assert.True((provider.UtcNow - DateTime.UtcNow).TotalSeconds < 1);
            Assert.True((provider.Now - DateTime.Now).TotalSeconds < 1);
            Assert.Equal(DateTime.Today, provider.Today);
        }

        [Fact]
        public void FixedDateTimeConstructor_InitializesWithSpecifiedTime()
        {
            // Arrange
            var fixedDate = new DateTime(2023, 5, 15, 10, 30, 0);
            
            // Act
            var provider = new MockDateTimeProvider(fixedDate);
            
            // Assert
            Assert.Equal(fixedDate.ToUniversalTime(), provider.UtcNow);
            Assert.Equal(fixedDate, provider.Now);
            Assert.Equal(fixedDate.Date, provider.Today);
        }

        [Fact]
        public void SetDateTime_UpdatesAllTimeProperties()
        {
            // Arrange
            var provider = new MockDateTimeProvider();
            var newDateTime = new DateTime(2024, 6, 20, 14, 45, 30);
            
            // Act
            provider.SetDateTime(newDateTime);
            
            // Assert
            Assert.Equal(newDateTime.ToUniversalTime(), provider.UtcNow);
            Assert.Equal(newDateTime, provider.Now);
            Assert.Equal(newDateTime.Date, provider.Today);
        }

        [Fact]
        public void SetUtcNow_UpdatesOnlyUtcNowProperty()
        {
            // Arrange
            var initialDate = new DateTime(2023, 5, 15, 10, 30, 0);
            var provider = new MockDateTimeProvider(initialDate);
            var newUtcNow = new DateTime(2023, 5, 15, 15, 30, 0, DateTimeKind.Utc);
            
            // Act
            provider.SetUtcNow(newUtcNow);
            
            // Assert
            Assert.Equal(newUtcNow, provider.UtcNow);
            Assert.Equal(initialDate, provider.Now);
            Assert.Equal(initialDate.Date, provider.Today);
        }

        [Fact]
        public void SetNow_UpdatesOnlyNowProperty()
        {
            // Arrange
            var initialDate = new DateTime(2023, 5, 15, 10, 30, 0);
            var provider = new MockDateTimeProvider(initialDate);
            var newNow = new DateTime(2023, 5, 15, 14, 45, 30);
            
            // Act
            provider.SetNow(newNow);
            
            // Assert
            Assert.Equal(initialDate.ToUniversalTime(), provider.UtcNow);
            Assert.Equal(newNow, provider.Now);
            Assert.Equal(initialDate.Date, provider.Today);
        }

        [Fact]
        public void SetToday_UpdatesOnlyTodayProperty()
        {
            // Arrange
            var initialDate = new DateTime(2023, 5, 15, 10, 30, 0);
            var provider = new MockDateTimeProvider(initialDate);
            var newToday = new DateTime(2023, 6, 1, 14, 45, 30);
            
            // Act
            provider.SetToday(newToday);
            
            // Assert
            Assert.Equal(initialDate.ToUniversalTime(), provider.UtcNow);
            Assert.Equal(initialDate, provider.Now);
            Assert.Equal(newToday.Date, provider.Today);
            Assert.Equal(0, provider.Today.Hour);
            Assert.Equal(0, provider.Today.Minute);
            Assert.Equal(0, provider.Today.Second);
        }
    }
} 