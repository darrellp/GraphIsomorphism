using System;
using System.Collections.Generic;
using System.Linq;

namespace vflibcs
{
	// At any point in time in the algorithm nodes are classified into one of four
	// groups.  Either they are part of the currently proposed isomorphism or they
	// are pointed to by some node in the isomorphism or they point to some node in
	// the isomorphism or they are completely disconnected from the isomorphism.
	[Flags]
	enum Group
	{
		ContainedInMapping = 1,		// Contained in the mapping
		FromMapping = 2,			// Outside the mapping but pointed to from the mapping
		ToMapping = 4,				// Outside the mapping but points to a node in the mapping
		Disconnected = 8			// Outside the mapping with no links to mapped nodes
	}

	class VfVertex<TAttr>
	{
		#region Private Variables
		readonly VfEdge[] _outEdges;
		readonly VfEdge[] _inEdges;
		readonly object _attribute;
		Group _grps = Group.Disconnected;
		#endregion

		#region Constructor
		internal VfVertex(IGraphLoader<TAttr> loader, int inodGraph, Dictionary<VfEdge, VfEdge> dctEdge, List<int> mpInodGraphInodVf)
		{
			var nid = loader.IdFromPos(inodGraph);
			_attribute = loader.GetNodeAttr(nid);
			_outEdges = new VfEdge[loader.OutEdgeCount(nid)];
			_inEdges = new VfEdge[loader.InEdgeCount(nid)];
			MakeEdges(loader, nid, dctEdge, mpInodGraphInodVf);
		}
		#endregion

		#region Properties

		internal Group Grps
		{
			get { return _grps; }
			set { _grps = value; }
		}


		internal object Attr
		{
			get
			{
				return _attribute;
			}
		}

		internal int InDegree
		{
			get
			{
				return _inEdges.Length;
			}
		}

		internal int OutDegree
		{
			get
			{
				return _outEdges.Length;
			}
		}

		internal List<int> OutNeighbors
		{
			get
			{
				return _outEdges.Select(vfe => vfe.InodTo).ToList();
			}
		}
		internal List<int> InNeighbors
		{
			get
			{
				return _inEdges.Select(vfe => vfe.InodFrom).ToList();
			}
		}

		internal bool FInMapping
		{
			get { return _grps == Group.ContainedInMapping; }
		}
		#endregion

		#region Edge Makers
		private void MakeEdges(IGraphLoader<TAttr> loader, int nid, Dictionary<VfEdge, VfEdge> dctEdge, List<int> mpInodGraphInodVf)
		{
			var inodGraph = loader.PosFromId(nid);
			var vfeKey = new VfEdge(mpInodGraphInodVf[inodGraph], 0, null);

			MakeOutEdges(loader, nid, dctEdge, mpInodGraphInodVf, ref vfeKey);
			MakeInEdges(loader, nid, dctEdge, mpInodGraphInodVf, ref vfeKey);
		}

		// Since we make edges both for incoming edges and outgoing edges every edge actually gets visited twice - once for it's incoming vertex
		// and once for it's outgoing vertex.  The first time we actually create the edge and the second time we need to reference the edge
		// created the first time.  dctEdge keeps track of previously created edges so we can find them on the second visit.  It has to be
		// be maintained during the entire construction of the graph so it's created in the graph constructor and passed down a rather large
		// call chain to be used here.
		private void MakeOutEdges(IGraphLoader<TAttr> loader, int nid, Dictionary<VfEdge, VfEdge> dctEdge, List<int> mpInodGraphInodVf, ref VfEdge vfeKey)
		{
			for (var i = 0; i < loader.OutEdgeCount(nid); i++)
			{
				TAttr attr;
				vfeKey.InodTo = mpInodGraphInodVf[loader.PosFromId(loader.GetOutEdge(nid, i, out attr))];

				if (!dctEdge.ContainsKey(vfeKey))
				{
					_outEdges[i] = dctEdge[vfeKey] = new VfEdge(vfeKey.InodFrom, vfeKey.InodTo, attr);
					vfeKey = new VfEdge(vfeKey.InodFrom, vfeKey.InodTo, null);
				}
				else
				{
					_outEdges[i] = dctEdge[vfeKey];
				}
			}
		}

		private void MakeInEdges(IGraphLoader<TAttr> loader, int nid, Dictionary<VfEdge, VfEdge> dctEdge, List<int> mpInodGraphInodVf, ref VfEdge vfeKey)
		{
			for (var i = 0; i < loader.InEdgeCount(nid); i++)
			{
				TAttr attr;
				vfeKey.InodFrom = mpInodGraphInodVf[loader.PosFromId(loader.GetInEdge(nid, i, out attr))];

				if (!dctEdge.ContainsKey(vfeKey))
				{
					_inEdges[i] = dctEdge[vfeKey] = new VfEdge(vfeKey.InodFrom, vfeKey.InodTo, attr);
					vfeKey = new VfEdge(vfeKey.InodFrom, vfeKey.InodTo, null);
				}
				else
				{
					_inEdges[i] = dctEdge[vfeKey];
				}
			}
		}
		#endregion
	}

	class VfVertex : VfVertex<Object>
	{
		internal VfVertex(IGraphLoader loader, int inodGraph, Dictionary<VfEdge, VfEdge> dctEdge, List<int> mpInodGraphInodVf) :
			base(loader, inodGraph, dctEdge, mpInodGraphInodVf) {}
	}
}
