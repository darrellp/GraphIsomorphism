using System;
using System.Collections.Generic;
using System.Linq;

namespace vflibcs
{
	// At any point in time in the algorithm vertices are classified into one of four
	// groups.  Either they are part of the currently proposed isomorphism or they
	// are pointed to by some vertex in the isomorphism or they point to some vertex in
	// the isomorphism or they are completely disconnected from the isomorphism.
	[Flags]
	enum Group
	{
		ContainedInMapping = 1,		// Contained in the mapping
		FromMapping = 2,			// Outside the mapping but pointed to from the mapping
		ToMapping = 4,				// Outside the mapping but points to a vertex in the mapping
		Disconnected = 8			// Outside the mapping with no links to mapped vertices
	}

	class VfVertex<TVAttr, TEAttr>
	{
		#region Private Variables
		readonly VfEdge[] _outEdges;
		readonly VfEdge[] _inEdges;
		readonly object _attribute;
		Group _grps = Group.Disconnected;
		#endregion

		#region Constructor
		internal VfVertex(IGraphLoader<TVAttr, TEAttr> loader, int ivtxGraph, Dictionary<VfEdge, VfEdge> dctEdge, List<int> mpIvtxGraphIvtxVf)
		{
			var vid = loader.IdFromPos(ivtxGraph);
			_attribute = loader.GetVertexAttr(vid);
			_outEdges = new VfEdge[loader.OutEdgeCount(vid)];
			_inEdges = new VfEdge[loader.InEdgeCount(vid)];
			MakeEdges(loader, vid, dctEdge, mpIvtxGraphIvtxVf);
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
				return _outEdges.Select(vfe => vfe.IvtxTo).ToList();
			}
		}
		internal List<int> InNeighbors
		{
			get
			{
				return _inEdges.Select(vfe => vfe.IvtxFrom).ToList();
			}
		}

		internal bool FInMapping
		{
			get { return _grps == Group.ContainedInMapping; }
		}
		#endregion

		#region Edge Makers
		private void MakeEdges(IGraphLoader<TVAttr, TEAttr> loader, int vid, Dictionary<VfEdge, VfEdge> dctEdge, List<int> mpIvtxGraphIvtxVf)
		{
			var ivtxGraph = loader.PosFromId(vid);
			var vfeKey = new VfEdge(mpIvtxGraphIvtxVf[ivtxGraph], 0, null);

			MakeOutEdges(loader, vid, dctEdge, mpIvtxGraphIvtxVf, ref vfeKey);
			MakeInEdges(loader, vid, dctEdge, mpIvtxGraphIvtxVf, ref vfeKey);
		}

		// Since we make edges both for incoming edges and outgoing edges every edge actually gets visited twice - once for it's incoming vertex
		// and once for it's outgoing vertex.  The first time we actually create the edge and the second time we need to reference the edge
		// created the first time.  dctEdge keeps track of previously created edges so we can find them on the second visit.  It has to be
		// be maintained during the entire construction of the graph so it's created in the graph constructor and passed down a rather large
		// call chain to be used here.
		private void MakeOutEdges(IGraphLoader<TVAttr, TEAttr> loader, int vid, Dictionary<VfEdge, VfEdge> dctEdge, List<int> mpIvtxGraphIvtxVf, ref VfEdge vfeKey)
		{
			for (var i = 0; i < loader.OutEdgeCount(vid); i++)
			{
				TEAttr attr;
				vfeKey.IvtxTo = mpIvtxGraphIvtxVf[loader.PosFromId(loader.GetOutEdge(vid, i, out attr))];

				if (!dctEdge.ContainsKey(vfeKey))
				{
					_outEdges[i] = dctEdge[vfeKey] = new VfEdge(vfeKey.IvtxFrom, vfeKey.IvtxTo, attr);
					vfeKey = new VfEdge(vfeKey.IvtxFrom, vfeKey.IvtxTo, null);
				}
				else
				{
					_outEdges[i] = dctEdge[vfeKey];
				}
			}
		}

		private void MakeInEdges(IGraphLoader<TVAttr, TEAttr> loader, int vid, Dictionary<VfEdge, VfEdge> dctEdge, List<int> mpIvtxGraphIvtxVf, ref VfEdge vfeKey)
		{
			for (var i = 0; i < loader.InEdgeCount(vid); i++)
			{
				TEAttr attr;
				vfeKey.IvtxFrom = mpIvtxGraphIvtxVf[loader.PosFromId(loader.GetInEdge(vid, i, out attr))];

				if (!dctEdge.ContainsKey(vfeKey))
				{
					_inEdges[i] = dctEdge[vfeKey] = new VfEdge(vfeKey.IvtxFrom, vfeKey.IvtxTo, attr);
					vfeKey = new VfEdge(vfeKey.IvtxFrom, vfeKey.IvtxTo, null);
				}
				else
				{
					_inEdges[i] = dctEdge[vfeKey];
				}
			}
		}
		#endregion
	}

	class VfVertex : VfVertex<Object, Object>
	{
		internal VfVertex(IGraphLoader loader, int ivtxGraph, Dictionary<VfEdge, VfEdge> dctEdge, List<int> mpIvtxGraphIvtxVf) :
			base(loader, ivtxGraph, dctEdge, mpIvtxGraphIvtxVf) {}
	}
}
