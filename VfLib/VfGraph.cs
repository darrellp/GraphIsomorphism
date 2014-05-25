using System;
using System.Collections.Generic;
using System.Linq;
#if NUNIT
using NUnit.Framework;
#endif

namespace vflibcs
{
	class VfGraph
	{
		#region Private Variables
		readonly VfnNode[] _arNodes;
		#endregion

		#region Properties
		internal int NodeCount
		{
			get
			{
				return _arNodes.Length;
			}
		}
		#endregion

		#region Accessors
		internal int OutDegree(int inod)
		{
			return _arNodes[inod].OutDegree;
		}

		internal int InDegree(int inod)
		{
			return _arNodes[inod].InDegree;
		}

		internal int TotalDegree(int inod)
		{
			return OutDegree(inod) + InDegree(inod);
		}

		internal List<int> OutNeighbors(int inod)
		{
			return _arNodes[inod].OutNeighbors;
		}

		internal List<int> InNeighbors(int inod)
		{
			return _arNodes[inod].InNeighbors;
		}

		internal Group GetGroup(int inod)
		{
			return _arNodes[inod].Grps;
		}

		internal void SetGroup(int inod, Group grps)
		{
			_arNodes[inod].Grps = grps;
		}

		internal object GetAttr(int inod)
		{
			return _arNodes[inod].Attr;
		}
		#endregion

		#region Constructor
		internal static int[] ReversePermutation(int[] perm)
		{
			return Enumerable.Range(0, perm.Length).Select(i => Array.IndexOf(perm, i)).ToArray();
		}

		internal VfGraph(IGraphLoader loader, int[] mpInodVfInodGraph = null)
		{
			if (mpInodVfInodGraph == null)
			{
				mpInodVfInodGraph = (new CmpNodeDegrees(loader)).Permutation;
			}
			_arNodes = new VfnNode[loader.NodeCount];
			var mpInodGraphInodVf = ReversePermutation(mpInodVfInodGraph);
			var dctEdge = new Dictionary<VfeNode, VfeNode>();

			for (var inodVf = 0; inodVf < loader.NodeCount; inodVf++)
			{
				_arNodes[inodVf] = new VfnNode(loader, mpInodVfInodGraph[inodVf], dctEdge, mpInodGraphInodVf);
			}
		}
		#endregion

		#region NUNIT Testing
#if NUNIT
		[TestFixture]
		public class VfGraphTester
		{
			VfGraph SetupGraph()
			{
				var graph = new Graph();
				Assert.AreEqual(0, graph.InsertNode());
				Assert.AreEqual(1, graph.InsertNode());
				Assert.AreEqual(2, graph.InsertNode());
				Assert.AreEqual(3, graph.InsertNode());
				Assert.AreEqual(4, graph.InsertNode());
				Assert.AreEqual(5, graph.InsertNode());
				graph.InsertEdge(0, 1);
				graph.InsertEdge(1, 2);
				graph.InsertEdge(2, 3);
				graph.InsertEdge(3, 4);
				graph.InsertEdge(4, 5);
				graph.InsertEdge(5, 0);
				graph.DeleteNode(0);
				graph.DeleteNode(1);
				graph.InsertEdge(5, 2);
				graph.InsertEdge(2, 4);

				return new VfGraph(graph);
			}

			[Test]
			public void TestPermutations()
			{
				var graph = new Graph();
				Assert.AreEqual(0, graph.InsertNode());
				Assert.AreEqual(1, graph.InsertNode());
				Assert.AreEqual(2, graph.InsertNode());
				graph.InsertEdge(1, 0);
				graph.InsertEdge(1, 2);
				var vfg = new VfGraph(graph);
				var arOut = new int[vfg._arNodes[0].OutNeighbors.Count];
				vfg._arNodes[0].OutNeighbors.CopyTo(arOut, 0);
				var inodNeighbor1 = arOut[0];
				var inodNeighbor2 = arOut[1];
				Assert.IsTrue(inodNeighbor1 == 1 && inodNeighbor2 == 2 || inodNeighbor1 == 2 && inodNeighbor2 == 1);
			}

			[Test]
			public void TestConstructor()
			{
				Assert.IsNotNull(SetupGraph());
			}

			[Test]
			public void TestNodeCount()
			{
				Assert.AreEqual(4, SetupGraph().NodeCount);
			}
		}
#endif
		#endregion
	}
}
