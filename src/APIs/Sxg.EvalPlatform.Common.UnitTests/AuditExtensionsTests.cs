using SXG.EvalPlatform.Common;

namespace Sxg.EvalPlatform.Common.UnitTests
{
    /// <summary>
    /// Unit tests for AuditExtensions class covering all public extension methods
    /// </summary>
    public class AuditExtensionsTests
    {
        #region Test Helper Class

        /// <summary>
        /// Simple test implementation of IAuditableEntity for testing purposes
        /// </summary>
        private class TestAuditableEntity : IAuditableEntity
        {
            public string? CreatedBy { get; set; }
            public DateTime? CreatedOn { get; set; }
            public string? LastUpdatedBy { get; set; }
            public DateTime? LastUpdatedOn { get; set; }
        }

        #endregion

        #region SetCreationAudit Tests

        [Fact]
        public void SetCreationAudit_WithValidUser_SetsAllAuditProperties()
        {
            // Arrange
            var entity = new TestAuditableEntity();
            var auditUser = "test-user@example.com";
            var beforeCall = DateTime.UtcNow;

            // Act
            entity.SetCreationAudit(auditUser);
            var afterCall = DateTime.UtcNow;

            // Assert
            Assert.Equal(auditUser, entity.CreatedBy);
            Assert.Equal(auditUser, entity.LastUpdatedBy);
            Assert.NotNull(entity.CreatedOn);
            Assert.NotNull(entity.LastUpdatedOn);
            
            // Verify timestamps are within reasonable range
            Assert.True(entity.CreatedOn >= beforeCall && entity.CreatedOn <= afterCall);
            Assert.True(entity.LastUpdatedOn >= beforeCall && entity.LastUpdatedOn <= afterCall);
        }

        [Fact]
        public void SetCreationAudit_SetsCreatedOnAndLastUpdatedOnToSameValue()
        {
            // Arrange
            var entity = new TestAuditableEntity();
            var auditUser = "test-user";

            // Act
            entity.SetCreationAudit(auditUser);

            // Assert
            Assert.NotNull(entity.CreatedOn);
            Assert.NotNull(entity.LastUpdatedOn);
            Assert.Equal(entity.CreatedOn, entity.LastUpdatedOn);
        }

        [Fact]
        public void SetCreationAudit_SetsTimestampsToUtcNow()
        {
            // Arrange
            var entity = new TestAuditableEntity();
            var auditUser = "test-user";
            var beforeCall = DateTime.UtcNow;

            // Act
            entity.SetCreationAudit(auditUser);
            var afterCall = DateTime.UtcNow;

            // Assert
            Assert.NotNull(entity.CreatedOn);
            Assert.InRange(entity.CreatedOn.Value, beforeCall, afterCall);
            Assert.Equal(DateTimeKind.Utc, entity.CreatedOn.Value.Kind);
        }

        [Fact]
        public void SetCreationAudit_OverwritesExistingValues()
        {
            // Arrange
            var entity = new TestAuditableEntity
            {
                CreatedBy = "old-user",
                CreatedOn = DateTime.UtcNow.AddDays(-1),
                LastUpdatedBy = "old-updater",
                LastUpdatedOn = DateTime.UtcNow.AddHours(-1)
            };
            var newUser = "new-user";

            // Act
            entity.SetCreationAudit(newUser);

            // Assert
            Assert.Equal(newUser, entity.CreatedBy);
            Assert.Equal(newUser, entity.LastUpdatedBy);
            Assert.True(entity.CreatedOn > DateTime.UtcNow.AddMinutes(-1));
            Assert.True(entity.LastUpdatedOn > DateTime.UtcNow.AddMinutes(-1));
        }

        [Theory]
        [InlineData("user@example.com")]
        [InlineData("system")]
        [InlineData("admin")]
        [InlineData("service-account")]
        [InlineData("John Doe")]
        public void SetCreationAudit_WithDifferentUserFormats_SetsCorrectly(string auditUser)
        {
            // Arrange
            var entity = new TestAuditableEntity();

            // Act
            entity.SetCreationAudit(auditUser);

            // Assert
            Assert.Equal(auditUser, entity.CreatedBy);
            Assert.Equal(auditUser, entity.LastUpdatedBy);
        }

        [Fact]
        public void SetCreationAudit_WithEmptyString_SetsEmptyString()
        {
            // Arrange
            var entity = new TestAuditableEntity();

            // Act
            entity.SetCreationAudit(string.Empty);

            // Assert
            Assert.Equal(string.Empty, entity.CreatedBy);
            Assert.Equal(string.Empty, entity.LastUpdatedBy);
            Assert.NotNull(entity.CreatedOn);
            Assert.NotNull(entity.LastUpdatedOn);
        }

        #endregion

        #region SetUpdateAudit Tests

        [Fact]
        public void SetUpdateAudit_WithValidUser_SetsOnlyUpdateProperties()
        {
            // Arrange
            var entity = new TestAuditableEntity
            {
                CreatedBy = "original-creator",
                CreatedOn = DateTime.UtcNow.AddDays(-7)
            };
            var originalCreatedBy = entity.CreatedBy;
            var originalCreatedOn = entity.CreatedOn;
            var auditUser = "updater-user";
            var beforeCall = DateTime.UtcNow;

            // Act
            entity.SetUpdateAudit(auditUser);
            var afterCall = DateTime.UtcNow;

            // Assert
            // CreatedBy and CreatedOn should remain unchanged
            Assert.Equal(originalCreatedBy, entity.CreatedBy);
            Assert.Equal(originalCreatedOn, entity.CreatedOn);
            
            // LastUpdatedBy and LastUpdatedOn should be updated
            Assert.Equal(auditUser, entity.LastUpdatedBy);
            Assert.NotNull(entity.LastUpdatedOn);
            Assert.True(entity.LastUpdatedOn >= beforeCall && entity.LastUpdatedOn <= afterCall);
        }

        [Fact]
        public void SetUpdateAudit_DoesNotModifyCreationProperties()
        {
            // Arrange
            var originalCreatedBy = "creator";
            var originalCreatedOn = DateTime.UtcNow.AddDays(-10);
            var entity = new TestAuditableEntity
            {
                CreatedBy = originalCreatedBy,
                CreatedOn = originalCreatedOn
            };

            // Act
            entity.SetUpdateAudit("updater");

            // Assert
            Assert.Equal(originalCreatedBy, entity.CreatedBy);
            Assert.Equal(originalCreatedOn, entity.CreatedOn);
        }

        [Fact]
        public void SetUpdateAudit_SetsTimestampToUtcNow()
        {
            // Arrange
            var entity = new TestAuditableEntity();
            var beforeCall = DateTime.UtcNow;

            // Act
            entity.SetUpdateAudit("test-user");
            var afterCall = DateTime.UtcNow;

            // Assert
            Assert.NotNull(entity.LastUpdatedOn);
            Assert.InRange(entity.LastUpdatedOn.Value, beforeCall, afterCall);
            Assert.Equal(DateTimeKind.Utc, entity.LastUpdatedOn.Value.Kind);
        }

        [Fact]
        public void SetUpdateAudit_OverwritesExistingLastUpdatedValues()
        {
            // Arrange
            var entity = new TestAuditableEntity
            {
                CreatedBy = "creator",
                CreatedOn = DateTime.UtcNow.AddDays(-5),
                LastUpdatedBy = "old-updater",
                LastUpdatedOn = DateTime.UtcNow.AddHours(-2)
            };
            var oldLastUpdatedOn = entity.LastUpdatedOn;
            var newUser = "new-updater";

            // Act
            System.Threading.Thread.Sleep(10); // Ensure time difference
            entity.SetUpdateAudit(newUser);

            // Assert
            Assert.Equal(newUser, entity.LastUpdatedBy);
            Assert.True(entity.LastUpdatedOn > oldLastUpdatedOn);
        }

        [Theory]
        [InlineData("updater@example.com")]
        [InlineData("system-service")]
        [InlineData("automated-process")]
        [InlineData("Jane Smith")]
        public void SetUpdateAudit_WithDifferentUserFormats_SetsCorrectly(string auditUser)
        {
            // Arrange
            var entity = new TestAuditableEntity
            {
                CreatedBy = "creator",
                CreatedOn = DateTime.UtcNow.AddDays(-1)
            };

            // Act
            entity.SetUpdateAudit(auditUser);

            // Assert
            Assert.Equal(auditUser, entity.LastUpdatedBy);
            Assert.Equal("creator", entity.CreatedBy); // Unchanged
        }

        [Fact]
        public void SetUpdateAudit_WithEmptyString_SetsEmptyString()
        {
            // Arrange
            var entity = new TestAuditableEntity
            {
                CreatedBy = "creator",
                CreatedOn = DateTime.UtcNow.AddDays(-1)
            };

            // Act
            entity.SetUpdateAudit(string.Empty);

            // Assert
            Assert.Equal(string.Empty, entity.LastUpdatedBy);
            Assert.NotNull(entity.LastUpdatedOn);
            Assert.Equal("creator", entity.CreatedBy); // Unchanged
        }

        [Fact]
        public void SetUpdateAudit_CanBeCalledMultipleTimes()
        {
            // Arrange
            var entity = new TestAuditableEntity
            {
                CreatedBy = "creator",
                CreatedOn = DateTime.UtcNow.AddDays(-1)
            };

            // Act - Multiple updates
            entity.SetUpdateAudit("updater-1");
            var firstUpdate = entity.LastUpdatedOn;
            
            System.Threading.Thread.Sleep(10);
            
            entity.SetUpdateAudit("updater-2");
            var secondUpdate = entity.LastUpdatedOn;
            
            System.Threading.Thread.Sleep(10);
            
            entity.SetUpdateAudit("updater-3");
            var thirdUpdate = entity.LastUpdatedOn;

            // Assert
            Assert.Equal("updater-3", entity.LastUpdatedBy);
            Assert.True(secondUpdate > firstUpdate);
            Assert.True(thirdUpdate > secondUpdate);
            Assert.Equal("creator", entity.CreatedBy); // Never changed
        }

        #endregion

        #region SetAudit Tests

        [Fact]
        public void SetAudit_WithNewEntity_CallsSetCreationAudit()
        {
            // Arrange
            var entity = new TestAuditableEntity(); // CreatedOn is null
            var auditUser = "test-user";
            var beforeCall = DateTime.UtcNow;

            // Act
            entity.SetAudit(auditUser);
            var afterCall = DateTime.UtcNow;

            // Assert
            Assert.Equal(auditUser, entity.CreatedBy);
            Assert.Equal(auditUser, entity.LastUpdatedBy);
            Assert.NotNull(entity.CreatedOn);
            Assert.NotNull(entity.LastUpdatedOn);
            Assert.InRange(entity.CreatedOn.Value, beforeCall, afterCall);
        }

        [Fact]
        public void SetAudit_WithExistingEntity_CallsSetUpdateAudit()
        {
            // Arrange
            var originalCreatedBy = "original-creator";
            var originalCreatedOn = DateTime.UtcNow.AddDays(-7);
            var entity = new TestAuditableEntity
            {
                CreatedBy = originalCreatedBy,
                CreatedOn = originalCreatedOn,
                LastUpdatedBy = "old-updater",
                LastUpdatedOn = DateTime.UtcNow.AddHours(-1)
            };
            var auditUser = "new-updater";

            // Act
            entity.SetAudit(auditUser);

            // Assert
            // Creation properties should remain unchanged
            Assert.Equal(originalCreatedBy, entity.CreatedBy);
            Assert.Equal(originalCreatedOn, entity.CreatedOn);
            
            // Update properties should be changed
            Assert.Equal(auditUser, entity.LastUpdatedBy);
            Assert.True(entity.LastUpdatedOn > originalCreatedOn);
        }

        [Fact]
        public void SetAudit_WithNullCreatedOn_TreatsAsNewEntity()
        {
            // Arrange
            var entity = new TestAuditableEntity
            {
                CreatedOn = null
            };
            var auditUser = "test-user";

            // Act
            entity.SetAudit(auditUser);

            // Assert
            Assert.Equal(auditUser, entity.CreatedBy);
            Assert.Equal(auditUser, entity.LastUpdatedBy);
            Assert.NotNull(entity.CreatedOn);
            Assert.NotNull(entity.LastUpdatedOn);
            Assert.Equal(entity.CreatedOn, entity.LastUpdatedOn);
        }

        [Fact]
        public void SetAudit_WithDefaultCreatedOn_TreatsAsNewEntity()
        {
            // Arrange
            var entity = new TestAuditableEntity
            {
                CreatedOn = default(DateTime?)
            };
            var auditUser = "test-user";

            // Act
            entity.SetAudit(auditUser);

            // Assert
            Assert.Equal(auditUser, entity.CreatedBy);
            Assert.Equal(auditUser, entity.LastUpdatedBy);
            Assert.NotNull(entity.CreatedOn);
            Assert.True(entity.CreatedOn > default(DateTime));
        }

        [Fact]
        public void SetAudit_WithValidCreatedOn_TreatsAsExistingEntity()
        {
            // Arrange
            var originalCreatedBy = "creator";
            var originalCreatedOn = DateTime.UtcNow.AddDays(-1);
            var entity = new TestAuditableEntity
            {
                CreatedBy = originalCreatedBy,
                CreatedOn = originalCreatedOn
            };
            var auditUser = "updater";

            // Act
            entity.SetAudit(auditUser);

            // Assert
            Assert.Equal(originalCreatedBy, entity.CreatedBy);
            Assert.Equal(originalCreatedOn, entity.CreatedOn);
            Assert.Equal(auditUser, entity.LastUpdatedBy);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("creator")]
        public void SetAudit_WithNullCreatedOn_AlwaysCreatesNew(string? existingCreatedBy)
        {
            // Arrange
            var entity = new TestAuditableEntity
            {
                CreatedBy = existingCreatedBy,
                CreatedOn = null
            };
            var auditUser = "new-user";

            // Act
            entity.SetAudit(auditUser);

            // Assert
            Assert.Equal(auditUser, entity.CreatedBy);
            Assert.Equal(auditUser, entity.LastUpdatedBy);
            Assert.NotNull(entity.CreatedOn);
        }

        [Fact]
        public void SetAudit_DeterminesCreateVsUpdate_BasedOnCreatedOnProperty()
        {
            // Arrange
            var newEntity = new TestAuditableEntity();
            var existingEntity = new TestAuditableEntity
            {
                CreatedBy = "creator",
                CreatedOn = DateTime.UtcNow.AddDays(-1)
            };

            // Act
            newEntity.SetAudit("user1");
            existingEntity.SetAudit("user2");

            // Assert
            // New entity: both properties set to same user
            Assert.Equal("user1", newEntity.CreatedBy);
            Assert.Equal("user1", newEntity.LastUpdatedBy);
            
            // Existing entity: CreatedBy preserved, only LastUpdatedBy changed
            Assert.Equal("creator", existingEntity.CreatedBy);
            Assert.Equal("user2", existingEntity.LastUpdatedBy);
        }

        #endregion

        #region Integration and Scenario Tests

        [Fact]
        public void AuditExtensions_CreateThenUpdateScenario_WorksCorrectly()
        {
            // Arrange
            var entity = new TestAuditableEntity();
            var creator = "creator@example.com";
            var updater1 = "updater1@example.com";
            var updater2 = "updater2@example.com";

            // Act - Create
            entity.SetCreationAudit(creator);
            var creationTime = entity.CreatedOn;

            System.Threading.Thread.Sleep(10);

            // Act - First Update
            entity.SetUpdateAudit(updater1);
            var firstUpdateTime = entity.LastUpdatedOn;

            System.Threading.Thread.Sleep(10);

            // Act - Second Update
            entity.SetUpdateAudit(updater2);
            var secondUpdateTime = entity.LastUpdatedOn;

            // Assert
            Assert.Equal(creator, entity.CreatedBy);
            Assert.Equal(creationTime, entity.CreatedOn);
            Assert.Equal(updater2, entity.LastUpdatedBy);
            Assert.True(firstUpdateTime > creationTime);
            Assert.True(secondUpdateTime > firstUpdateTime);
        }

        [Fact]
        public void AuditExtensions_UsingSetAudit_HandlesCreateAndUpdateScenarios()
        {
            // Arrange
            var entity = new TestAuditableEntity();

            // Act & Assert - First call (creation)
            entity.SetAudit("user1");
            Assert.Equal("user1", entity.CreatedBy);
            Assert.Equal("user1", entity.LastUpdatedBy);
            var creationTime = entity.CreatedOn;

            System.Threading.Thread.Sleep(10);

            // Act & Assert - Second call (update)
            entity.SetAudit("user2");
            Assert.Equal("user1", entity.CreatedBy); // Unchanged
            Assert.Equal(creationTime, entity.CreatedOn); // Unchanged
            Assert.Equal("user2", entity.LastUpdatedBy); // Changed
            Assert.True(entity.LastUpdatedOn > creationTime);
        }

        [Fact]
        public void AuditExtensions_MultipleUpdaters_PreservesOriginalCreator()
        {
            // Arrange
            var entity = new TestAuditableEntity();
            var creator = "original-creator";

            // Act
            entity.SetCreationAudit(creator);
            
            // Multiple updates by different users
            entity.SetUpdateAudit("updater1");
            entity.SetUpdateAudit("updater2");
            entity.SetUpdateAudit("updater3");
            entity.SetUpdateAudit("final-updater");

            // Assert
            Assert.Equal(creator, entity.CreatedBy);
            Assert.Equal("final-updater", entity.LastUpdatedBy);
        }

        [Fact]
        public void AuditExtensions_AllMethods_SetUtcTimestamps()
        {
            // Arrange
            var entity1 = new TestAuditableEntity();
            var entity2 = new TestAuditableEntity();
            var entity3 = new TestAuditableEntity { CreatedOn = DateTime.UtcNow.AddDays(-1) };

            // Act
            entity1.SetCreationAudit("user");
            entity2.SetUpdateAudit("user");
            entity3.SetAudit("user");

            // Assert
            Assert.Equal(DateTimeKind.Utc, entity1.CreatedOn!.Value.Kind);
            Assert.Equal(DateTimeKind.Utc, entity1.LastUpdatedOn!.Value.Kind);
            Assert.Equal(DateTimeKind.Utc, entity2.LastUpdatedOn!.Value.Kind);
            Assert.Equal(DateTimeKind.Utc, entity3.LastUpdatedOn!.Value.Kind);
        }

        [Fact]
        public void AuditExtensions_CanChainWithOtherOperations()
        {
            // Arrange
            var entity = new TestAuditableEntity();

            // Act - Fluent-style chaining (not actually fluent, but testing consecutive calls)
            entity.SetCreationAudit("creator");
            var id = Guid.NewGuid(); // Simulating other operations
            System.Threading.Thread.Sleep(10);
            entity.SetUpdateAudit("updater");

            // Assert
            Assert.Equal("creator", entity.CreatedBy);
            Assert.Equal("updater", entity.LastUpdatedBy);
            Assert.NotNull(entity.CreatedOn);
            Assert.NotNull(entity.LastUpdatedOn);
        }

        [Fact]
        public void AuditExtensions_TimestampsAreMonotonicallyIncreasing()
        {
            // Arrange
            var entity = new TestAuditableEntity();

            // Act
            entity.SetCreationAudit("creator");
            var time1 = entity.LastUpdatedOn!.Value;

            System.Threading.Thread.Sleep(10);
            entity.SetUpdateAudit("updater1");
            var time2 = entity.LastUpdatedOn!.Value;

            System.Threading.Thread.Sleep(10);
            entity.SetUpdateAudit("updater2");
            var time3 = entity.LastUpdatedOn!.Value;

            // Assert
            Assert.True(time2 > time1);
            Assert.True(time3 > time2);
            Assert.True(time3 > time1);
        }

        #endregion

        #region Edge Cases and Validation Tests

        [Fact]
        public void SetCreationAudit_WithNullUser_SetsNullValues()
        {
            // Arrange
            var entity = new TestAuditableEntity();

            // Act
            entity.SetCreationAudit(null!);

            // Assert
            Assert.Null(entity.CreatedBy);
            Assert.Null(entity.LastUpdatedBy);
            Assert.NotNull(entity.CreatedOn);
            Assert.NotNull(entity.LastUpdatedOn);
        }

        [Fact]
        public void SetUpdateAudit_WithNullUser_SetsNullValue()
        {
            // Arrange
            var entity = new TestAuditableEntity
            {
                CreatedBy = "creator",
                CreatedOn = DateTime.UtcNow.AddDays(-1)
            };

            // Act
            entity.SetUpdateAudit(null!);

            // Assert
            Assert.Null(entity.LastUpdatedBy);
            Assert.NotNull(entity.LastUpdatedOn);
            Assert.Equal("creator", entity.CreatedBy); // Unchanged
        }

        [Fact]
        public void SetAudit_WithNullUser_HandlesAppropriately()
        {
            // Arrange
            var newEntity = new TestAuditableEntity();
            var existingEntity = new TestAuditableEntity
            {
                CreatedBy = "creator",
                CreatedOn = DateTime.UtcNow.AddDays(-1)
            };

            // Act
            newEntity.SetAudit(null!);
            existingEntity.SetAudit(null!);

            // Assert
            Assert.Null(newEntity.CreatedBy);
            Assert.Null(newEntity.LastUpdatedBy);
            Assert.Equal("creator", existingEntity.CreatedBy);
            Assert.Null(existingEntity.LastUpdatedBy);
        }

        [Fact]
        public void AuditExtensions_WithVeryLongUserNames_StoresCorrectly()
        {
            // Arrange
            var entity = new TestAuditableEntity();
            var longUserName = new string('a', 500);

            // Act
            entity.SetCreationAudit(longUserName);

            // Assert
            Assert.Equal(longUserName, entity.CreatedBy);
            Assert.Equal(longUserName, entity.LastUpdatedBy);
        }

        [Theory]
        [InlineData("user@domain.com")]
        [InlineData("SYSTEM")]
        [InlineData("Service-Account-123")]
        [InlineData("user with spaces")]
        [InlineData("user-with-special-chars!@#$")]
        public void AuditExtensions_WithVariousUserNameFormats_HandlesCorrectly(string userName)
        {
            // Arrange
            var entity = new TestAuditableEntity();

            // Act
            entity.SetCreationAudit(userName);
            entity.SetUpdateAudit(userName + "-update");

            // Assert
            Assert.Equal(userName, entity.CreatedBy);
            Assert.Equal(userName + "-update", entity.LastUpdatedBy);
        }

        #endregion
    }
}
