using NUnit.Framework;
using Unity.PerformanceTesting;

namespace Unity.AI.Planner.Tests.Performance
{
    static class PerformanceUtility
    {
        public static void AssertRange(double min, double max)
        {
            PerformanceTest.Active.CalculateStatisticalValues();
            foreach (var sampleGroup in PerformanceTest.Active.SampleGroups)
            {
                Assert.GreaterOrEqual(sampleGroup.Median, min);
                Assert.LessOrEqual(sampleGroup.Median, max);
            }
        }
    }
}
