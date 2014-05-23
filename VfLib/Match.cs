namespace vflibcs
{
	class Match
	{
		#region Private Variables
		readonly int _inod2;
		#endregion

		#region Properties
		internal int Inod1 { get; set; }

		internal int Inod2
		{
			get { return _inod2; }
		}
		#endregion

		#region Constructor
		public Match(int inod1, int inod2)
		{
			Inod1 = inod1;
			_inod2 = inod2;
		}
		#endregion
	}
}
