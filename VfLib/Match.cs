namespace vflibcs
{
	/// <summary>
	/// Represents a proposed match in the isomorphism.  In the search
	/// Graph2 vertices are thought of as the "independent variables" and
	/// Graph1 vertices are the dependent variables.  In other words, we
	/// determine a vertex in graph2 and then we search for it's correspondent
	/// in Graph1 which is why Ivtx2 is a privately set variable.
	/// </summary>
	class Match
	{
		#region Private Variables
		#endregion

		#region Properties
		internal int Ivtx1 { get; set; }
		internal int Ivtx2 { get; private set; }
		#endregion

		#region Constructor
		public Match(int ivtx1, int ivtx2)
		{
			Ivtx1 = ivtx1;
			Ivtx2 = ivtx2;
		}
		#endregion
	}
}
