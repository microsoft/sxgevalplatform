using Sxg.EvalPlatform.API.UnitTests.RequestHandlerTests;
using SxgEvalPlatformApi.Models;

namespace Sxg.EvalPlatform.API.UnitTests.Helpers.Builders
{
    /// <summary>
    /// Builder for creating UpdateDatasetDto test objects with fluent API.
    /// </summary>
    public class UpdateDatasetDtoBuilder
    {
        private List<EvalDataset> _datasetRecords = new()
        {
            EvalDatasetBuilder.CreateDefault()
        };

        /// <summary>
        /// Sets the dataset records.
        /// </summary>
        public UpdateDatasetDtoBuilder WithDatasetRecords(List<EvalDataset> datasetRecords)
        {
            _datasetRecords = datasetRecords;
            return this;
        }

        /// <summary>
        /// Adds a single dataset record.
        /// </summary>
        public UpdateDatasetDtoBuilder AddDatasetRecord(EvalDataset record)
        {
            _datasetRecords.Add(record);
            return this;
        }

        /// <summary>
        /// Sets empty dataset records (for validation testing).
        /// </summary>
        public UpdateDatasetDtoBuilder WithEmptyRecords()
        {
            _datasetRecords = new List<EvalDataset>();
            return this;
        }

        /// <summary>
        /// Builds the UpdateDatasetDto object.
        /// </summary>
        public UpdateDatasetDto Build()
        {
            return new UpdateDatasetDto
            {
                DatasetRecords = _datasetRecords
            };
        }

        /// <summary>
        /// Creates a default valid UpdateDatasetDto for testing.
        /// </summary>
        public static UpdateDatasetDto CreateDefault() => new UpdateDatasetDtoBuilder().Build();

        /// <summary>
        /// Creates an UpdateDatasetDto with updated data.
        /// </summary>
        public static UpdateDatasetDto CreateWithUpdatedData() => new UpdateDatasetDtoBuilder()
            .WithDatasetRecords(new List<EvalDataset>
            {
                new EvalDatasetBuilder()
                    .WithQuery("Updated Query")
                    .WithGroundTruth("Updated Ground Truth")
                    .WithActualResponse("Updated Response")
                    .Build()
            })
            .Build();
    }
}
