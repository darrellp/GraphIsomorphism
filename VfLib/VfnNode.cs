using System;
using System.Collections.Generic;
using System.Linq;

namespace vflibcs
{
	[Flags]
	enum Groups
	{
		ContainedInMapping = 1,		// Contained in the mapping
		FromMapping = 2,			// Outside the mapping but pointed to from the mapping
		ToMapping = 4,				// Outside the mapping but points to a node in the mapping
		Disconnected = 8			// Outside the mapping with no links to mapped nodes
	}

	class VfnNode
	{
		#region Private Variables
		readonly VfeNode[] _arvfeEdgeOut;
		readonly VfeNode[] _arvfeEdgeIn;
		readonly object _objAttr;
		Groups _grps = Groups.Disconnected;
		#endregion

		#region Constructor
		internal VfnNode(IGraphLoader loader, int inodGraph, Dictionary<VfeNode, VfeNode> dctEdge, int[] mpInodGraphInodVf)
		{
			int nid = loader.IdFromPos(inodGraph);
			_objAttr = loader.GetNodeAttr(nid);
			_arvfeEdgeOut = new VfeNode[loader.OutEdgeCount(nid)];
			_arvfeEdgeIn = new VfeNode[loader.InEdgeCount(nid)];
			MakeEdges(loader, nid, dctEdge, mpInodGraphInodVf);
		}
		#endregion

		#region Properties

		internal Groups Grps
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
				//List<int> lstIn = new List<int>(_arvfeEdgeIn.Length);
				//lstIn.AddRange(_arvfeEdgeIn.Select(vfe => vfe.InodFrom));
				return _arvfeEdgeIn.Select(vfe => vfe.InodFrom).ToList();
			}
		}

		internal bool FInMapping
		{
			get { return _grps == Groups.ContainedInMapping; }
		}
		#endregion

		#region Edge Makers
		private void MakeEdges(IGraphLoader loader, int nid, Dictionary<VfeNode, VfeNode> dctEdge, int[] mpInodGraphInodVf)
		{
			int inodGraph = loader.PosFromId(nid);
			int inodVf = mpInodGraphInodVf[inodGraph];
			var vfeKey = new VfeNode(0, 0, null) {InodFrom = inodVf};

			MakeOutEdges(loader, nid, dctEdge, mpInodGraphInodVf, ref vfeKey);
			vfeKey.InodTo = inodVf;
			MakeInEdges(loader, nid, dctEdge, mpInodGraphInodVf, ref vfeKey);
		}

		private void MakeOutEdges(IGraphLoader loader, int nid, Dictionary<VfeNode, VfeNode> dctEdge, int[] mpInodGraphInodVf, ref VfeNode vfeKey)
		{
			for (var i = 0; i < loader.OutEdgeCount(nid); i++)
			{
				object attr;
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

		private void MakeInEdges(IGraphLoader loader, int nid, Dictionary<VfeNode, VfeNode> dctEdge, int[] mpInodGraphInodVf, ref VfeNode vfeKey)
		{
			for (var i = 0; i < loader.InEdgeCount(nid); i++)
			{
				object attr;
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
}
