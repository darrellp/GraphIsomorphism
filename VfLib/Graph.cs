using System;
using System.Collections.Generic;
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
		public int IDFrom { get; private set; }
		public int IDTo { get; private set; }
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
		public int ID = Graph.VidIllegal;

		public Edge<TE, TV> FindOutEdge(int vidTo)
		{
			try
			{
				return EdgesFrom[vidTo];
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
		internal readonly SortedList<int, Vertex<TV, TE>> Vertices = new SortedList<int, Vertex<TV, TE>>(); // Sorted by vertex id's
		internal const int VidIllegal = -1;
		#endregion

		#region Private structs
		#endregion

		#region Properties
		public int VertexCount
		{
			get { return Vertices.Count; }
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
				var nod = Vertices[ivtxShuffled];

				foreach (var end in nod.EdgesFrom.Values)
				{
					graph.InsertEdge(ariShuffle[PosFromId(end.IDFrom)], ariShuffle[PosFromId(end.IDTo)]);
				}
			}
			return graph;
		}
		#endregion

		#region Accessors
		internal Vertex<TV, TE> VertexFromPos(int ipos)
		{
			return Vertices[ipos];
		}

		internal Vertex<TV, TE> FindVertex(int id)
		{
			var i = Vertices.IndexOfKey(id);
			if (i >= 0)
			{
				return Vertices.Values[i];
			}
			VfException.Error("Inconsistent data");
			return null;
		}

		public int IdFromPos(int ivtx)
		{
			return Vertices.Values[ivtx].ID;
		}

		public int PosFromId(int vid)
		{
			return Vertices.IndexOfKey(vid);
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
			return FindVertex(id).EdgesFrom.Count;
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
			var nod = new Vertex<TV, TE> {ID = Vertices.Count, Attr = attr};
			Vertices.Add(nod.ID, nod);
			return nod.ID;
		}

		// ReSharper disable once UnusedMethodReturnValue.Global
		public int InsertVertices(int cnod, TV vattr = null)
		{
			var vid = InsertVertex(vattr);

			for (var i = 0; i < cnod - 1; i++)
			{
				InsertVertex(vattr);
			}

			return vid;
		}

		public void InsertEdge(int vidFrom, int vidTo, TE attr = null)
		{
			var end = new Edge<TE, TV>(vidFrom, vidTo, this);
			var nodFrom = FindVertex(vidFrom);
			var nodTo = FindVertex(vidTo);
			end.Attr = attr;
			try
			{
				nodFrom.EdgesFrom.Add(vidTo, end);
				nodTo.EdgesTo.Add(end);
			}
			catch (Exception)
			{
				VfException.Error("Inconsistent Data");
			}
		}

		public void DeleteVertex(int vid)
		{
			var nod = FindVertex(vid);
			var arend = new Edge<TE, TV>[nod.EdgesFrom.Count + nod.EdgesTo.Count];
			nod.EdgesFrom.Values.CopyTo(arend, 0);
			nod.EdgesTo.CopyTo(arend, nod.EdgesFrom.Count);

			foreach (var end in arend)
			{
				DeleteEdge(end.IDFrom, end.IDTo);
			}
			Vertices.Remove(nod.ID);
		}

		public void DeleteEdge(int vidFrom, int vidTo)
		{
			var nodFrom = FindVertex(vidFrom);
			var nodTo = FindVertex(vidTo);
			var end = nodFrom.FindOutEdge(vidTo);

			nodFrom.EdgesFrom.Remove(vidTo);
			nodTo.EdgesTo.Remove(end);
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
			gr.InsertEdge(0, 1);
			gr.InsertEdge(1, 2);
			gr.InsertEdge(2, 0);
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
			gr.InsertEdge(0, 1);
			gr.InsertEdge(1, 2);
			gr.InsertEdge(2, 0);
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
			var nod = gr.FindVertex(0);
			Assert.IsNotNull(nod);
		}

		[ExpectedException(typeof (VfException))]
		[Test]
		public void TestInsertEdge()
		{
			object attr;
			var gr = new Graph();
			var idFrom = gr.InsertVertex(0);
			var idTo = gr.InsertVertex(1);
			gr.InsertEdge(idFrom, idTo, 100);
			Assert.AreEqual(gr.OutEdgeCount(idFrom), 1);
			Assert.AreEqual(gr.OutEdgeCount(idTo), 0);
			var idEdge = gr.GetOutEdge(idFrom, 0, out attr);
			Assert.AreEqual(100, (int) attr);
			Assert.AreEqual(idTo, idEdge);

			// Try inserting the same edge twice to trigger exception...
			gr.InsertEdge(0, 1, 200);
		}

		[ExpectedException(typeof (VfException))]
		[Test]
		public void TestInsertVertex()
		{
			var gr = new Graph();
			gr.InsertVertex(1);
			var nod = gr.FindVertex(0);
			Assert.IsNotNull(nod);
			nod = gr.FindVertex(1);
			Assert.IsNotNull(nod);
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