using System;

namespace vflibcs
{
	internal class VfEdge : IEquatable<VfEdge>
	{
		#region Private Variables
		internal int IvtxFrom;
		internal int IvtxTo;
		// ReSharper disable once UnusedAutoPropertyAccessor.Global
		public object ObjAttr { get; set; }
		#endregion

		#region Constructor
		internal VfEdge(int ivtxFrom, int ivtxTo, object objAttr)
		{
			IvtxFrom = ivtxFrom;
			IvtxTo = ivtxTo;
			ObjAttr = objAttr;
		}
		#endregion

		#region Hashing
		public bool Equals(VfEdge other)
		{
			return (other != null) && (other.IvtxFrom.Equals(IvtxFrom) && other.IvtxTo.Equals(IvtxTo));
		}

		// We have to use edges as dictionary keys in VfVertex.MakeInEdges/MakeOutEdges so we give
		// a nice simple hash function here.
		public override int GetHashCode()
		{
			// ReSharper disable NonReadonlyFieldInGetHashCode
			return ((IvtxFrom << 16) + IvtxTo).GetHashCode();
			// ReSharper restore NonReadonlyFieldInGetHashCode
		}
		#endregion
	}
}