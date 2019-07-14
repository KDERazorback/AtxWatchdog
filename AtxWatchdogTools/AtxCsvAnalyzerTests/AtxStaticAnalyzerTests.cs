using Microsoft.VisualStudio.TestTools.UnitTesting;
using AtxCsvAnalyzer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AtxCsvAnalyzer.Tests
{
    [TestClass()]
    public class AtxStaticAnalyzerTests
    {
        [TestMethod()]
        public void SearchPeaksTest()
        {
            AtxStaticAnalyzer analyzer = new AtxStaticAnalyzer();

            float[] testPoints = {0.0f, 0.0f, 0.0f, 5.0f, 5.0f, 7.0f, 5.0f, 0.0f, 0.0f, 0.0f};
            long[] expectedPeaks = { 5 };
            long[] expectedEdges = {2, 3, 4, 5, 6, 7};
            long[] edges;

            int sign;

            long[] results = analyzer.SearchPeaksEdges(testPoints, out sign, out edges);

            Assert.AreEqual(expectedPeaks.Length, results.Length);
            Assert.AreEqual(expectedEdges.Length, edges.Length);
            for (int i = 0; i < results.Length; i++)
                Assert.AreEqual(expectedPeaks[i], results[i]);
            for (int i = 0; i < edges.Length; i++)
                Assert.AreEqual(expectedEdges[i], edges[i]);
        }
    }
}