using SxgEvalPlatformApi.Models;

namespace Sxg.EvalPlatform.API.UnitTests.Helpers.Builders
{
    /// <summary>
    /// Builder for creating EvalDataset test objects with fluent API.
    /// </summary>
    public class EvalDatasetBuilder
    {
        private string _query = "What is AI?";
        private string _groundTruth = "Artificial Intelligence";
        private string _actualResponse = "AI is Artificial Intelligence";
        private string _context = "Technology context";
        private string? _conversationId;
        private int? _turnIndex;
        private string? _copilotConversationId;

        /// <summary>
        /// Sets the query field.
        /// </summary>
        public EvalDatasetBuilder WithQuery(string query)
        {
            _query = query;
            return this;
        }

        /// <summary>
        /// Sets the ground truth field.
        /// </summary>
        public EvalDatasetBuilder WithGroundTruth(string groundTruth)
        {
            _groundTruth = groundTruth;
            return this;
        }

        /// <summary>
        /// Sets the actual response field.
        /// </summary>
        public EvalDatasetBuilder WithActualResponse(string actualResponse)
        {
            _actualResponse = actualResponse;
            return this;
        }

        /// <summary>
        /// Sets the context field.
        /// </summary>
        public EvalDatasetBuilder WithContext(string context)
        {
            _context = context;
            return this;
        }

        /// <summary>
        /// Sets the conversation ID field.
        /// </summary>
        public EvalDatasetBuilder WithConversationId(string conversationId)
        {
            _conversationId = conversationId;
            return this;
        }

        /// <summary>
        /// Sets the turn index field.
        /// </summary>
        public EvalDatasetBuilder WithTurnIndex(int turnIndex)
        {
            _turnIndex = turnIndex;
            return this;
        }

        /// <summary>
        /// Sets the copilot conversation ID field.
        /// </summary>
        public EvalDatasetBuilder WithCopilotConversationId(string copilotConversationId)
        {
            _copilotConversationId = copilotConversationId;
            return this;
        }

        /// <summary>
        /// Builds the EvalDataset object.
        /// </summary>
        public EvalDataset Build()
        {
            return new EvalDataset
            {
                Query = _query,
                GroundTruth = _groundTruth,
                ActualResponse = _actualResponse,
                Context = _context,
                ConversationId = _conversationId,
                TurnIndex = _turnIndex,
                CopilotConversationId = _copilotConversationId
            };
        }

        /// <summary>
        /// Creates a default test EvalDataset.
        /// </summary>
        public static EvalDataset CreateDefault() => new EvalDatasetBuilder().Build();

        /// <summary>
        /// Creates a minimal valid EvalDataset.
        /// </summary>
        public static EvalDataset CreateMinimal() => new EvalDatasetBuilder()
            .WithQuery("Test query")
            .WithGroundTruth("Test ground truth")
            .WithActualResponse("")
            .WithContext("")
            .Build();
    }
}
