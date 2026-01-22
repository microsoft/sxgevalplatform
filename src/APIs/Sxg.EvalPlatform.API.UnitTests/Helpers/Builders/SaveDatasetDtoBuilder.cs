using Sxg.EvalPlatform.API.UnitTests.RequestHandlerTests;
using SxgEvalPlatformApi.Models;

namespace Sxg.EvalPlatform.API.UnitTests.Helpers.Builders
{
    /// <summary>
    /// Builder for creating SaveDatasetDto test objects with fluent API.
    /// </summary>
    public class SaveDatasetDtoBuilder
    {
        private string _agentId = TestConstants.Agents.DefaultAgentId;
        private string _datasetType = TestConstants.Datasets.GoldenType;
        private string _datasetName = TestConstants.Datasets.DefaultName;
        private List<EvalDataset> _datasetRecords = new()
        {
            EvalDatasetBuilder.CreateDefault()
        };

        /// <summary>
        /// Sets the agent ID.
        /// </summary>
        public SaveDatasetDtoBuilder WithAgentId(string agentId)
        {
            _agentId = agentId;
            return this;
        }

        /// <summary>
        /// Sets the dataset type.
        /// </summary>
        public SaveDatasetDtoBuilder WithDatasetType(string datasetType)
        {
            _datasetType = datasetType;
            return this;
        }

        /// <summary>
        /// Sets the dataset name.
        /// </summary>
        public SaveDatasetDtoBuilder WithDatasetName(string datasetName)
        {
            _datasetName = datasetName;
            return this;
        }

        /// <summary>
        /// Sets the dataset records.
        /// </summary>
        public SaveDatasetDtoBuilder WithDatasetRecords(List<EvalDataset> datasetRecords)
        {
            _datasetRecords = datasetRecords;
            return this;
        }

        /// <summary>
        /// Adds a single dataset record.
        /// </summary>
        public SaveDatasetDtoBuilder AddDatasetRecord(EvalDataset record)
        {
            _datasetRecords.Add(record);
            return this;
        }

        /// <summary>
        /// Sets empty dataset records (for validation testing).
        /// </summary>
        public SaveDatasetDtoBuilder WithEmptyRecords()
        {
            _datasetRecords = new List<EvalDataset>();
            return this;
        }

        /// <summary>
        /// Builds the SaveDatasetDto object.
        /// </summary>
        public SaveDatasetDto Build()
        {
            return new SaveDatasetDto
            {
                AgentId = _agentId,
                DatasetType = _datasetType,
                DatasetName = _datasetName,
                DatasetRecords = _datasetRecords
            };
        }

        /// <summary>
        /// Creates a default valid SaveDatasetDto for testing.
        /// </summary>
        public static SaveDatasetDto CreateDefault() => new SaveDatasetDtoBuilder().Build();

        /// <summary>
        /// Creates a SaveDatasetDto with minimal valid data.
        /// </summary>
        public static SaveDatasetDto CreateMinimal() => new SaveDatasetDtoBuilder()
            .WithDatasetName("Minimal Dataset")
            .WithDatasetRecords(new List<EvalDataset> { EvalDatasetBuilder.CreateMinimal() })
            .Build();

        /// <summary>
        /// Creates a SaveDatasetDto with multiple records.
        /// </summary>
        public static SaveDatasetDto CreateWithMultipleRecords(int count)
        {
            var builder = new SaveDatasetDtoBuilder();
            builder.WithEmptyRecords();

            for (int i = 0; i < count; i++)
            {
                builder.AddDatasetRecord(new EvalDatasetBuilder()
                    .WithQuery($"{TestConstants.TestData.Query1} {i}")
                    .WithGroundTruth($"{TestConstants.TestData.GroundTruth1} {i}")
                    .Build());
            }

            return builder.Build();
        }
    }
}
