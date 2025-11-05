using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SXG.EvalPlatform.Common
{
    public class CommonConstants
    {
        public class EvalRunStatus
        {
            public const string RequestSubmitted = "RequestSubmitted";
            public const string EnrichingDataset = "EnrichingDataset";
            public const string DatasetEnrichmentCompleted = "DatasetEnrichmentCompleted";
            public const string EvalRunStarted = "EvalRunStarted";
            public const string EvalRunCompleted = "EvalRunCompleted";
            public const string EvalRunFailed = "EvalRunFailed";
        }
    }
}
