using System;
using System.Collections.Generic;
using System.Linq;

namespace vflibcs
{
	class CmpNodeDegrees : IComparer<int>
	{
		#region Private Variables
		readonly IGraphLoader _loader;
		#endregion

		#region Constructor
		internal CmpNodeDegrees(IGraphLoader loader)
		{
			_loader = loader;
		}
		#endregion

		#region Permutation
		// Return a permutation of node positions so that the corresponding nodes are ordered
		// from highest total degree to lowest
		internal int[] Permutation
		{
			get
			{
				var mpInodInodPermuted = Enumerable.Range(0, _loader.NodeCount).ToArray();
				Array.Sort(mpInodInodPermuted, this);
				return mpInodInodPermuted;
			}
		}
		#endregion

		#region IComparer<vfnNode> Members
		// Sorts positions in _loader so their corresponding vertices run from
		// highest total degree to lowest
		public int Compare(int x, int y)
		{
			if (x == y)
			{
				return 0;
			}
			var nidX = _loader.IdFromPos(x);
			var nidY = _loader.IdFromPos(y);
			var xDegree = _loader.InEdgeCount(nidX) + _loader.OutEdgeCount(nidX);
			var yDegree = _loader.InEdgeCount(nidY) + _loader.OutEdgeCount(nidY);
			return yDegree.CompareTo(xDegree);
		}
		#endregion
	}
}
