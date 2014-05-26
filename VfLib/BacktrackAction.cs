#if NUNIT
using NUnit.Framework;
#endif

namespace vflibcs
{
	enum Action
	{
		DeleteMatch,
		GroupMove
	}

	/// <summary>
	/// Backtrack action taken on a state
	/// </summary>
	class BacktrackAction
	{
		#region Private Variables
		readonly Action _act;			// Type of action
		readonly int _iGraph;			// Which graph was affected
		readonly int _inod;				// Node affected
		readonly Group _grpRestore;		// Group to move back to if GroupMove action
		#endregion

		#region Constructor
		internal BacktrackAction(Action act, int iGraph, int inod, Group grpRestore = 0)
		{
			_act = act;
			_iGraph = iGraph;
			_inod = inod;
			_grpRestore = grpRestore;
		}
		#endregion

		#region Backtracking
		internal void Backtrack<TAttr>(VfState<TAttr> vfs)
		{
			switch (_act)
			{
				case Action.DeleteMatch:
					// Undo a matching
					vfs.RemoveFromMappingList(_iGraph, _inod);
					break;

				case Action.GroupMove:
					// Move back to previous group
					vfs.MakeMove(_iGraph, _inod, _grpRestore);
					break;
			}
		}
		#endregion

		#region NUNIT Testing
#if NUNIT
		[TestFixture]
		public class BacktrackActionTester
		{
			[Test]
			public void TestConstructor()
			{
				Assert.IsNotNull(new BacktrackAction(Action.DeleteMatch, 1, 0));
				Assert.IsNotNull(new BacktrackAction(Action.GroupMove, 1, 0, Group.FromMapping));
			}
		}
#endif
		#endregion

	}
}
