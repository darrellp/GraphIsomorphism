using System;
using System.Collections.Generic;
using System.Linq;
#if NUNIT
using NUnit.Framework;
#endif

namespace vflibcs
{
	class VfGraph<TVAttr, TEAttr>
		where TVAttr : class 
		where TEAttr : class
	{
		#region Private Variables
		readonly VfVertex<TVAttr, TEAttr>[] _arNodes;
		#endregion

		#region Properties
		internal int VertexCount
		{
			get
			{
				return _arNodes.Length;
			}
		}
		#endregion

		#region Accessors
		internal int OutDegree(int ivtx)
		{
			return _arNodes[ivtx].OutDegree;
		}

		internal int InDegree(int ivtx)
		{
			return _arNodes[ivtx].InDegree;
		}

		internal int TotalDegree(int ivtx)
		{
			return OutDegree(ivtx) + InDegree(ivtx);
		}

		internal List<int> OutNeighbors(int ivtx)
		{
			return _arNodes[ivtx].OutNeighbors;
		}

		internal List<int> InNeighbors(int ivtx)
		{
			return _arNodes[ivtx].InNeighbors;
		}

		internal Group GetGroup(int ivtx)
		{
			return _arNodes[ivtx].Grps;
		}

		internal void SetGroup(int ivtx, Group grps)
		{
			_arNodes[ivtx].Grps = grps;
		}

		internal object GetAttr(int ivtx)
		{
			return _arNodes[ivtx].Attr;
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

		internal VfGraph(IGraphLoader<TVAttr, TEAttr> loader, List<int> mpIvtxVfIvtxGraph = null)
		{
			if (mpIvtxVfIvtxGraph == null)
			{
				mpIvtxVfIvtxGraph = (new CmpVertexDegrees<TVAttr, TEAttr>(loader)).Permutation;
			}
			_arNodes = new VfVertex<TVAttr, TEAttr>[loader.VertexCount];
			var mpIvtxGraphIvtxVf = ReversePermutation(mpIvtxVfIvtxGraph);
			var dctEdge = new Dictionary<VfEdge, VfEdge>();

			for (var ivtxVf = 0; ivtxVf < loader.VertexCount; ivtxVf++)
			{
				_arNodes[ivtxVf] = new VfVertex<TVAttr, TEAttr>(loader, mpIvtxVfIvtxGraph[ivtxVf], dctEdge, mpIvtxGraphIvtxVf);
			}
		}
		#endregion
	}

	class VfGraph : VfGraph<Object, Object>
	{
		internal VfGraph(IGraphLoader<Object, Object> loader, List<int> mpIvtxVfIvtxGraph = null) : base(loader, mpIvtxVfIvtxGraph) {}
	}
#if NUNIT
	[TestFixture]
	public class VfGraphTester
	{
		#region NUNIT Testing
		VfGraph SetupGraph()
		{
			var graph = new Graph();
			Assert.AreEqual(0, graph.InsertVertex());
			Assert.AreEqual(1, graph.InsertVertex());
			Assert.AreEqual(2, graph.InsertVertex());
			Assert.AreEqual(3, graph.InsertVertex());
			Assert.AreEqual(4, graph.InsertVertex());
			Assert.AreEqual(5, graph.InsertVertex());
			graph.AddEdge(0, 1);
			graph.AddEdge(1, 2);
			graph.AddEdge(2, 3);
			graph.AddEdge(3, 4);
			graph.AddEdge(4, 5);
			graph.AddEdge(5, 0);
			graph.DeleteVertex(0);
			graph.DeleteVertex(1);
			graph.AddEdge(5, 2);
			graph.AddEdge(2, 4);

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
			Assert.AreEqual(4, SetupGraph().VertexCount);
		}
		#endregion
	}
#endif
}
