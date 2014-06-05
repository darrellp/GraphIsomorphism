using System;
using System.Collections.Generic;
using System.Linq;
#if NUNIT
using NUnit.Framework;
#endif

namespace vflibcs
{
	class VfGraph<TVAttr, TEAttr>
	{
		#region Private Variables
		readonly VfVertex<TVAttr, TEAttr>[] _arNodes;
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
		internal static List<int> ReversePermutation(List<int> perm)
		{
			var ret = new int[perm.Count];
			for(var i = 0; i < perm.Count; i++)
			{
				ret[perm[i]] = i;
			}
			return ret.ToList();
		}

		internal VfGraph(IGraphLoader<TVAttr, TEAttr> loader, List<int> mpInodVfInodGraph = null)
		{
			if (mpInodVfInodGraph == null)
			{
				mpInodVfInodGraph = (new CmpNodeDegrees<TVAttr, TEAttr>(loader)).Permutation;
			}
			_arNodes = new VfVertex<TVAttr, TEAttr>[loader.NodeCount];
			var mpInodGraphInodVf = ReversePermutation(mpInodVfInodGraph);
			var dctEdge = new Dictionary<VfEdge, VfEdge>();

			for (var inodVf = 0; inodVf < loader.NodeCount; inodVf++)
			{
				_arNodes[inodVf] = new VfVertex<TVAttr, TEAttr>(loader, mpInodVfInodGraph[inodVf], dctEdge, mpInodGraphInodVf);
			}
		}
		#endregion
	}

	class VfGraph : VfGraph<Object, Object>
	{
		internal VfGraph(IGraphLoader<Object, Object> loader, List<int> mpInodVfInodGraph = null) : base(loader, mpInodVfInodGraph) {}
	}
#if NUNIT
	[TestFixture]
	public class VfGraphTester
	{
		#region NUNIT Testing
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
		public void TestConstructor()
		{
			Assert.IsNotNull(SetupGraph());
		}

		[Test]
		public void TestNodeCount()
		{
			Assert.AreEqual(4, SetupGraph().NodeCount);
		}
		#endregion
	}
#endif
}
