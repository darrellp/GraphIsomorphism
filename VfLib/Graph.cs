using System;
using System.Collections.Generic;
using System.Linq;
#if NUNIT
using NUnit.Framework;

#endif

namespace vflibcs
{
	public class Edge<TE, TV>
		where TE : class
		where TV : class
	{
		public TE Attr;
		internal int IDFrom { get; private set; }
		internal int IDTo { get; private set; }
		public Vertex<TV, TE> From { get; private set; }
		public Vertex<TV, TE> To { get; private set; }

		public Edge(int idFrom, int idTo, Graph<TV, TE> graph)
		{
			IDFrom = idFrom;
			IDTo = idTo;
			From = graph.VertexFromPos(idFrom);
			To = graph.VertexFromPos(idTo);
		}
	}

	public class Vertex<TV, TE>
		where TV : class 
		where TE : class
	{
		public TV Attr;
		public readonly SortedList<int, Edge<TE, TV>> EdgesFrom = new SortedList<int, Edge<TE, TV>>(); // Key is named id of "to" vertex
		public readonly List<Edge<TE, TV>> EdgesTo = new List<Edge<TE, TV>>();
		public readonly List<Edge<TE, TV>> EdgesFromList = new List<Edge<TE, TV>>(); 
		public int ID = Graph.VidIllegal;

		public Edge<TE, TV> FindOutEdge(int vidTo)
		{
			try
			{
				return EdgesFromList.FirstOrDefault(e => e.To.ID == vidTo);
			}
			catch (Exception)
			{
				VfException.Error("Inconsistent Data");
			}
			return null;
		}
	}

	public class Graph<TV, TE> : IGraphLoader<TV, TE>
		where TV : class
		where TE : class
	{
		#region Private variables
		internal readonly SortedList<int, Vertex<TV, TE>> VertexList = new SortedList<int, Vertex<TV, TE>>(); // Sorted by vertex id's
		internal const int VidIllegal = -1;
		#endregion

		#region Private structs
		#endregion

		#region Properties
		public int VertexCount
		{
			get { return VertexList.Count; }
		}

		public IEnumerable<Vertex<TV, TE>> Vertices
		{
			get { return VertexList.Values.ToList(); }
		}
		#endregion

		#region Constructors
		public static void Shuffle<T>(T[] arT, Random rnd, out T[] arRet)
		{
			arRet = new T[arT.Length];
			arT.CopyTo(arRet, 0);
			Shuffle(arRet, rnd);
		}

		public static void Shuffle<T>(T[] arT, Random rnd)
		{
			for (var i = 0; i < arT.Length - 1; i++)
			{
				var iSwap = rnd.Next(arT.Length - i) + i;
				var hold = arT[i];
				arT[i] = arT[iSwap];
				arT[iSwap] = hold;
			}
		}

		internal Graph IsomorphicShuffling(Random rnd)
		{
			var graph = new Graph();
			var ariShuffle = new int[VertexCount];

			for (var i = 0; i < VertexCount; i++)
			{
				ariShuffle[i] = i;
			}
			Shuffle(ariShuffle, rnd);

			graph.InsertVertices(VertexCount);

			for (var ivtx = 0; ivtx < VertexCount; ivtx++)
			{
				var ivtxShuffled = ariShuffle[ivtx];
				var vtx = VertexList[ivtxShuffled];

				foreach (var end in vtx.EdgesFromList)
				{
					graph.AddEdge(ariShuffle[PosFromId(end.IDFrom)], ariShuffle[PosFromId(end.IDTo)]);
				}
			}
			return graph;
		}
		#endregion

		#region Accessors
		internal Vertex<TV, TE> VertexFromPos(int ipos)
		{
			return VertexList[ipos];
		}

		internal Vertex<TV, TE> FindVertex(int id)
		{
			var i = VertexList.IndexOfKey(id);
			if (i >= 0)
			{
				return VertexList.Values[i];
			}
			VfException.Error("Inconsistent data");
			return null;
		}

		public int IdFromPos(int ivtx)
		{
			return VertexList.Values[ivtx].ID;
		}

		public int PosFromId(int vid)
		{
			return VertexList.IndexOfKey(vid);
		}

		public TV GetVertexAttr(int id)
		{
			return FindVertex(id).Attr;
		}

		public int InEdgeCount(int id)
		{
			return FindVertex(id).EdgesTo.Count;
		}

		public int OutEdgeCount(int id)
		{
			return FindVertex(id).EdgesFromList.Count;
		}

		public int GetInEdge(int idTo, int pos, out TE attr)
		{
			Edge<TE, TV> end = null;
			try
			{
				end = FindVertex(idTo).EdgesTo[pos];
			}
			catch (Exception)
			{
				VfException.Error("Inconsistent data");
			}

			// ReSharper disable once PossibleNullReferenceException
			attr = end.Attr;
			return end.IDFrom;
		}

		public int GetOutEdge(int idFrom, int pos, out TE attr)
		{
			Edge<TE, TV> end = null;
			try
			{
				end = FindVertex(idFrom).EdgesFrom.Values[pos];
			}
			catch (Exception)
			{
				VfException.Error("Inconsistent data");
			}

			// ReSharper disable once PossibleNullReferenceException
			attr = end.Attr;
			return end.IDTo;
		}
		#endregion

		#region Insertion/Deletion
		public int InsertVertex(TV attr = null)
		{
			var vtx = new Vertex<TV, TE> {ID = VertexList.Count, Attr = attr};
			VertexList.Add(vtx.ID, vtx);
			return vtx.ID;
		}

		// ReSharper disable once UnusedMethodReturnValue.Global
		public int InsertVertices(int vtx, TV vattr = null)
		{
			var vid = InsertVertex(vattr);

			for (var i = 0; i < vtx - 1; i++)
			{
				InsertVertex(vattr);
			}

			return vid;
		}

		public void AddEdge(int vidFrom, int vidTo, TE attr = null)
		{
			var end = new Edge<TE, TV>(vidFrom, vidTo, this);
			var vtxFrom = FindVertex(vidFrom);
			var vtxTo = FindVertex(vidTo);
			end.Attr = attr;
			try
			{
				vtxFrom.EdgesFrom.Add(vidTo, end);
				vtxFrom.EdgesFromList.Add(end);
				vtxTo.EdgesTo.Add(end);
			}
			catch (Exception)
			{
				VfException.Error("Inconsistent Data");
			}
		}

		public void DeleteVertex(int vid)
		{
			var vtx = FindVertex(vid);
			var arend = new Edge<TE, TV>[vtx.EdgesFromList.Count + vtx.EdgesTo.Count];
			vtx.EdgesFromList.CopyTo(arend, 0);
			vtx.EdgesTo.CopyTo(arend, vtx.EdgesFromList.Count);

			foreach (var end in arend)
			{
				DeleteEdge(end.IDFrom, end.IDTo);
			}
			VertexList.Remove(vtx.ID);
		}

		public void DeleteEdge(int vidFrom, int vidTo)
		{
			var vtxFrom = FindVertex(vidFrom);
			var vtxTo = FindVertex(vidTo);
			var end = vtxFrom.EdgesFromList.FirstOrDefault(e => ReferenceEquals(vtxTo, e.To));

			if (end == null)
			{
				VfException.Error("Inconsistent Data");
			}
			vtxFrom.EdgesFrom.Remove(vidTo);
			vtxTo.EdgesTo.Remove(end);
			vtxFrom.EdgesFromList.Remove(end);
		}
		#endregion
	}

	public class Graph : Graph<Object, Object> { }

#if NUNIT
	[TestFixture]
	public class GraphTester
	{
		[Test]
		public void TestConstructor()
		{
			var gr = new Graph();
			Assert.IsNotNull(gr);
		}

		[ExpectedException(typeof (VfException))]
		[Test]
		public void TestDeleteEdge()
		{
			var gr = new Graph();
			Assert.AreEqual(0, gr.InsertVertex());
			Assert.AreEqual(1, gr.InsertVertex());
			Assert.AreEqual(2, gr.InsertVertex());
			gr.AddEdge(0, 1);
			gr.AddEdge(1, 2);
			gr.AddEdge(2, 0);
			gr.DeleteEdge(1, 2);
			Assert.AreEqual(1, gr.OutEdgeCount(0));
			Assert.AreEqual(0, gr.OutEdgeCount(1));
			Assert.AreEqual(1, gr.OutEdgeCount(2));

			// Trigger the exception - no edge from 1 to 0...
			gr.DeleteEdge(1, 0);
		}

		[ExpectedException(typeof (VfException))]
		[Test]
		public void TestDeleteVertex()
		{
			var gr = new Graph();
			Assert.AreEqual(0, gr.InsertVertex());
			Assert.AreEqual(1, gr.InsertVertex());
			Assert.AreEqual(2, gr.InsertVertex());
			gr.AddEdge(0, 1);
			gr.AddEdge(1, 2);
			gr.AddEdge(2, 0);
			gr.DeleteVertex(0);

			Assert.AreEqual(1, gr.OutEdgeCount(1));
			Assert.AreEqual(0, gr.OutEdgeCount(2));

			// Trigger the exception - shouldn't be a zero vertex any more...
			gr.FindVertex(0);
		}

		[ExpectedException(typeof (VfException))]
		[Test]
		public void TestFindVertexNotFound()
		{
			var gr = new Graph();
			var vtx = gr.FindVertex(0);
			Assert.IsNotNull(vtx);
		}

		[ExpectedException(typeof (VfException))]
		[Test]
		public void TestInsertEdge()
		{
			object attr;
			var gr = new Graph();
			var idFrom = gr.InsertVertex(0);
			var idTo = gr.InsertVertex(1);
			gr.AddEdge(idFrom, idTo, 100);
			Assert.AreEqual(gr.OutEdgeCount(idFrom), 1);
			Assert.AreEqual(gr.OutEdgeCount(idTo), 0);
			var idEdge = gr.GetOutEdge(idFrom, 0, out attr);
			Assert.AreEqual(100, (int) attr);
			Assert.AreEqual(idTo, idEdge);

			// Try inserting the same edge twice to trigger exception...
			gr.AddEdge(0, 1, 200);
		}

		[ExpectedException(typeof (VfException))]
		[Test]
		public void TestInsertVertex()
		{
			var gr = new Graph();
			gr.InsertVertex(1);
			var vtx = gr.FindVertex(0);
			Assert.IsNotNull(vtx);
			vtx = gr.FindVertex(1);
			Assert.IsNotNull(vtx);
		}

		[Test]
		public void TestShuffle()
		{
			var rnd = new Random(10);
			const int count = 100;
			var ariShuffle = new int[count];

			for (var i = 0; i < count; i++)
			{
				ariShuffle[i] = i;
			}
			Assert.AreEqual(0, ariShuffle[0]);
			Assert.AreEqual(99, ariShuffle[99]);
			Graph.Shuffle(ariShuffle, rnd);
			Assert.AreNotEqual(0, ariShuffle[0]);
			Assert.AreNotEqual(99, ariShuffle[99]);

			var iTotal = 0;
			for (var i = 0; i < count; i++)
			{
				iTotal += ariShuffle[i];
			}
			Assert.AreEqual(count * (count - 1) / 2, iTotal);
		}
	}
#endif
}