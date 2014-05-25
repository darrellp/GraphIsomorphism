namespace vflibcs
{
	/// <summary>
	/// Represents a proposed match in the isomorphism.  In the search
	/// Graph2 nodes are thought of as the "independent variables" and
	/// Graph1 nodes are the dependent variables.  In other words, we
	/// determine a node in graph2 and then we search for it's correspondent
	/// in Graph1 which is why Inod2 is a privately set variable.
	/// </summary>
	class Match
	{
		#region Private Variables
		#endregion

		#region Properties
		internal int Inod1 { get; set; }
		internal int Inod2 { get; private set; }
		#endregion

		#region Constructor
		public Match(int inod1, int inod2)
		{
			Inod1 = inod1;
			Inod2 = inod2;
		}
		#endregion
	}
}
