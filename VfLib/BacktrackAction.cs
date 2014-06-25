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
		readonly int _ivtx;				// Vertex affected
		readonly Group _grpRestore;		// Group to move back to if GroupMove action
		#endregion

		#region Constructor
		internal BacktrackAction(Action act, int iGraph, int ivtx, Group grpRestore = 0)
		{
			_act = act;
			_iGraph = iGraph;
			_ivtx = ivtx;
			_grpRestore = grpRestore;
		}
		#endregion

		#region Backtracking
		internal void Backtrack<TVAttr, TEAttr>(VfState<TVAttr, TEAttr> vfs) 
			where TVAttr : class 
			where TEAttr : class
		{
			switch (_act)
			{
				case Action.DeleteMatch:
					// Undo a matching
					vfs.RemoveFromMappingList(_iGraph, _ivtx);
					break;

				case Action.GroupMove:
					// Move back to previous group
					vfs.MakeMove(_iGraph, _ivtx, _grpRestore);
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
