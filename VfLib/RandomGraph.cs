using System;
using System.Collections.Generic;
#if NUNIT
using NUnit.Framework;

#endif

namespace vflibcs
{
#if NUNIT
	internal class RandomGraph
	{
		#region Private variables
		private readonly double _pctEdges;
		private readonly Random _rnd;
		#endregion

		#region Constructor
		// Works best for pctEdges < 0.5
		public RandomGraph(double pctEdges, int iSeed = -1)
		{
			_pctEdges = pctEdges;
			_rnd = iSeed > 0 ? new Random(iSeed) : new Random();
		}
		#endregion

		#region Random Graphs
		private struct EdgeKey
		{
			public readonly int From;
			public readonly int To;

			public EdgeKey(int @from, int to)
			{
				From = @from;
				To = to;
			}
		}

		public Graph GetGraph(int cnod)
		{
			var cEdgesNeeded = (int) (cnod * (cnod - 1) * _pctEdges);
			var cEdgesSoFar = 0;
			var setEdges = new HashSet<EdgeKey>();
			var graph = new Graph();

			graph.InsertNodes(cnod);

			while (cEdgesSoFar < cEdgesNeeded)
			{
				var ekey = new EdgeKey(_rnd.Next(cnod), _rnd.Next(cnod));

				if (setEdges.Contains(ekey) || ekey.From == ekey.To)
				{
					continue;
				}
				graph.InsertEdge(ekey.From, ekey.To);
				setEdges.Add(ekey);
				cEdgesSoFar++;
			}
			return graph;
		}

		public void IsomorphicPair(int cnod, out Graph graph1, out Graph graph2)
		{
			graph1 = GetGraph(cnod);
			graph2 = graph1.IsomorphicShuffling(_rnd);
		}
		#endregion

		#region NUNIT Testing
#if NUNIT
		[TestFixture]
		public class GraphTester
		{
			[Test]
			public void TestConstructor()
			{
				var rg = new RandomGraph(0.2, 20);
				Assert.IsNotNull(rg);
			}

			[Test]
			public void TestGetGraph()
			{
				var rg = new RandomGraph(0.3);

				var graph = rg.GetGraph(30);
				Assert.IsNotNull(graph);
				Assert.AreEqual(30, graph.NodeCount);
			}
		}
#endif
		#endregion
	}
#endif
}