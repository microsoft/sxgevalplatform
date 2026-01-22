using Sxg.EvalPlatform.API.UnitTests.RequestHandlerTests;
using SxgEvalPlatformApi.Models;

namespace Sxg.EvalPlatform.API.UnitTests.Helpers.Builders
{
    /// <summary>
    /// Builder for creating DatasetMetadataDto test objects with fluent API.
    /// </summary>
    public class DatasetMetadataDtoBuilder
    {
        private string _datasetId = Guid.NewGuid().ToString();
        private string _agentId = TestConstants.Agents.DefaultAgentId;
        private string _datasetType = TestConstants.Datasets.GoldenType;
        private string _datasetName = TestConstants.Datasets.DefaultName;
        private int _recordCount = 10;
        private string _createdBy = TestConstants.Users.DefaultEmail;
        private DateTime _createdOn = DateTime.UtcNow;
        private string _lastUpdatedBy = TestConstants.Users.DefaultEmail;
        private DateTime _lastUpdatedOn = DateTime.UtcNow;

        /// <summary>
        /// Sets the dataset ID.
        /// </summary>
        public DatasetMetadataDtoBuilder WithDatasetId(string datasetId)
        {
            _datasetId = datasetId;
            return this;
        }

        /// <summary>
        /// Sets the agent ID.
        /// </summary>
        public DatasetMetadataDtoBuilder WithAgentId(string agentId)
        {
            _agentId = agentId;
            return this;
        }

        /// <summary>
        /// Sets the dataset type.
        /// </summary>
        public DatasetMetadataDtoBuilder WithDatasetType(string datasetType)
        {
            _datasetType = datasetType;
            return this;
        }

        /// <summary>
        /// Sets the dataset name.
        /// </summary>
        public DatasetMetadataDtoBuilder WithDatasetName(string datasetName)
        {
            _datasetName = datasetName;
            return this;
        }

        /// <summary>
        /// Sets the record count.
        /// </summary>
        public DatasetMetadataDtoBuilder WithRecordCount(int recordCount)
        {
            _recordCount = recordCount;
            return this;
        }

        /// <summary>
        /// Sets the created by field.
        /// </summary>
        public DatasetMetadataDtoBuilder WithCreatedBy(string createdBy)
        {
            _createdBy = createdBy;
            return this;
        }

        /// <summary>
        /// Sets the created on timestamp.
        /// </summary>
        public DatasetMetadataDtoBuilder WithCreatedOn(DateTime createdOn)
        {
            _createdOn = createdOn;
            return this;
        }

        /// <summary>
        /// Sets the last updated by field.
        /// </summary>
        public DatasetMetadataDtoBuilder WithLastUpdatedBy(string lastUpdatedBy)
        {
            _lastUpdatedBy = lastUpdatedBy;
            return this;
        }

        /// <summary>
        /// Sets the last updated on timestamp.
        /// </summary>
        public DatasetMetadataDtoBuilder WithLastUpdatedOn(DateTime lastUpdatedOn)
        {
            _lastUpdatedOn = lastUpdatedOn;
            return this;
        }

        /// <summary>
        /// Builds the DatasetMetadataDto object.
        /// </summary>
        public DatasetMetadataDto Build()
        {
            return new DatasetMetadataDto
            {
                DatasetId = _datasetId,
                AgentId = _agentId,
                DatasetType = _datasetType,
                DatasetName = _datasetName,
                RecordCount = _recordCount,
                CreatedBy = _createdBy,
                CreatedOn = _createdOn,
                LastUpdatedBy = _lastUpdatedBy,
                LastUpdatedOn = _lastUpdatedOn
            };
        }

        /// <summary>
        /// Creates a default DatasetMetadataDto for testing.
        /// </summary>
        public static DatasetMetadataDto CreateDefault() => new DatasetMetadataDtoBuilder().Build();

        /// <summary>
        /// Creates a DatasetMetadataDto with specific ID.
        /// </summary>
        public static DatasetMetadataDto CreateWithId(string datasetId) =>
            new DatasetMetadataDtoBuilder()
                .WithDatasetId(datasetId)
                .Build();
    }
}
