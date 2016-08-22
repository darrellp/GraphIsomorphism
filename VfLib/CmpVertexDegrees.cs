using System;
using System.Collections.Generic;
using System.Linq;

namespace vflibcs
{
	class CmpVertexDegrees<TV, TE> : IComparer<int>
	{
		#region Private Variables
		readonly IGraphLoader<TV, TE> _loader;
		#endregion

		#region Constructor
		internal CmpVertexDegrees(IGraphLoader<TV, TE> loader)
		{
			_loader = loader;
		}
		#endregion

		#region Permutation
		// Return a permutation of vertex positions so that the corresponding vertices are ordered
		// from highest total degree to lowest
		internal List<int> Permutation
		{
			get
			{
				var mpIvtxIvtxPermuted = Enumerable.Range(0, _loader.VertexCount).ToArray();
				var ret = new List<int>(mpIvtxIvtxPermuted.Length);
				Array.Sort(mpIvtxIvtxPermuted, this);
				for (int i = 0; i < mpIvtxIvtxPermuted.Length; i++)
				{
					ret.Add(mpIvtxIvtxPermuted[i]);
				}
				return ret;
			}
		}
		// Return a permutation of vertex positions so that the corresponding vertices are ordered
		// from highest total degree to lowest
		internal Dictionary<VfVertex<TV, TE>, Vertex<TV, TE>> PermutationVertices(List<VfVertex<TV, TE>> vfVertices)
		{
			var ret = new Dictionary<VfVertex<TV, TE>, Vertex<TV, TE>>();

			var newVertices = _loader.Vertices.ToList();
			var originalVertices = new Vertex<TV, TE>[newVertices.Count];
			newVertices.CopyTo(originalVertices);

			newVertices.Sort(Compare2);
			for (var i = 0; i < newVertices.Count; i++)
			{
				ret[vfVertices[i]] = newVertices[i];
			}
			return ret;
		}
		#endregion

		#region IComparer<vfnVertex> Members
		// Sorts positions in _loader so their corresponding vertices run from
		// highest total degree to lowest
		public int Compare(int x, int y)
		{
			if (x == y)
			{
				return 0;
			}
			var vidX = _loader.IdFromPos(x);
			var vidY = _loader.IdFromPos(y);
			var xDegree = _loader.InEdgeCount(vidX) + _loader.OutEdgeCount(vidX);
			var yDegree = _loader.InEdgeCount(vidY) + _loader.OutEdgeCount(vidY);
			return yDegree.CompareTo(xDegree);
		}
		// Sorts positions in _loader so their corresponding vertices run from
		// highest total degree to lowest
		private int Compare2(Vertex<TV, TE> x, Vertex<TV, TE> y)
		{
			if (ReferenceEquals(x, y))
			{
				return 0;
			}
			var xDegree = _loader.InEdgeCount(x.ID) + _loader.OutEdgeCount(x.ID);
			var yDegree = _loader.InEdgeCount(y.ID) + _loader.OutEdgeCount(y.ID);
			return yDegree.CompareTo(xDegree);
		}
		#endregion
	}
}
