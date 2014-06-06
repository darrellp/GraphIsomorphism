using System;
using System.Collections.Generic;
using System.Linq;

namespace vflibcs
{
	class CmpVertexDegrees<TVAttr, TEAttr> : IComparer<int>
	{
		#region Private Variables
		readonly IGraphLoader<TVAttr, TEAttr> _loader;
		#endregion

		#region Constructor
		internal CmpVertexDegrees(IGraphLoader<TVAttr, TEAttr> loader)
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
		#endregion
	}
}
