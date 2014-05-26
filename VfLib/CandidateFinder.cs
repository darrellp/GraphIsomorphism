using System;
#if NUNIT
using NUnit.Framework;
#endif

namespace vflibcs
{
	class CandidateFinder<TAttr>
	{
		#region Private variables
		readonly VfState<TAttr> _vfs;
		readonly int[] _arinodGraph1;
		int _iinod;
		Match _mch;
		int _totalDegree2;
		bool _fFailImmediately;
		#endregion

		#region Constructor
		void SetInitialMatch(int inod1, int inod2)
		{
			_totalDegree2 = _vfs.Vfgr2.InDegree(inod2) + _vfs.Vfgr2.OutDegree(inod2);

			if (!FValidDegrees(inod1, inod2))
			{
				_fFailImmediately = true;
			}
			_mch = new Match(inod1, inod2);
		}

		internal CandidateFinder(VfState<TAttr> vfs)
		{
			_vfs = vfs;

			if (
				!vfs.FnCompareDegrees(vfs.LstOut1.Count, vfs.LstOut2.Count) ||
				!vfs.FnCompareDegrees(vfs.LstIn1.Count, vfs.LstIn2.Count) ||
				!vfs.FnCompareDegrees(vfs.LstDisconnected1.Count, vfs.LstDisconnected2.Count))
			{
				_fFailImmediately = true;
				return;
			}
			if (vfs.LstOut2.Count > 0 && vfs.LstOut1.Count > 0)
			{
				_arinodGraph1 = new int[vfs.LstOut1.Count];
				vfs.LstOut1.CopyTo(_arinodGraph1);
				SetInitialMatch(vfs.LstOut1[0], vfs.LstOut2[0]);
			}
			else if (vfs.LstIn2.Count > 0 && vfs.LstIn1.Count > 0)
			{
				_arinodGraph1 = new int[vfs.LstIn1.Count];
				vfs.LstIn1.CopyTo(_arinodGraph1);
				SetInitialMatch(vfs.LstIn1[0], vfs.LstIn2[0]);
			}
			else if (vfs.LstDisconnected1.Count >= 0)
			{
				_arinodGraph1 = new int[vfs.LstDisconnected1.Count];
				vfs.LstDisconnected1.CopyTo(_arinodGraph1);
				SetInitialMatch(vfs.LstDisconnected1[0], vfs.LstDisconnected2[0]);
			}
		}
		#endregion

		#region State
		// ReSharper disable once UnusedParameter.Local
		bool FValidDegrees(int inod1, int inod2)
		{
			// We must always have the degrees in graph1 at least as large as those in graph2.  Also,
			// since we order the nodes by total degree size, when we fail this condition, we know that
			// there are no further nodes in graph1 which will match the current graph2 node so we can
			// abandon the search.
			return _vfs.FnCompareDegrees(_vfs.Vfgr1.InDegree(inod1) + _vfs.Vfgr1.OutDegree(inod1), _totalDegree2);
		}

		internal Match NextCandidateMatch()
		{
			if (_fFailImmediately)
			{
				return null;
			}

			if (_iinod < _arinodGraph1.Length)
			{
				_mch.Inod1 = _arinodGraph1[_iinod++];
				if (!FValidDegrees(_mch.Inod1, _mch.Inod2))
				{
					return null;
				}
				return _mch;
			}
			return null;
		}
		#endregion
	}

	class CandidateFinder : CandidateFinder<Object>
	{
		internal CandidateFinder(VfState vfs) : base(vfs) {}
	}

	public class CFTests
	{
		public class VfGraphTester
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
				var vfs = VfsTest();
				var cf = new CandidateFinder(vfs);
				var mch = cf.NextCandidateMatch();
				Assert.AreEqual(0, mch.Inod1);
				Assert.AreEqual(0, mch.Inod2);
				mch = cf.NextCandidateMatch();
				Assert.AreEqual(1, mch.Inod1);
				Assert.AreEqual(0, mch.Inod2);
			}
		}
	}
}
