//#define GATHERSTATS
//#define BIGSLOWTEST
//#define PERFORMANCETEST

using System;
using System.Collections.Generic;
#if NUNIT
using NUnit.Framework;

#endif

namespace vflibcs
{
	// Struct representing a full isomorphism mapping
	public struct FullMapping
	{
		public int[] ArinodMap1To2;
		public int[] ArinodMap2To1;

		public FullMapping(int count1, int count2)
		{
			ArinodMap1To2 = new int[count1];
			ArinodMap2To1 = new int[count2];
		}

		public FullMapping(int[] arinodMap1To2Prm, int[] arinodMap2To1Prm)
		{
			var cElements = arinodMap1To2Prm.Length;
			ArinodMap1To2 = new int[cElements];
			ArinodMap2To1 = new int[cElements];
			Array.Copy(arinodMap1To2Prm, ArinodMap1To2, cElements);
			Array.Copy(arinodMap2To1Prm, ArinodMap2To1, cElements);
		}
	}

	internal class VfState
	{
		#region Private Variables
		private VfGraph _vfgr1, _vfgr2;
		internal const int MapIllegal = -1;

		// Mapping between node positions in VfGraph and the original Graph.
		// This is just the permutation to sort the original graph nodes by degrees.
		private readonly int[] _armpInodVfInodGraph1;
		private readonly int[] _armpInodVfInodGraph2;

		// Above mappings but indexed from/to node id's from the original maps.
		private int[] _armpNid1Nid2;
		private int[] _armpNid2Nid1;

		// List of Isomorphic mappings in original maps
		private List<FullMapping> _lstfm;

		// The original ILoader's - needed to map back the permutation
		// to the original node id's after the match
		private readonly IGraphLoader _ldr1;
		private readonly IGraphLoader _ldr2;

		// The actual mappings we're building up
		private readonly int[] _arinodMap1To2;
		private readonly int[] _arinodMap2To1;

		// All the mappings we've located thus far...
		private readonly List<FullMapping> _lstMappings = new List<FullMapping>();

		// The following lists are sorted on index but since the
		// original nodes were sorted by degree, that means that
		// these are also sorted by degree which is the key.
		private SortedListNoKey<int> _lstIn1 = new SortedListNoKey<int>();
		private SortedListNoKey<int> _lstOut1 = new SortedListNoKey<int>();
		private SortedListNoKey<int> _lstIn2 = new SortedListNoKey<int>();
		private SortedListNoKey<int> _lstOut2 = new SortedListNoKey<int>();
		private SortedListNoKey<int> _lstDisconnected1 = new SortedListNoKey<int>();
		private SortedListNoKey<int> _lstDisconnected2 = new SortedListNoKey<int>();

		private int _inDegreeTotal1, _inDegreeTotal2, _outDegreeTotal1, _outDegreeTotal2;

		private bool _fSuccessfulMatch; // Have we made a successful match?
		private bool _fMatched; // Have we attempted a match?
		private readonly bool _fContextCheck; // Do we context check using the attributes?
		private readonly bool _fFindAll; // Find all subgraphs or just the first?
		#endregion

		#region Properties
		private bool FMassagedPermutation
		{
			get { return _armpNid1Nid2 != null; }
		}

		private bool FMassagedPermutationList
		{
			get { return _lstfm != null; }
		}

		public int[] Mapping1To2
		{
			get
			{
				if (!_fSuccessfulMatch)
				{
					return null;
				}
				if (!FMassagedPermutation)
				{
					MassagePermutation();
				}
				return _armpNid1Nid2;
			}
		}

		public int[] Mapping2To1
		{
			get
			{
				if (!_fSuccessfulMatch)
				{
					return null;
				}
				if (!FMassagedPermutation)
				{
					MassagePermutation();
				}
				return _armpNid2Nid1;
			}
		}

		public List<FullMapping> Mappings
		{
			get
			{
				if (!_fSuccessfulMatch)
				{
					return null;
				}
				if (!FMassagedPermutationList)
				{
					MassagePermutationList();
				}
				return _lstfm;
			}
		}

		private void MassagePermutation()
		{
			// Permutations to move from VfGraph inods to Graph inods
			var armpInodGraphInodVf1 = VfGraph.ReversePermutation(_armpInodVfInodGraph1);
			var armpInodGraphInodVf2 = VfGraph.ReversePermutation(_armpInodVfInodGraph2);

			// Holding areas for new permutations
			_armpNid1Nid2 = new int[_arinodMap1To2.Length];
			_armpNid2Nid1 = new int[_arinodMap2To1.Length];

			for (var i = 0; i < _arinodMap1To2.Length; i++)
			{
				var inodMap = _arinodMap1To2[armpInodGraphInodVf1[i]];
				_armpNid1Nid2[i] = (inodMap == MapIllegal ? MapIllegal : _ldr2.IdFromPos(_armpInodVfInodGraph2[inodMap]));
			}

			for (var i = 0; i < _arinodMap2To1.Length; i++)
			{
				// Shouldn't be any map_illegal values in the second graph's array
				_armpNid2Nid1[i] = _ldr1.IdFromPos(_armpInodVfInodGraph1[_arinodMap2To1[armpInodGraphInodVf2[i]]]);
			}
		}

		private void MassagePermutations(
			int[] arinodMap1To2, int[] arinodMap2To1,
			int[] armpInodGraphInodVf1, int[] armpInodGraphInodVf2,
			ref int[] armpNid1Nid2, ref int[] armpNid2Nid1)
		{
			// Holding areas for new permutations
			armpNid1Nid2 = new int[arinodMap1To2.Length];
			armpNid2Nid1 = new int[arinodMap2To1.Length];

			for (var i = 0; i < arinodMap1To2.Length; i++)
			{
				var inodMap = arinodMap1To2[armpInodGraphInodVf1[i]];
				armpNid1Nid2[i] = (inodMap == MapIllegal ? MapIllegal : _ldr2.IdFromPos(_armpInodVfInodGraph2[inodMap]));
			}

			for (var i = 0; i < arinodMap2To1.Length; i++)
			{
				// Shouldn't be any map_illegal values in the second graph's array
				armpNid2Nid1[i] = _ldr1.IdFromPos(_armpInodVfInodGraph1[arinodMap2To1[armpInodGraphInodVf2[i]]]);
			}
		}

		private void MassagePermutationList()
		{
			var count1 = _arinodMap1To2.Length;
			var count2 = _arinodMap2To1.Length;

			// Permutations to move from VfGraph inods to Graph inods
			var armpInodGraphInodVf1 = VfGraph.ReversePermutation(_armpInodVfInodGraph1);
			var armpInodGraphInodVf2 = VfGraph.ReversePermutation(_armpInodVfInodGraph2);
			_lstfm = new List<FullMapping>(_lstMappings.Count);

			foreach (var fm in _lstMappings)
			{
				var fmTmp = new FullMapping(count1, count2);
				MassagePermutations(fm.ArinodMap1To2, fm.ArinodMap2To1, armpInodGraphInodVf1, armpInodGraphInodVf2,
					ref fmTmp.ArinodMap1To2, ref fmTmp.ArinodMap2To1);
				_lstfm.Add(fmTmp);
			}
		}

		internal VfGraph Vfgr2
		{
			get { return _vfgr2; }
			set { _vfgr2 = value; }
		}

		internal VfGraph Vfgr1
		{
			get { return _vfgr1; }
			set { _vfgr1 = value; }
		}

		internal SortedListNoKey<int> LstIn1
		{
			get { return _lstIn1; }
			set { _lstIn1 = value; }
		}

		internal SortedListNoKey<int> LstOut1
		{
			get { return _lstOut1; }
			set { _lstOut1 = value; }
		}

		internal SortedListNoKey<int> LstIn2
		{
			get { return _lstIn2; }
			set { _lstIn2 = value; }
		}

		internal SortedListNoKey<int> LstOut2
		{
			get { return _lstOut2; }
			set { _lstOut2 = value; }
		}

		internal SortedListNoKey<int> LstDisconnected1
		{
			get { return _lstDisconnected1; }
			set { _lstDisconnected1 = value; }
		}

		internal SortedListNoKey<int> LstDisconnected2
		{
			get { return _lstDisconnected2; }
			set { _lstDisconnected2 = value; }
		}
		#endregion

		#region Delegates
		internal delegate bool FCompareDegreesDelegate(int degree1, int degree2);

		internal FCompareDegreesDelegate fnCmp;

		private static bool CmpIsomorphism(int degree1, int degree2)
		{
			return degree1 == degree2;
		}

		private static bool CmpSubgraphIsomorphism(int degree1, int degree2)
		{
			return degree1 >= degree2;
		}
		#endregion

		#region Constructors
		public VfState(IGraphLoader loader1, IGraphLoader loader2, bool fIsomorphism, bool fContextCheck, bool fFindAll)
		{
			_ldr1 = loader1;
			_ldr2 = loader2;

			_fContextCheck = fContextCheck;
			_fFindAll = fFindAll;

			if (fIsomorphism)
			{
				fnCmp = CmpIsomorphism;
			}
			else
			{
				fnCmp = CmpSubgraphIsomorphism;
			}

			_armpInodVfInodGraph1 = new CmpNodeDegrees(loader1).Permutation;
			_armpInodVfInodGraph2 = new CmpNodeDegrees(loader2).Permutation;
			_vfgr1 = new VfGraph(loader1, _armpInodVfInodGraph1);
			_vfgr2 = new VfGraph(loader2, _armpInodVfInodGraph2);
			_arinodMap1To2 = new int[loader1.NodeCount];
			_arinodMap2To1 = new int[loader2.NodeCount];
			for (var i = 0; i < loader1.NodeCount; i++)
			{
				_arinodMap1To2[i] = MapIllegal;
				_lstDisconnected1.Add(i);
			}
			for (var i = 0; i < loader2.NodeCount; i++)
			{
				_arinodMap2To1[i] = MapIllegal;
				_lstDisconnected2.Add(i);
			}
		}

		public VfState(IGraphLoader loader1, IGraphLoader loader2, bool fIsomorphism, bool fContextCheck)
			: this(loader1, loader2, fIsomorphism, fContextCheck, false)
		{
		}

		public VfState(IGraphLoader loader1, IGraphLoader loader2, bool fIsomorphism)
			: this(loader1, loader2, fIsomorphism, false, false)
		{
		}

		public VfState(IGraphLoader loader1, IGraphLoader loader2) : this(loader1, loader2, false, false, false)
		{
		}
		#endregion

		#region Matching
		private bool FCompleteMatch()
		{
			return _lstDisconnected2.Count == 0 && _lstIn2.Count == 0 && _lstOut2.Count == 0;
		}

#if RECURSIVE
	// Find an isomorphism between a subgraph of _vfgr1 and the entirey of _vfgr2...
		public bool FMatch()
		{
			if (_fMatched)
			{
				return false;
			}
			_fMatched = true;

			// Since the subgraph of subgraph isomorphism is in _vfgr1, it must have at
			// least as many nodes as _vfgr2...
			if (!fnCmp(_vfgr1.NodeCount, _vfgr2.NodeCount))
			{
				return false;
			}
			return FMatchRecursive();
		}

		bool FMatchRecursive()
		{
			Match mchCur;
			CandidateFinder cf;

			if (FCompleteMatch())
			{
				return true;
			}

			cf = new CandidateFinder(this);
			while ((mchCur = cf.NextCandidateMatch()) != null)
			{
				if (FFeasible(mchCur))
				{
					BacktrackRecord btr = new BacktrackRecord();
					AddMatchToSolution(mchCur, btr);
					if (FMatchRecursive())
					{
						_fSuccessfulMatch = true;
						return true;
					}
					btr.Backtrack(this);
				}
			}
			return false;
        }
#else
		private bool FCompatibleDegrees()
		{
			if (!fnCmp(_vfgr1.NodeCount, _vfgr2.NodeCount))
			{
				return false;
			}

			for (var iNode = 0; iNode < _vfgr2.NodeCount; iNode++)
			{
				if (!fnCmp(_vfgr1.TotalDegree(iNode), _vfgr2.TotalDegree(iNode)))
				{
					return false;
				}
			}
			return true;
		}

		private void RecordCurrentMatch()
		{
			_lstMappings.Add(new FullMapping(_arinodMap1To2, _arinodMap2To1));
		}

		// Find an isomorphism between a subgraph of _vfgr1 and the entirity of _vfgr2...
		public bool FMatch()
		{
			if (!FCompatibleDegrees())
			{
				return false;
			}

			var stkcf = new Stack<CandidateFinder>();
			var stkbr = new Stack<BacktrackRecord>();

			Match mchCur;
			CandidateFinder cf;
			BacktrackRecord btr;
			var fPopOut = false;
#if GATHERSTATS
			int cSearchGuesses = 0;
			int cBackTracks = 0;
			int cInfeasible = 0;
#endif

			if (_fMatched)
			{
				return false;
			}
			_fMatched = true;

			// Since the subgraph of subgraph isomorphism is in _vfgr1, it must have at
			// least as many nodes as _vfgr2...
			if (!fnCmp(_vfgr1.NodeCount, _vfgr2.NodeCount))
			{
				return false;
			}

			if (FCompleteMatch())
			{
				_fSuccessfulMatch = true;
				return true;
			}

			// Non-recursive implementation of a formerly recursive function
			while (true)
			{
				if (fPopOut)
				{
					if (stkcf.Count <= 0)
					{
						break; // Out of the top level while loop and return false
					}
					cf = stkcf.Pop();
					btr = stkbr.Pop();
#if GATHERSTATS
					cBackTracks++;
#endif
					btr.Backtrack(this);
				}
				else
				{
					cf = new CandidateFinder(this);
					btr = new BacktrackRecord();
				}
				fPopOut = true;
				while ((mchCur = cf.NextCandidateMatch()) != null)
				{
					if (FFeasible(mchCur))
					{
						if (FAddMatchToSolution(mchCur, btr) && FCompleteMatch())
						{
							_fSuccessfulMatch = true;
#if GATHERSTATS
							Console.WriteLine("cBackTracks = {0}", cBackTracks);
							Console.WriteLine("cSearchGuesses = {0}", cSearchGuesses);
							Console.WriteLine("cInfeasible = {0}", cInfeasible);
#endif
							if (_fFindAll)
							{
								// Record this match and simulate a failure...
								RecordCurrentMatch();
								stkcf.Push(cf);
								stkbr.Push(btr);
								break;
							}
							return true;
						}
#if GATHERSTATS
	// Made a bad guess, count it up...
							cSearchGuesses++;
#endif
						stkcf.Push(cf);
						stkbr.Push(btr);
						fPopOut = false;
						break; // Out of the inner level while loop to "call" into the outer loop
					}
#if GATHERSTATS
					else
					{
						cInfeasible++;
					}
#endif
				}
			}
#if GATHERSTATS
			Console.WriteLine("cBackTracks = {0}", cBackTracks);
			Console.WriteLine("cSearchGuesses = {0}", cSearchGuesses);
			Console.WriteLine("cInfeasible = {0}", cInfeasible);
#endif
			return _fSuccessfulMatch;
		}
#endif
		#endregion

		#region Mapping
		internal void SetMapping(int inod1, int inod2)
		{
			_arinodMap1To2[inod1] = inod2;
			_arinodMap2To1[inod2] = inod1;
		}

		internal void RemoveFromMappingList(int iGraph, int inod)
		{
			var mp = iGraph == 1 ? _arinodMap1To2 : _arinodMap2To1;
			mp[inod] = MapIllegal;
		}
		#endregion

		#region Feasibility
		private int GetGroupCountInList(IEnumerable<int> lstOfNodes, VfGraph vfgr, Groups grp)
		{
			var cnod = 0;

			foreach (var inod in lstOfNodes)
			{
				if (((int) vfgr.GetGroup(inod) & (int) grp) != 0)
				{
					cnod++;
				}
			}

			return cnod;
		}

		private bool FLocallyIsomorphic(List<int> lstConnected1, List<int> lstConnected2)
		{
			var cnodInMapping1 = 0;

			foreach (var inod in lstConnected1)
			{
				var inodMap = _arinodMap1To2[inod];
				if (inodMap != MapIllegal)
				{
					cnodInMapping1++;
					if (!lstConnected2.Contains(inodMap))
					{
						return false;
					}
					if (_fContextCheck)
					{
						/* Implement edge isomorphism here */
					}
				}
			}

			if (cnodInMapping1 != GetGroupCountInList(lstConnected2, _vfgr2, Groups.ContainedInMapping))
			{
				return false;
			}
			return true;
		}


		private bool FInOutNew(IEnumerable<int> lstIn1, IEnumerable<int> lstOut1, IEnumerable<int> lstIn2,
			IEnumerable<int> lstOut2)
		{
			if (!fnCmp(
				GetGroupCountInList(lstIn1, _vfgr1, Groups.FromMapping),
				GetGroupCountInList(lstIn2, _vfgr2, Groups.FromMapping)))
			{
				return false;
			}

			if (!fnCmp(
				GetGroupCountInList(lstOut1, _vfgr1, Groups.FromMapping),
				GetGroupCountInList(lstOut2, _vfgr2, Groups.FromMapping)))
			{
				return false;
			}

			if (!fnCmp(
				GetGroupCountInList(lstIn1, _vfgr1, Groups.ToMapping),
				GetGroupCountInList(lstIn2, _vfgr2, Groups.ToMapping)))
			{
				return false;
			}

			if (!fnCmp(
				GetGroupCountInList(lstOut1, _vfgr1, Groups.ToMapping),
				GetGroupCountInList(lstOut2, _vfgr2, Groups.ToMapping)))
			{
				return false;
			}

			if (!fnCmp(
				GetGroupCountInList(lstOut1, _vfgr1, Groups.Disconnected),
				GetGroupCountInList(lstOut2, _vfgr2, Groups.Disconnected)))
			{
				return false;
			}

			if (!fnCmp(
				GetGroupCountInList(lstIn1, _vfgr1, Groups.Disconnected),
				GetGroupCountInList(lstIn2, _vfgr2, Groups.Disconnected)))
			{
				return false;
			}

			return true;
		}

		private bool FFeasible(Match mtc)
		{
			var inod1 = mtc.Inod1;
			var inod2 = mtc.Inod2;

			if (_fContextCheck)
			{
				var icc1 = _vfgr1.GetAttr(inod1) as IContextCheck;
				var icc2 = _vfgr2.GetAttr(inod2) as IContextCheck;

				if (icc1 != null && icc2 != null)
				{
					if (!icc1.FCompatible(icc2))
					{
						return false;
					}
				}
			}

			var lstIn1 = _vfgr1.InNeighbors(inod1);
			var lstIn2 = _vfgr2.InNeighbors(inod2);
			var lstOut1 = _vfgr1.OutNeighbors(inod1);
			var lstOut2 = _vfgr2.OutNeighbors(inod2);

			// In Neighbors in mapping must map to In Neighbors...

			if (!FLocallyIsomorphic(lstIn1, lstIn2))
			{
				return false;
			}

			// Ditto the above for out neighbors...

			if (!FLocallyIsomorphic(lstOut1, lstOut2))
			{
				return false;
			}

			// Verify the in, out and new predicate

			if (!FInOutNew(lstOut1, lstIn1, lstOut2, lstIn2))
			{
				return false;
			}

			return true;
		}
		#endregion

		#region State Change/Restoral
		private List<int> GetList(List<int> lstOfNodes, VfGraph vfgr, Groups grp)
		{
			var lstRet = new List<int>();

			foreach (var inod in lstOfNodes)
			{
				if (((int) vfgr.GetGroup(inod) & (int) grp) != 0)
				{
					lstRet.Add(inod);
				}
			}

			return lstRet;
		}

		private bool FAddMatchToSolution(Match mtc, BacktrackRecord btr)
		{
			var inod1 = mtc.Inod1;
			var inod2 = mtc.Inod2;

			btr.SetMatch(mtc.Inod1, mtc.Inod2, this);

			var lstIn1 = _vfgr1.InNeighbors(inod1);
			var lstIn2 = _vfgr2.InNeighbors(inod2);
			var lstOut1 = _vfgr1.OutNeighbors(inod1);
			var lstOut2 = _vfgr2.OutNeighbors(inod2);

			foreach (var inod in lstOut1)
			{
				if (((int) _vfgr1.GetGroup(inod) & (int) (Groups.Disconnected | Groups.ToMapping)) != 0)
				{
					btr.MoveToGroup(1, inod, Groups.FromMapping, this);
				}
			}
			foreach (var inod in lstIn1)
			{
				if (((int) _vfgr1.GetGroup(inod) & (int) (Groups.Disconnected | Groups.FromMapping)) != 0)
				{
					btr.MoveToGroup(1, inod, Groups.ToMapping, this);
				}
			}
			foreach (var inod in lstOut2)
			{
				if (((int) _vfgr2.GetGroup(inod) & (int) (Groups.Disconnected | Groups.ToMapping)) != 0)
				{
					btr.MoveToGroup(2, inod, Groups.FromMapping, this);
				}
			}
			foreach (var inod in lstIn2)
			{
				if (((int) _vfgr2.GetGroup(inod) & (int) (Groups.Disconnected | Groups.FromMapping)) != 0)
				{
					btr.MoveToGroup(2, inod, Groups.ToMapping, this);
				}
			}

			if (!fnCmp(_outDegreeTotal1, _outDegreeTotal2) ||
			    !fnCmp(_inDegreeTotal1, _inDegreeTotal2))
			{
				return false;
			}
			return fnCmp(lstOut1.Count, lstOut2.Count) && fnCmp(lstIn1.Count, lstIn2.Count);
		}

		internal void MakeMove(int iGraph, int inod, Groups grpNew)
		{
			// Moves to the mapping are handled by the mapping arrays and aren't handled here.

			VfGraph vfg;
			SortedListNoKey<int> lstDisconnected;

			if (iGraph == 1)
			{
				vfg = Vfgr1;
				lstDisconnected = LstDisconnected1;
			}
			else
			{
				vfg = Vfgr2;
				lstDisconnected = LstDisconnected2;
			}

			var igrpCur = (int) vfg.GetGroup(inod);
			var igrpNew = (int) grpNew;

			var igrpRemove = igrpCur & ~igrpNew;
			var igrpAdd = igrpNew & ~igrpCur;

			if (igrpRemove != 0)
			{
				if ((igrpRemove & (int) Groups.Disconnected) != 0)
				{
					lstDisconnected.Delete(inod);
				}
				if ((igrpRemove & (int) Groups.FromMapping) != 0)
				{
					if (iGraph == 1)
					{
						_lstOut1.Delete(inod);
						_outDegreeTotal1--;
					}
					else
					{
						_lstOut2.Delete(inod);
						_outDegreeTotal2--;
					}
				}
				if ((igrpRemove & (int) Groups.ToMapping) != 0)
				{
					if (iGraph == 1)
					{
						_lstIn1.Delete(inod);
						_inDegreeTotal1--;
					}
					else
					{
						_lstIn2.Delete(inod);
						_inDegreeTotal2--;
					}
				}
			}
			if (igrpAdd != 0)
			{
				if ((igrpAdd & (int) Groups.Disconnected) != 0)
				{
					lstDisconnected.Add(inod);
				}
				if ((igrpAdd & (int) Groups.FromMapping) != 0)
				{
					if (iGraph == 1)
					{
						_lstOut1.Add(inod);
						_outDegreeTotal1++;
					}
					else
					{
						_lstOut2.Add(inod);
						_outDegreeTotal2++;
					}
				}
				if ((igrpAdd & (int) Groups.ToMapping) != 0)
				{
					if (iGraph == 1)
					{
						_lstIn1.Add(inod);
						_inDegreeTotal1++;
					}
					else
					{
						_lstIn2.Add(inod);
						_inDegreeTotal2++;
					}
				}
			}
			vfg.SetGroup(inod, grpNew);
		}
		#endregion

		#region NUNIT Testing
#if NUNIT
		[TestFixture]
		public class VfGraphTester
		{
			private Graph VfsTestGraph1()
			{
				var graph = new Graph();
				Assert.AreEqual(0, graph.InsertNode());
				Assert.AreEqual(1, graph.InsertNode());
				Assert.AreEqual(2, graph.InsertNode());
				Assert.AreEqual(3, graph.InsertNode());
				Assert.AreEqual(4, graph.InsertNode());
				Assert.AreEqual(5, graph.InsertNode());
				// Circular graph with "extra" edge at (0,3)
				graph.InsertEdge(0, 1);
				graph.InsertEdge(1, 2);
				graph.InsertEdge(2, 3);
				graph.InsertEdge(3, 4);
				graph.InsertEdge(4, 5);
				graph.InsertEdge(5, 0);
				graph.InsertEdge(0, 3);

				return graph;
			}

			private Graph VfsTestGraph2()
			{
				var graph = new Graph();
				Assert.AreEqual(0, graph.InsertNode());
				Assert.AreEqual(1, graph.InsertNode());
				Assert.AreEqual(2, graph.InsertNode());
				Assert.AreEqual(3, graph.InsertNode());
				Assert.AreEqual(4, graph.InsertNode());
				Assert.AreEqual(5, graph.InsertNode());
				// Same graph in reverse order with slightly offset "extra" edge at (4,1)
				graph.InsertEdge(1, 0);
				graph.InsertEdge(2, 1);
				graph.InsertEdge(3, 2);
				graph.InsertEdge(4, 3);
				graph.InsertEdge(5, 4);
				graph.InsertEdge(0, 5);
				graph.InsertEdge(1, 4);

				return graph;
			}

			private VfState VfsTest()
			{
				return new VfState(VfsTestGraph1(), VfsTestGraph2());
			}

			[Test]
			public void TestMultipleMatches()
			{
				var gr1 = new Graph();
				var gr2 = new Graph();
				VfState vfs;

				gr1.InsertNodes(3);
				gr2.InsertNodes(3);
				gr1.InsertEdge(0, 1);
				gr1.InsertEdge(1, 2);
				gr1.InsertEdge(2, 0);
				gr2.InsertEdge(0, 2);
				gr2.InsertEdge(2, 1);
				gr2.InsertEdge(1, 0);

				vfs = new VfState(gr1, gr2, false, false, true);
				Assert.IsTrue(vfs.FMatch());
				Assert.AreEqual(3, vfs.Mappings.Count);

				vfs = new VfState(gr1, gr2, true, false, true);
				Assert.IsTrue(vfs.FMatch());
				Assert.AreEqual(3, vfs.Mappings.Count);

				gr2.InsertEdge(0, 1);
				gr2.InsertEdge(1, 2);
				gr2.InsertEdge(2, 0);
				gr1.InsertEdge(0, 2);
				gr1.InsertEdge(2, 1);
				gr1.InsertEdge(1, 0);

				vfs = new VfState(gr1, gr2, false, false, true);
				Assert.IsTrue(vfs.FMatch());
				Assert.AreEqual(6, vfs.Mappings.Count);

				vfs = new VfState(gr1, gr2, true, false, true);
				Assert.IsTrue(vfs.FMatch());
				Assert.AreEqual(6, vfs.Mappings.Count);
			}

			[Test]
			public void TestConstructor()
			{
				Assert.IsNotNull(VfsTest());
			}

			[Test]
			public void TestFCompleteMatch()
			{
				Assert.IsFalse(VfsTest().FCompleteMatch());
			}

			[Test]
			public void TestMakeMove()
			{
				var vfs = VfsTest();
				vfs.MakeMove(1, 0, Groups.FromMapping);
				Assert.AreEqual(1, vfs._lstOut1.Count);
				Assert.AreEqual(5, vfs._lstDisconnected1.Count);
				Assert.AreEqual(Groups.FromMapping, vfs.Vfgr1.GetGroup(0));
			}

			[Test]
			public void TestSetMapping()
			{
				var vfs = VfsTest();
				vfs.SetMapping(0, 1);
				Assert.AreEqual(1, vfs._arinodMap1To2[0]);
				Assert.AreEqual(0, vfs._arinodMap2To1[1]);
			}

			[Test]
			public void TestMatch()
			{
				var gr1 = new Graph();
				var gr2 = new Graph();

				var vfs = new VfState(gr1, gr2);
				Assert.IsTrue(vfs.FMatch());
				gr2.InsertNode();
				vfs = new VfState(gr1, gr2);
				Assert.IsFalse(vfs.FMatch());
				gr1.InsertNode();
				vfs = new VfState(gr1, gr2);
				Assert.IsTrue(vfs.FMatch());
				gr1.InsertNode();
				vfs = new VfState(gr1, gr2);
				Assert.IsTrue(vfs.FMatch());
				gr1.InsertEdge(0, 1);
				vfs = new VfState(gr1, gr2);
				Assert.IsTrue(vfs.FMatch());
				vfs = new VfState(gr2, gr1);
				Assert.IsFalse(vfs.FMatch());
			}

			[Test]
			public void TestMatchComplex()
			{
				var vfs = VfsTest();
				Assert.IsTrue(vfs.FMatch());
				var graph2 = VfsTestGraph2();
				graph2.DeleteEdge(1, 4);
				graph2.InsertEdge(2, 4);
				vfs = new VfState(VfsTestGraph1(), graph2);
				Assert.IsFalse(vfs.FMatch());

				var graph1 = new Graph();
				graph1.InsertNodes(11);
				graph2 = new Graph();
				graph2.InsertNodes(11);

				graph1.InsertEdge(0, 2);
				graph1.InsertEdge(0, 1);
				graph1.InsertEdge(2, 4);
				graph1.InsertEdge(2, 5);
				graph1.InsertEdge(1, 5);
				graph1.InsertEdge(1, 3);
				graph1.InsertEdge(4, 7);
				graph1.InsertEdge(5, 7);
				graph1.InsertEdge(5, 8);
				graph1.InsertEdge(5, 6);
				graph1.InsertEdge(3, 6);
				graph1.InsertEdge(7, 10);
				graph1.InsertEdge(6, 9);
				graph1.InsertEdge(10, 8);
				graph1.InsertEdge(8, 9);

				graph2.InsertEdge(0, 1);
				graph2.InsertEdge(0, 9);
				graph2.InsertEdge(1, 2);
				graph2.InsertEdge(1, 10);
				graph2.InsertEdge(9, 10);
				graph2.InsertEdge(9, 8);
				graph2.InsertEdge(2, 3);
				graph2.InsertEdge(10, 3);
				graph2.InsertEdge(10, 5);
				graph2.InsertEdge(10, 7);
				graph2.InsertEdge(8, 7);
				graph2.InsertEdge(3, 4);
				graph2.InsertEdge(7, 6);
				graph2.InsertEdge(4, 5);
				graph2.InsertEdge(5, 6);

				vfs = new VfState(graph1, graph2, true);
				Assert.IsTrue(vfs.FMatch());
			}

			[Test]
			public void TestMatchNonConnected()
			{
				var gr1 = new Graph();
				var gr2 = new Graph();

				gr1.InsertNodes(4);
				gr2.InsertNodes(4);

				gr1.InsertEdge(0, 1);
				gr1.InsertEdge(2, 3);
				gr2.InsertEdge(2, 1);
				gr2.InsertEdge(0, 3);
				var vfs = new VfState(gr1, gr2);
				Assert.IsTrue(vfs.FMatch());
			}

			[Test]
			public void TestMatchSubgraph()
			{
				// Note that "Subgraph" is defined as a graph derived from
				// deleting nodes from another graph.  Thus, the edges
				// between the remaining nodes must match with matches in the
				// original graph.  Adding edges between nodes in a graph does
				// not constitute a supergraph under this definition.

				var gr1 = new Graph();
				var gr2 = new Graph();

				gr1.InsertNodes(4);
				gr2.InsertNodes(3);
				gr1.InsertEdge(0, 1);
				gr1.InsertEdge(2, 0);
				gr1.InsertEdge(3, 0);
				gr1.InsertEdge(2, 3);
				gr2.InsertEdge(0, 1);
				gr2.InsertEdge(2, 0);
				var vfs = new VfState(gr1, gr2);
				Assert.IsTrue(vfs.FMatch());

				gr1 = VfsTestGraph1();
				gr2 = VfsTestGraph2();
				vfs = new VfState(gr1, gr2);
				Assert.IsTrue(vfs.FMatch());
				gr1.InsertNode();
				gr1.InsertEdge(6, 3);
				gr1.InsertEdge(6, 5);

				// Graph 2 is isomorphic to a subgraph of graph 1 (default check is for
				// subgraph isomorphism).
				vfs = new VfState(gr1, gr2);
				Assert.IsTrue(vfs.FMatch());

				// The converse is false
				vfs = new VfState(gr2, gr1);
				Assert.IsFalse(vfs.FMatch());

				// The two graphs are subgraph ismorphic but not ismorphic
				vfs = new VfState(gr1, gr2, true /* fIsomorphism */);
				Assert.IsFalse(vfs.FMatch());
			}

			[Test]
			public void TestAutomorphic()
			{
				int cRows = 10, cCols = 10;
				var graph1 = new Graph();

				graph1.InsertNodes(cRows * cCols);
				for (var iRow = 0; iRow < cRows - 1; iRow++)
				{
					for (var iCol = 0; iCol < cCols - 1; iCol++)
					{
						var iNode = iCol * cRows + iRow;
						var iNodeToCol = iNode + 1;
						var iNodeToRow = iNode + cRows;

						graph1.InsertEdge(iNode, iNodeToCol);
						graph1.InsertEdge(iNode, iNodeToRow);
						graph1.InsertEdge(iNodeToCol, iNode);
						graph1.InsertEdge(iNodeToRow, iNode);
					}
				}
				var graph2 = graph1.IsomorphicShuffling(new Random(102));
				// Insert this and you'll wait a LONG time for this test to finish...
				// Note - the above is no longer true now that we have Degree compatibility check
				// graph1.InsertEdge(0, cRows * cCols - 1);

				var vfs = new VfState(graph1, graph2, true);
				Assert.IsTrue(vfs.FMatch());
			}

			[Test]
			public void TestPermutationMassage()
			{
				var graph1 = new Graph();
				graph1.InsertNodes(4);
				graph1.InsertEdge(2, 3);

				var graph2 = new Graph();
				graph2.InsertNodes(2);
				graph2.InsertEdge(1, 0);

				var vfs = new VfState(graph1, graph2);
				Assert.IsTrue(vfs.FMatch());
				var arprm1To2 = vfs.Mapping1To2;
				var arprm2To1 = vfs.Mapping2To1;
				Assert.AreEqual(1, arprm1To2[2]);
				Assert.AreEqual(0, arprm1To2[3]);
				Assert.AreEqual(MapIllegal, arprm1To2[0]);
				Assert.AreEqual(MapIllegal, arprm1To2[1]);
				Assert.AreEqual(3, arprm2To1[0]);
				Assert.AreEqual(2, arprm2To1[1]);
			}

			private class NodeColor : IContextCheck
			{
				private readonly string _strColor;

				public NodeColor(string strColor)
				{
					_strColor = strColor;
				}

				public bool FCompatible(IContextCheck icc)
				{
					return ((NodeColor) icc)._strColor == _strColor;
				}
			}

			[Test]
			public void TestContextCheck()
			{
				var graph1 = new Graph();
				var graph2 = new Graph();

				graph1.InsertNode(new NodeColor("Blue"));
				graph1.InsertNode(new NodeColor("Red"));
				graph1.InsertNode(new NodeColor("Red"));
				graph1.InsertNode(new NodeColor("Red"));
				graph1.InsertNode(new NodeColor("Red"));
				graph1.InsertEdge(0, 1);
				graph1.InsertEdge(1, 2);
				graph1.InsertEdge(2, 3);
				graph1.InsertEdge(3, 4);
				graph1.InsertEdge(4, 0);

				graph2.InsertNode(new NodeColor("Red"));
				graph2.InsertNode(new NodeColor("Red"));
				graph2.InsertNode(new NodeColor("Red"));
				graph2.InsertNode(new NodeColor("Blue"));
				graph2.InsertNode(new NodeColor("Red"));
				graph2.InsertEdge(0, 1);
				graph2.InsertEdge(1, 2);
				graph2.InsertEdge(2, 3);
				graph2.InsertEdge(3, 4);
				graph2.InsertEdge(4, 0);

				var vfs = new VfState(graph1, graph2, true);
				Assert.IsTrue(vfs.FMatch());
				var mpMatch = vfs.Mapping1To2;
				// With no context checking, vertex 0 in the first graph can match
				// vertex 0 in the second graph
				Assert.AreEqual(0, mpMatch[0]);

				vfs = new VfState(graph1, graph2, true, true);
				Assert.IsTrue(vfs.FMatch());
				mpMatch = vfs.Mapping1To2;
				// With context checking, Blue in first circular graph has to map to blue
				// in second circular graph.
				Assert.AreEqual(3, mpMatch[0]);
			}

			[Test]
			public void TestRandomly()
			{
				var rg = new RandomGraph(0.3, 4000 /* seed */);
				Graph graph1, graph2;
				VfState vfs;

				for (var i = 0; i < 10; i++)
				{
					rg.IsomorphicPair(100, out graph1, out graph2);
					vfs = new VfState(graph1, graph2);
					Assert.IsTrue(vfs.FMatch());
				}

				graph1 = rg.GetGraph(100);
				graph2 = rg.GetGraph(100);
				vfs = new VfState(graph1, graph2);
				Assert.IsFalse(vfs.FMatch());
			}

#if BIGSLOWTEST
			[Test]
			public void TestRandomlyBig()
			{
				RandomGraph rg = new RandomGraph(0.3, 4000 /* seed */);
				Graph graph1, graph2;
				VfState vfs;

				// Vast majority of time spent here looking for 299,000 edges
				// in Isomorphic Pair...
				rg.IsomorphicPair(1000, out graph1, out graph2);

				// The actual match took less than 3 seconds on my machine
				vfs = new VfState(graph1, graph2);
				Assert.IsTrue(vfs.FMatch());
			}
#endif
#if PERFORMANCETEST
			[Test]
			public void TestRandomlyBigPerf()
			{
				RandomGraph rg = new RandomGraph(0.025, 7000 /* seed */);
				Graph graph1, graph2;
				VfState vfs;

				rg.IsomorphicPair(4000, out graph1, out graph2);

				//Object objDummy;
				//int iTo = graph2.GetOutEdge(0, 0, out objDummy);
				//graph2.DeleteEdge(0, iTo);

				int totalTicks = 0;
				int cTests = 20;
				for (int i = 0; i < cTests; i++)
                {
					vfs = new VfState(graph1, graph2);
					int starttime = System.Environment.TickCount;
					vfs.FMatch();
					totalTicks += System.Environment.TickCount - starttime;
                }
				Console.WriteLine("Total seconds = {0}", totalTicks / (cTests * 1000.0));
			}
#endif
		}

#endif
		#endregion
	}
}