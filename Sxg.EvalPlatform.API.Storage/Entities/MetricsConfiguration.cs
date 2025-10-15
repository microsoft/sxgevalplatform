using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sxg.EvalPlatform.API.Storage.Entities
{
    public class Category
    {
        public string categoryName { get; set; }
        public string description { get; set; }
        public List<Metric> metrics { get; set; }
    }

    public class Metric
    {
        public string metricName { get; set; }
        public string description { get; set; }
        public double defaultThreshold { get; set; }
        public bool enabled { get; set; }
        public bool isMandatory { get; set; }
    }

    public class MetricsConfiguration
    {
        public string version { get; set; }
        public DateTime lastUpdated { get; set; }
        public List<Category> categories { get; set; }
    }

    

}
