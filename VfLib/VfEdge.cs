using System;

namespace vflibcs
{
	internal class VfEdge : IEquatable<VfEdge>
	{
		#region Private Variables
		internal int InodFrom;
		internal int InodTo;
		// ReSharper disable once UnusedAutoPropertyAccessor.Global
		public object ObjAttr { get; set; }
		#endregion

		#region Constructor
		internal VfEdge(int inodFrom, int inodTo, object objAttr)
		{
			InodFrom = inodFrom;
			InodTo = inodTo;
			ObjAttr = objAttr;
		}
		#endregion

		#region Hashing
		public bool Equals(VfEdge other)
		{
			return (other != null) && (other.InodFrom.Equals(InodFrom) && other.InodTo.Equals(InodTo));
		}

		// We have to use edges as dictionary keys in VfVertex.MakeInEdges/MakeOutEdges so we give
		// a nice simple hash function here.
		public override int GetHashCode()
		{
			// ReSharper disable NonReadonlyFieldInGetHashCode
			return ((InodFrom << 16) + InodTo).GetHashCode();
			// ReSharper restore NonReadonlyFieldInGetHashCode
		}
		#endregion
	}
}