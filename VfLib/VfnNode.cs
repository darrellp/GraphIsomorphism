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

	class VfnNode<TAttr>
	{
		#region Private Variables
		readonly VfeNode[] _arvfeEdgeOut;
		readonly VfeNode[] _arvfeEdgeIn;
		readonly object _objAttr;
		Group _grps = Group.Disconnected;
		#endregion

		#region Constructor
		internal VfnNode(IGraphLoader<TAttr> loader, int inodGraph, Dictionary<VfeNode, VfeNode> dctEdge, List<int> mpInodGraphInodVf)
		{
			var nid = loader.IdFromPos(inodGraph);
			_objAttr = loader.GetNodeAttr(nid);
			_arvfeEdgeOut = new VfeNode[loader.OutEdgeCount(nid)];
			_arvfeEdgeIn = new VfeNode[loader.InEdgeCount(nid)];
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
				return _objAttr;
			}
		}

		internal int InDegree
		{
			get
			{
				return _arvfeEdgeIn.Length;
			}
		}

		internal int OutDegree
		{
			get
			{
				return _arvfeEdgeOut.Length;
			}
		}

		internal List<int> OutNeighbors
		{
			get
			{
				//var lstOut = new List<int>(_arvfeEdgeOut.Length);
				//lstOut.AddRange(_arvfeEdgeOut.Select(vfe => vfe.InodTo));
				return _arvfeEdgeOut.Select(vfe => vfe.InodTo).ToList();
			}
		}
		internal List<int> InNeighbors
		{
			get
			{
				return _arvfeEdgeIn.Select(vfe => vfe.InodFrom).ToList();
			}
		}

		internal bool FInMapping
		{
			get { return _grps == Group.ContainedInMapping; }
		}
		#endregion

		#region Edge Makers
		private void MakeEdges(IGraphLoader<TAttr> loader, int nid, Dictionary<VfeNode, VfeNode> dctEdge, List<int> mpInodGraphInodVf)
		{
			var inodGraph = loader.PosFromId(nid);
			var vfeKey = new VfeNode(mpInodGraphInodVf[inodGraph], 0, null);

			MakeOutEdges(loader, nid, dctEdge, mpInodGraphInodVf, ref vfeKey);
			MakeInEdges(loader, nid, dctEdge, mpInodGraphInodVf, ref vfeKey);
		}

		private void MakeOutEdges(IGraphLoader<TAttr> loader, int nid, Dictionary<VfeNode, VfeNode> dctEdge, List<int> mpInodGraphInodVf, ref VfeNode vfeKey)
		{
			for (var i = 0; i < loader.OutEdgeCount(nid); i++)
			{
				TAttr attr;
				vfeKey.InodTo = mpInodGraphInodVf[loader.PosFromId(loader.GetOutEdge(nid, i, out attr))];

				if (!dctEdge.ContainsKey(vfeKey))
				{
					_arvfeEdgeOut[i] = dctEdge[vfeKey] = new VfeNode(vfeKey.InodFrom, vfeKey.InodTo, attr);
					vfeKey = new VfeNode(vfeKey.InodFrom, vfeKey.InodTo, null);
				}
				else
				{
					_arvfeEdgeOut[i] = dctEdge[vfeKey];
				}
			}
		}

		private void MakeInEdges(IGraphLoader<TAttr> loader, int nid, Dictionary<VfeNode, VfeNode> dctEdge, List<int> mpInodGraphInodVf, ref VfeNode vfeKey)
		{
			for (var i = 0; i < loader.InEdgeCount(nid); i++)
			{
				TAttr attr;
				vfeKey.InodFrom = mpInodGraphInodVf[loader.PosFromId(loader.GetInEdge(nid, i, out attr))];

				if (!dctEdge.ContainsKey(vfeKey))
				{
					_arvfeEdgeIn[i] = dctEdge[vfeKey] = new VfeNode(vfeKey.InodFrom, vfeKey.InodTo, attr);
					vfeKey = new VfeNode(vfeKey.InodFrom, vfeKey.InodTo, null);
				}
				else
				{
					_arvfeEdgeIn[i] = dctEdge[vfeKey];
				}
			}
		}
		#endregion
	}

	class VfnNode : VfnNode<Object>
	{
		internal VfnNode(IGraphLoader loader, int inodGraph, Dictionary<VfeNode, VfeNode> dctEdge, List<int> mpInodGraphInodVf) :
			base(loader, inodGraph, dctEdge, mpInodGraphInodVf) {}
	}
}
