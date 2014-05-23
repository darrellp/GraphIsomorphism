using System;

namespace vflibcs
{
	internal class VfeNode : IEquatable<VfeNode>
	{
		#region Private Variables
		internal int InodFrom;
		internal int InodTo;
		// ReSharper disable once UnusedAutoPropertyAccessor.Global
		public object ObjAttr { get; set; }
		#endregion

		#region Constructor
		internal VfeNode(int inodFrom, int inodTo, object objAttr)
		{
			InodFrom = inodFrom;
			InodTo = inodTo;
			ObjAttr = objAttr;
		}
		#endregion

		#region Hashing
		public bool Equals(VfeNode other)
		{
			return (other != null) && (other.InodFrom.Equals(InodFrom) && other.InodTo.Equals(InodTo));
		}

		public override int GetHashCode()
		{
			// ReSharper disable NonReadonlyFieldInGetHashCode
			return ((InodFrom << 16) + InodTo).GetHashCode();
			// ReSharper restore NonReadonlyFieldInGetHashCode
		}
		#endregion
	}
}