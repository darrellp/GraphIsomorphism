using System;
#if NUNIT
using NUnit.Framework;
#endif

namespace vflibcs
{
	class CandidateFinder<TVAttr, TEAttr>
	{
		#region Private variables
		// State we're finding candfidate matches for
		readonly VfState<TVAttr, TEAttr> _vfs;

		// List of graph1 candidates for the current graph2 candidate
		readonly int[] _graph1Candidates;

		// Index of the current graph1 candidate in _graph1Candidates
		int _iivtx;

		// Current match being attempted
		Match _mch;

		// Total number of edges into and out of our graph2 candidate
		int _totalDegree2;

		// Fail at the next call to NextCandidateMatch()
		bool _fFailImmediately;
		#endregion

		#region Constructor
		/// <summary>
		/// Set the next graph1 vertex and graph2 vertices to attempt to match
		/// </summary>
		/// <param name="ivtx1">graph1 vertex</param>
		/// <param name="ivtx2">graph2 vertex</param>
		void SetInitialMatch(int ivtx1, int ivtx2)
		{
			// Determine the graph2 vertex's degree once so we can compare it as we work our way
			// through the graph1 candidates
			_totalDegree2 = _vfs.VfGraph2.InDegree(ivtx2) + _vfs.VfGraph2.OutDegree(ivtx2);

			// If our degrees don't match up properly, we can't make an isomorphism from this.
			// This early opt out is only available because we sort vertices by degree.
			if (!FValidDegrees(ivtx1, ivtx2))
			{
				_fFailImmediately = true;
			}

			// If everything is kosher, set up the match
			_mch = new Match(ivtx1, ivtx2);
		}

		/// <summary>
		/// Find a candidate graph2 vertex and the list of possible matches from graph1
		/// </summary>
		/// <remarks>
		/// Find a candidate list from graph1 vertices for a particular graph2 vertex.  If we've got vertices
		/// in both out lists, take the first graph2 vertex in the outlist and use all the vertices in the
		/// graph1 out list as candidates.  If that doesn't work, do the same for the two respective
		/// in lists and finally for the vertices disconnected from the current isomorphism.
		/// </remarks>
		/// <param name="vfs">State to grab vertices from</param>
		internal CandidateFinder(VfState<TVAttr, TEAttr> vfs)
		{
			_vfs = vfs;

			// Check to see if degrees are valid - this is only available because we sort the vertices
			// by degree
			if (
				!vfs.FnCompareDegrees(vfs.LstOut1.Count, vfs.LstOut2.Count) ||
				!vfs.FnCompareDegrees(vfs.LstIn1.Count, vfs.LstIn2.Count) ||
				!vfs.FnCompareDegrees(vfs.LstDisconnected1.Count, vfs.LstDisconnected2.Count))
			{
				// Life isn't worth living - signal to ourselves to fail at the first call to
				// NextCandidateMatch().
				_fFailImmediately = true;
				return;
			}

			// Try to find a match in vertices pointed to from the isomorphism
			if (vfs.LstOut2.Count > 0 && vfs.LstOut1.Count > 0)
			{
				_graph1Candidates = new int[vfs.LstOut1.Count];
				vfs.LstOut1.CopyTo(_graph1Candidates);
				SetInitialMatch(vfs.LstOut1[0], vfs.LstOut2[0]);
			}
			// Try to find a match in vertices pointing into the isomorphism
			else if (vfs.LstIn2.Count > 0 && vfs.LstIn1.Count > 0)
			{
				_graph1Candidates = new int[vfs.LstIn1.Count];
				vfs.LstIn1.CopyTo(_graph1Candidates);
				SetInitialMatch(vfs.LstIn1[0], vfs.LstIn2[0]);
			}
			// Try to find a match in vertices unattached to the isomorphism
			else if (vfs.LstDisconnected1.Count >= 0)
			{
				_graph1Candidates = new int[vfs.LstDisconnected1.Count];
				vfs.LstDisconnected1.CopyTo(_graph1Candidates);
				SetInitialMatch(vfs.LstDisconnected1[0], vfs.LstDisconnected2[0]);
			}
		}
		#endregion

		#region State
		// ReSharper disable once UnusedParameter.Local
		bool FValidDegrees(int ivtx1, int ivtx2)
		{
			// We must always have the degrees in graph1 at least as large as those in graph2.  Also,
			// since we order the vertices by total degree size, when we fail this condition, we know that
			// there are no further vertices in graph1 which will match the current graph2 vertex so we can
			// abandon the search.
			return _vfs.FnCompareDegrees(_vfs.VfGraph1.InDegree(ivtx1) + _vfs.VfGraph1.OutDegree(ivtx1), _totalDegree2);
		}

		internal Match NextCandidateMatch()
		{
			// Have we predetermined a failure?
			if (_fFailImmediately)
			{
				return null;
			}

			// Try to move to the next graph1 candidate
			if (_iivtx < _graph1Candidates.Length)
			{
				_mch.Ivtx1 = _graph1Candidates[_iivtx++];

				// If the degrees don't match up properly then fail - nothing beyond here will
				// work since the degrees are in decreasing size
				if (!FValidDegrees(_mch.Ivtx1, _mch.Ivtx2))
				{
					return null;
				}
				return _mch;
			}
			return null;
		}
		#endregion
	}

	class CandidateFinder : CandidateFinder<Object, Object>
	{
		internal CandidateFinder(VfState vfs) : base(vfs) {}
	}

#if NUNIT
	public class CFTests
	{
		public class VfGraphTester
		{
			VfState VfsTest()
			{
				var graph1 = new Graph();
				Assert.AreEqual(0, graph1.InsertVertex());
				Assert.AreEqual(1, graph1.InsertVertex());
				Assert.AreEqual(2, graph1.InsertVertex());
				Assert.AreEqual(3, graph1.InsertVertex());
				Assert.AreEqual(4, graph1.InsertVertex());
				Assert.AreEqual(5, graph1.InsertVertex());
				graph1.AddEdge(0, 1);
				graph1.AddEdge(1, 2);
				graph1.AddEdge(2, 3);
				graph1.AddEdge(3, 4);
				graph1.AddEdge(4, 5);
				graph1.AddEdge(5, 0);
				graph1.AddEdge(0, 3);

				var graph2 = new Graph();
				Assert.AreEqual(0, graph2.InsertVertex());
				Assert.AreEqual(1, graph2.InsertVertex());
				Assert.AreEqual(2, graph2.InsertVertex());
				Assert.AreEqual(3, graph2.InsertVertex());
				Assert.AreEqual(4, graph2.InsertVertex());
				Assert.AreEqual(5, graph2.InsertVertex());
				graph2.AddEdge(1, 0);
				graph2.AddEdge(2, 1);
				graph2.AddEdge(3, 2);
				graph2.AddEdge(4, 3);
				graph2.AddEdge(5, 4);
				graph2.AddEdge(0, 5);
				graph2.AddEdge(4, 1);

				return new VfState(graph1, graph2);
			}

			[Test]
			public void TestConstructor()
			{
				var vfs = VfsTest();
				var cf = new CandidateFinder(vfs);
				var mch = cf.NextCandidateMatch();
				Assert.AreEqual(0, mch.Ivtx1);
				Assert.AreEqual(0, mch.Ivtx2);
				mch = cf.NextCandidateMatch();
				Assert.AreEqual(1, mch.Ivtx1);
				Assert.AreEqual(0, mch.Ivtx2);
			}
		}
	}
#endif
}
