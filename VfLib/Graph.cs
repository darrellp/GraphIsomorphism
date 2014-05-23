using System;
using System.Collections.Generic;
#if NUNIT
using NUnit.Framework;

#endif

namespace vflibcs
{
	public class Graph : IGraphLoader
	{
		#region Private variables
		internal readonly SortedList<int, NNode> Nodes = new SortedList<int, NNode>(); // Sorted by node id's
		internal const int NidIllegal = -1;
		#endregion

		#region Private structs
		internal class ENode
		{
			public object Attr;
			public int IDFrom = NidIllegal;
			public int IDTo = NidIllegal;
		}

		internal class NNode
		{
			public object Attr;
			public readonly SortedList<int, ENode> EdgesFrom = new SortedList<int, ENode>(); // Key is named id of "to" node
			public readonly List<ENode> EdgesTo = new List<ENode>();
			public int ID = NidIllegal;

			public ENode FindOutEdge(int nidTo)
			{
				try
				{
					return EdgesFrom[nidTo];
				}
				catch (Exception)
				{
					VfException.Error("Inconsistent Data");
				}
				return null;
			}
		}
		#endregion

		#region Properties
		public int NodeCount
		{
			get { return Nodes.Count; }
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
			var ariShuffle = new int[NodeCount];

			for (var i = 0; i < NodeCount; i++)
			{
				ariShuffle[i] = i;
			}
			Shuffle(ariShuffle, rnd);

			graph.InsertNodes(NodeCount);

			for (var inod = 0; inod < NodeCount; inod++)
			{
				var inodShuffled = ariShuffle[inod];
				var nod = Nodes[inodShuffled];

				foreach (var end in nod.EdgesFrom.Values)
				{
					graph.InsertEdge(ariShuffle[PosFromId(end.IDFrom)], ariShuffle[PosFromId(end.IDTo)]);
				}
			}
			return graph;
		}
		#endregion

		#region Accessors
		internal NNode FindNode(int id)
		{
			var i = Nodes.IndexOfKey(id);
			if (i >= 0)
			{
				return Nodes.Values[i];
			}
			VfException.Error("Inconsistent data");
			return null;
		}

		public int IdFromPos(int inod)
		{
			return Nodes.Values[inod].ID;
		}

		public int PosFromId(int nid)
		{
			return Nodes.IndexOfKey(nid);
		}

		public object GetNodeAttr(int id)
		{
			return FindNode(id).Attr;
		}

		public int InEdgeCount(int id)
		{
			return FindNode(id).EdgesTo.Count;
		}

		public int OutEdgeCount(int id)
		{
			return FindNode(id).EdgesFrom.Count;
		}

		public int GetInEdge(int idTo, int pos, out object attr)
		{
			ENode end = null;
			try
			{
				end = FindNode(idTo).EdgesTo[pos];
			}
			catch (Exception)
			{
				VfException.Error("Inconsistent data");
			}

			// ReSharper disable once PossibleNullReferenceException
			attr = end.Attr;
			return end.IDFrom;
		}

		public int GetOutEdge(int idFrom, int pos, out object attr)
		{
			ENode end = null;
			try
			{
				end = FindNode(idFrom).EdgesFrom.Values[pos];
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
		public int InsertNode(object attr = null)
		{
			var nod = new NNode {ID = Nodes.Count, Attr = attr};
			Nodes.Add(nod.ID, nod);
			return nod.ID;
		}

		public int InsertNodes(int cnod, object attr)
		{
			var nid = InsertNode(attr);

			for (var i = 0; i < cnod - 1; i++)
			{
				InsertNode(attr);
			}

			return nid;
		}

		// ReSharper disable once UnusedMethodReturnValue.Global
		public int InsertNodes(int cnod)
		{
			var nid = InsertNode();

			for (var i = 0; i < cnod - 1; i++)
			{
				InsertNode();
			}
			return nid;
		}

		public void InsertEdge(int nidFrom, int nidTo, object attr = null)
		{
			var end = new ENode();
			var nodFrom = FindNode(nidFrom);
			var nodTo = FindNode(nidTo);

			end.IDFrom = nidFrom;
			end.IDTo = nidTo;
			end.Attr = attr;
			try
			{
				nodFrom.EdgesFrom.Add(nidTo, end);
				nodTo.EdgesTo.Add(end);
			}
			catch (Exception)
			{
				VfException.Error("Inconsistent Data");
			}
		}

		public void DeleteNode(int nid)
		{
			var nod = FindNode(nid);
			var arend = new ENode[nod.EdgesFrom.Count + nod.EdgesTo.Count];
			nod.EdgesFrom.Values.CopyTo(arend, 0);
			nod.EdgesTo.CopyTo(arend, nod.EdgesFrom.Count);

			foreach (var end in arend)
			{
				DeleteEdge(end.IDFrom, end.IDTo);
			}
			Nodes.Remove(nod.ID);
		}

		public void DeleteEdge(int nidFrom, int nidTo)
		{
			var nodFrom = FindNode(nidFrom);
			var nodTo = FindNode(nidTo);
			var end = nodFrom.FindOutEdge(nidTo);

			nodFrom.EdgesFrom.Remove(nidTo);
			nodTo.EdgesTo.Remove(end);
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
				var gr = new Graph();
				Assert.IsNotNull(gr);
			}

			[ExpectedException(typeof (VfException))]
			[Test]
			public void TestDeleteEdge()
			{
				var gr = new Graph();
				Assert.AreEqual(0, gr.InsertNode());
				Assert.AreEqual(1, gr.InsertNode());
				Assert.AreEqual(2, gr.InsertNode());
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
			public void TestDeleteNode()
			{
				var gr = new Graph();
				Assert.AreEqual(0, gr.InsertNode());
				Assert.AreEqual(1, gr.InsertNode());
				Assert.AreEqual(2, gr.InsertNode());
				gr.InsertEdge(0, 1);
				gr.InsertEdge(1, 2);
				gr.InsertEdge(2, 0);
				gr.DeleteNode(0);

				Assert.AreEqual(1, gr.OutEdgeCount(1));
				Assert.AreEqual(0, gr.OutEdgeCount(2));

				// Trigger the exception - shouldn't be a zero node any more...
				gr.FindNode(0);
			}

			[ExpectedException(typeof (VfException))]
			[Test]
			public void TestFindNodeNotFound()
			{
				var gr = new Graph();
				var nod = gr.FindNode(0);
				Assert.IsNotNull(nod);
			}

			[ExpectedException(typeof (VfException))]
			[Test]
			public void TestInsertEdge()
			{
				object attr;
				var gr = new Graph();
				var idFrom = gr.InsertNode(0);
				var idTo = gr.InsertNode(1);
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
			public void TestInsertNode()
			{
				var gr = new Graph();
				gr.InsertNode(1);
				var nod = gr.FindNode(0);
				Assert.IsNotNull(nod);
				nod = gr.FindNode(1);
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
				Shuffle(ariShuffle, rnd);
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
		#endregion
	}
}