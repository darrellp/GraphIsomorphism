using System.Collections.Generic;
#if NUNIT
using NUnit.Framework;
#endif

namespace vflibcs
{
	/// <summary>
	/// Record to allow undos in backtracking.
	/// </summary>
	/// <remarks>
	/// As we mess with the state we need to keep track of what we do so when
	/// we backtrack we can undo.  This is the purpose of the BacktrackRecord.
	/// There are only two actions taken in the course of a backtrack record -
	/// matching nodes and reclassifying nodes into different groups.
	/// </remarks>
	class BacktrackRecord
	{
		#region Private Variables
		// List of actions to be undone on backtrack
		readonly List<BacktrackAction> _lstActions = new List<BacktrackAction>();
		#endregion

		#region Actions
		// Add an action to potentially be undone later
		internal void AddAction(BacktrackAction act)
		{
			_lstActions.Insert(0, act);
		}

		/// <summary>
		/// Propose to map inod1 from the first graph to inod2 in the second for
		/// the isomorphism being formed
		/// </summary>
		/// <param name="inod1">Node index in the first graph</param>
		/// <param name="inod2">Node index in the second graph</param>
		/// <param name="vfs">State determining the isomorphism</param>
		internal void SetMatch(int inod1, int inod2, VfState vfs)
		{
			// Add both nodes to the "In Mapping" group
			MoveToGroup(1, inod1, Group.ContainedInMapping, vfs);
			MoveToGroup(2, inod2, Group.ContainedInMapping, vfs);

			// Actually set the nodes to correspond in the isomorphism
			vfs.SetIsomorphic(inod1, inod2);

			// Add actions to undo this act...
			AddAction(new BacktrackAction(Action.DeleteMatch, 1, inod1));
			AddAction(new BacktrackAction(Action.DeleteMatch, 2, inod2));
		}

		/// <summary>
		/// Move node to one of the four classifications:
		///		+ unconnected to any node in isomorphism
		///		+ points in to node in isomorphism
		///		+ pointed to be a node in isomorphism
		///		+ in the isomorphism
		/// Note that this call may be redundant - i.e., we may "move" a node
		/// to a group it's already in.  That's expected and we don't actually do
		/// anything in that case.
		/// </summary>
		/// <param name="iGraph">Graph to take action on</param>
		/// <param name="inod">Node index of node to act on</param>
		/// <param name="grpNew">New group to potentially move node to</param>
		/// <param name="vfs">State determining the isomorphism</param>
 
		internal void MoveToGroup(int iGraph, int inod, Group grpNew, VfState vfs)
		{
			var vfg = iGraph == 1 ? vfs.Vfgr1 : vfs.Vfgr2;
			var grpOld = vfg.GetGroup(inod);

			// If node is newly connected to the isomorphism, see if it was connected
			// in the opposite direction previously - if so, it's now connected both ways.
			if (grpOld == Group.FromMapping && grpNew == Group.ToMapping ||
				grpOld == Group.ToMapping && grpNew == Group.FromMapping)
			{
				grpNew = Group.FromMapping | Group.ToMapping;
			}

			// If we actually made a change, then add it to the action list and ensure that
			// it's recorded in the graph.
			if (grpOld != (grpOld | grpNew))
			{
				AddAction(new BacktrackAction(Action.GroupMove, iGraph, inod, grpOld));
				vfs.MakeMove(iGraph, inod, grpNew);
			}
		}
		#endregion

		#region Backtracking
		/// <summary>
		/// Undo the actions taken for this backtrack record
		/// </summary>
		/// <param name="vfs">State to undo the actions in</param>
		internal void Backtrack(VfState vfs)
		{
			_lstActions.ForEach(action => action.Backtrack(vfs));
		}
		#endregion

		#region NUNIT Testing
#if NUNIT
		[TestFixture]
		public class BacktrackRecordTester
		{
			VfState VfsTest()
			{
				var graph1 = new Graph();
				Assert.AreEqual(0, graph1.InsertNode());
				Assert.AreEqual(1, graph1.InsertNode());
				Assert.AreEqual(2, graph1.InsertNode());
				Assert.AreEqual(3, graph1.InsertNode());
				Assert.AreEqual(4, graph1.InsertNode());
				Assert.AreEqual(5, graph1.InsertNode());
				// Circular graph with "extra" edge at (0,3)
				graph1.InsertEdge(0, 1);
				graph1.InsertEdge(1, 2);
				graph1.InsertEdge(2, 3);
				graph1.InsertEdge(3, 4);
				graph1.InsertEdge(4, 5);
				graph1.InsertEdge(5, 0);
				graph1.InsertEdge(0, 3);

				var graph2 = new Graph();
				Assert.AreEqual(0, graph2.InsertNode());
				Assert.AreEqual(1, graph2.InsertNode());
				Assert.AreEqual(2, graph2.InsertNode());
				Assert.AreEqual(3, graph2.InsertNode());
				Assert.AreEqual(4, graph2.InsertNode());
				Assert.AreEqual(5, graph2.InsertNode());
				// Same graph in reverse order with slightly offset "extra" edge at (4,1)
				graph2.InsertEdge(1, 0);
				graph2.InsertEdge(2, 1);
				graph2.InsertEdge(3, 2);
				graph2.InsertEdge(4, 3);
				graph2.InsertEdge(5, 4);
				graph2.InsertEdge(0, 5);
				graph2.InsertEdge(4, 1);

				return new VfState(graph1, graph2);
			}

			[Test]
			public void TestConstructor()
			{
				Assert.IsNotNull(new BacktrackRecord());
			}

			[Test]
			public void TestMatchBacktrack()
			{
				var vfs = VfsTest();
				var btr = new BacktrackRecord();

				btr.SetMatch(0, 1, vfs);
				var grp1 = vfs.Vfgr1.GetGroup(0);
				var grp2 = vfs.Vfgr2.GetGroup(1);

				Assert.IsTrue((((int)grp1 & (int)Group.ContainedInMapping)) != 0);
				Assert.IsTrue((((int)grp2 & (int)Group.ContainedInMapping)) != 0);
				Assert.AreEqual(Group.ContainedInMapping, vfs.Vfgr1.GetGroup(0));
				Assert.AreEqual(Group.ContainedInMapping, vfs.Vfgr2.GetGroup(1));
				btr.Backtrack(vfs);
				grp1 = vfs.Vfgr1.GetGroup(0);
				grp2 = vfs.Vfgr2.GetGroup(1);

				Assert.IsFalse((((int)grp1 & (int)Group.ContainedInMapping)) != 0);
				Assert.IsFalse((((int)grp2 & (int)Group.ContainedInMapping)) != 0);
				Assert.AreEqual(Group.Disconnected, vfs.Vfgr1.GetGroup(0));
				Assert.AreEqual(Group.Disconnected, vfs.Vfgr2.GetGroup(1));
			}
		}
#endif
		#endregion
	}
}
