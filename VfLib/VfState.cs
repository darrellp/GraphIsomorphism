//#define GATHERSTATS
//#define BIGSLOWTEST
//#define PERFORMANCETEST

using System;
using System.Collections.Generic;
using System.Linq;
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
		private readonly int[] _isomorphism1To2;
		private readonly int[] _isomorphism2To1;

		// All the mappings we've located thus far...
		private readonly List<FullMapping> _lstMappings = new List<FullMapping>();

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
			_armpNid1Nid2 = new int[_isomorphism1To2.Length];
			_armpNid2Nid1 = new int[_isomorphism2To1.Length];

			for (var i = 0; i < _isomorphism1To2.Length; i++)
			{
				var inodMap = _isomorphism1To2[armpInodGraphInodVf1[i]];
				_armpNid1Nid2[i] = (inodMap == MapIllegal ? MapIllegal : _ldr2.IdFromPos(_armpInodVfInodGraph2[inodMap]));
			}

			for (var i = 0; i < _isomorphism2To1.Length; i++)
			{
				// Shouldn't be any map_illegal values in the second graph's array
				_armpNid2Nid1[i] = _ldr1.IdFromPos(_armpInodVfInodGraph1[_isomorphism2To1[armpInodGraphInodVf2[i]]]);
			}
		}

		private void MassagePermutations(
			int[] arinodMap1To2, int[] arinodMap2To1,
			int[] armpInodGraphInodVf1, int[] armpInodGraphInodVf2,
			ref int[] armpNid1Nid2, ref int[] armpNid2Nid1)
		{
			if (armpNid2Nid1 == null)
			{
				throw new ArgumentNullException("armpNid2Nid1");
			}
			if (armpNid1Nid2 == null)
			{
				throw new ArgumentNullException("armpNid1Nid2");
			}
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
			var count1 = _isomorphism1To2.Length;
			var count2 = _isomorphism2To1.Length;

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

		// The two graphs we're comparing
		internal VfGraph Vfgr2 { get; set; }
		internal VfGraph Vfgr1 { get; set; }

		// Lists of nodes not yet participating in the current isomorphism
		// These lists are sorted on index but since the original nodes were
		// sorted by degree, that means that these are also sorted by degree
		// which is a key performance heuristic.


		// Nodes that point into the current isomorphism set without participating in it
		internal SortedListNoValue<int> LstIn1 { get; set; }
		internal SortedListNoValue<int> LstIn2 { get; set; }

		// Nodes that point out of the current isomorphism set without participating in it
		internal SortedListNoValue<int> LstOut1 { get; set; }
		internal SortedListNoValue<int> LstOut2 { get; set; }

		// Nodes that are not connected with any node in the current isomorphism candidate
		internal SortedListNoValue<int> LstDisconnected1 { get; set; }
		internal SortedListNoValue<int> LstDisconnected2 { get; set; }
		#endregion

		#region Delegates
		internal readonly Func<int, int, bool> FnCompareDegrees;

		// In a straight isomorphism degrees of matching nodes must match exactly
		private static bool CmpIsomorphism(int degree1, int degree2)
		{
			return degree1 == degree2;
		}

		// In a subgraph isomorphism, nodes in the original graph may have lower degree
		// than nodes in the subgraph since the subgraph is embedded in the "supergraph"
		// which may have nodes connecting to the subgraph increasing the degree of nodes
		// in the subgraph.
		private static bool CmpSubgraphIsomorphism(int degreeSubgraph, int degreeOriginal)
		{
			return degreeSubgraph >= degreeOriginal;
		}
		#endregion

		#region Constructors
		public VfState(IGraphLoader loader1, IGraphLoader loader2, bool fIsomorphism = false, bool fContextCheck = false, bool fFindAll = false)
		{
			LstIn1 = new SortedListNoValue<int>();
			LstOut1 = new SortedListNoValue<int>();
			LstIn2 = new SortedListNoValue<int>();
			LstOut2 = new SortedListNoValue<int>();
			LstDisconnected2 = new SortedListNoValue<int>();
			LstDisconnected1 = new SortedListNoValue<int>();
			_ldr1 = loader1;
			_ldr2 = loader2;

			_fContextCheck = fContextCheck;
			_fFindAll = fFindAll;

			if (fIsomorphism)
			{
				FnCompareDegrees = CmpIsomorphism;
			}
			else
			{
				FnCompareDegrees = CmpSubgraphIsomorphism;
			}

			_armpInodVfInodGraph1 = new CmpNodeDegrees(loader1).Permutation;
			_armpInodVfInodGraph2 = new CmpNodeDegrees(loader2).Permutation;
			Vfgr1 = new VfGraph(loader1, _armpInodVfInodGraph1);
			Vfgr2 = new VfGraph(loader2, _armpInodVfInodGraph2);
			_isomorphism1To2 = new int[loader1.NodeCount];
			_isomorphism2To1 = new int[loader2.NodeCount];
			for (var i = 0; i < loader1.NodeCount; i++)
			{
				_isomorphism1To2[i] = MapIllegal;
				LstDisconnected1.Add(i);
			}
			for (var i = 0; i < loader2.NodeCount; i++)
			{
				_isomorphism2To1[i] = MapIllegal;
				LstDisconnected2.Add(i);
			}
		}
		#endregion

		#region Matching
		private bool FCompleteMatch()
		{
			return LstDisconnected2.Count == 0 && LstIn2.Count == 0 && LstOut2.Count == 0;
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
			if (!FnCompareDegrees(Vfgr1.NodeCount, Vfgr2.NodeCount))
			{
				return false;
			}

			for (var iNode = 0; iNode < Vfgr2.NodeCount; iNode++)
			{
				if (!FnCompareDegrees(Vfgr1.TotalDegree(iNode), Vfgr2.TotalDegree(iNode)))
				{
					return false;
				}
			}
			return true;
		}

		private void RecordCurrentMatch()
		{
			_lstMappings.Add(new FullMapping(_isomorphism1To2, _isomorphism2To1));
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
			if (!FnCompareDegrees(Vfgr1.NodeCount, Vfgr2.NodeCount))
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
				CandidateFinder cf;
				BacktrackRecord btr;
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
				Match mchCur;
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
		internal void SetIsomorphic(int inod1, int inod2)
		{
			// Set the mapping in both directions
			_isomorphism1To2[inod1] = inod2;
			_isomorphism2To1[inod2] = inod1;
		}

		internal void RemoveFromMappingList(int iGraph, int inod)
		{
			var mp = iGraph == 1 ? _isomorphism1To2 : _isomorphism2To1;
			mp[inod] = MapIllegal;
		}
		#endregion

		#region Feasibility
		private int GetGroupCountInList(IEnumerable<int> lstOfNodes, VfGraph vfgr, Group grp)
		{
			return lstOfNodes.Count(inod => ((int) vfgr.GetGroup(inod) & (int) grp) != 0);
		}

// ReSharper disable once ParameterTypeCanBeEnumerable.Local
		private bool FLocallyIsomorphic(List<int> lstConnected1, List<int> lstConnected2)
		{
			var cnodInMapping1 = 0;

			foreach (var inodMap in lstConnected1.Select(inod => _isomorphism1To2[inod]).Where(inodMap => inodMap != MapIllegal))
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

			if (cnodInMapping1 != GetGroupCountInList(lstConnected2, Vfgr2, Group.ContainedInMapping))
			{
				return false;
			}
			return true;
		}


		private bool FInOutNew(IEnumerable<int> lstIn1, IEnumerable<int> lstOut1, IEnumerable<int> lstIn2,
			IEnumerable<int> lstOut2)
		{
			var nodesIn1 = lstIn1 as List<int> ?? lstIn1.ToList();
			var nodesIn2 = lstIn2 as List<int> ?? lstIn2.ToList();
			if (!FnCompareDegrees(
				GetGroupCountInList(nodesIn1, Vfgr1, Group.FromMapping),
				GetGroupCountInList(nodesIn2, Vfgr2, Group.FromMapping)))
			{
				return false;
			}

			var nodesOut1 = lstOut1 as List<int> ?? lstOut1.ToList();
			var nodesOut2 = lstOut2 as List<int> ?? lstOut2.ToList();
			if (!FnCompareDegrees(
				GetGroupCountInList(nodesOut1, Vfgr1, Group.FromMapping),
				GetGroupCountInList(nodesOut2, Vfgr2, Group.FromMapping)))
			{
				return false;
			}

			if (!FnCompareDegrees(
				GetGroupCountInList(nodesIn1, Vfgr1, Group.ToMapping),
				GetGroupCountInList(nodesIn2, Vfgr2, Group.ToMapping)))
			{
				return false;
			}

			if (!FnCompareDegrees(
				GetGroupCountInList(nodesOut1, Vfgr1, Group.ToMapping),
				GetGroupCountInList(nodesOut2, Vfgr2, Group.ToMapping)))
			{
				return false;
			}

			if (!FnCompareDegrees(
				GetGroupCountInList(nodesOut1, Vfgr1, Group.Disconnected),
				GetGroupCountInList(nodesOut2, Vfgr2, Group.Disconnected)))
			{
				return false;
			}

			if (!FnCompareDegrees(
				GetGroupCountInList(nodesIn1, Vfgr1, Group.Disconnected),
				GetGroupCountInList(nodesIn2, Vfgr2, Group.Disconnected)))
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
				var icc1 = Vfgr1.GetAttr(inod1) as IContextCheck;
				var icc2 = Vfgr2.GetAttr(inod2) as IContextCheck;

				if (icc1 != null && icc2 != null)
				{
					if (!icc1.FCompatible(icc2))
					{
						return false;
					}
				}
			}

			var lstIn1 = Vfgr1.InNeighbors(inod1);
			var lstIn2 = Vfgr2.InNeighbors(inod2);
			var lstOut1 = Vfgr1.OutNeighbors(inod1);
			var lstOut2 = Vfgr2.OutNeighbors(inod2);

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
// ReSharper disable once UnusedMember.Local
		private List<int> GetList(IEnumerable<int> lstOfNodes, VfGraph vfgr, Group grp)
		{
			return lstOfNodes.Where(inod => ((int) vfgr.GetGroup(inod) & (int) grp) != 0).ToList();
		}

		private bool FAddMatchToSolution(Match mtc, BacktrackRecord btr)
		{
			var inod1 = mtc.Inod1;
			var inod2 = mtc.Inod2;

			btr.SetMatch(mtc.Inod1, mtc.Inod2, this);

			var lstIn1 = Vfgr1.InNeighbors(inod1);
			var lstIn2 = Vfgr2.InNeighbors(inod2);
			var lstOut1 = Vfgr1.OutNeighbors(inod1);
			var lstOut2 = Vfgr2.OutNeighbors(inod2);

			foreach (var inod in lstOut1)
			{
				if (((int) Vfgr1.GetGroup(inod) & (int) (Group.Disconnected | Group.ToMapping)) != 0)
				{
					btr.MoveToGroup(1, inod, Group.FromMapping, this);
				}
			}
			foreach (var inod in lstIn1)
			{
				if (((int) Vfgr1.GetGroup(inod) & (int) (Group.Disconnected | Group.FromMapping)) != 0)
				{
					btr.MoveToGroup(1, inod, Group.ToMapping, this);
				}
			}
			foreach (var inod in lstOut2)
			{
				if (((int) Vfgr2.GetGroup(inod) & (int) (Group.Disconnected | Group.ToMapping)) != 0)
				{
					btr.MoveToGroup(2, inod, Group.FromMapping, this);
				}
			}
			foreach (var inod in lstIn2)
			{
				if (((int) Vfgr2.GetGroup(inod) & (int) (Group.Disconnected | Group.FromMapping)) != 0)
				{
					btr.MoveToGroup(2, inod, Group.ToMapping, this);
				}
			}

			if (!FnCompareDegrees(_outDegreeTotal1, _outDegreeTotal2) ||
			    !FnCompareDegrees(_inDegreeTotal1, _inDegreeTotal2))
			{
				return false;
			}
			return FnCompareDegrees(lstOut1.Count, lstOut2.Count) && FnCompareDegrees(lstIn1.Count, lstIn2.Count);
		}

		void AddToInDegree(int iGraph, int increment)
		{
			if (iGraph == 1)
			{
				_inDegreeTotal1 = _inDegreeTotal1 + increment;
			}
			else
			{
				_inDegreeTotal2 = _inDegreeTotal2 + increment;
			}
		}

		void AddToOutDegree(int iGraph, int increment)
		{
			if (iGraph == 1)
			{
				_outDegreeTotal1 = _outDegreeTotal1 + increment;
			}
			else
			{
				_outDegreeTotal2 = _outDegreeTotal2 + increment;
			}
		}

		/// <summary>
		/// Reclassify a node into a new group
		/// </summary>
		/// <param name="iGraph">Graph the node is being reclassified in</param>
		/// <param name="inod">Node to reclassify</param>
		/// <param name="grpNew">New classification for the node</param>
		internal void MakeMove(int iGraph, int inod, Group grpNew)
		{
			// Moves to the mapping are handled by the mapping arrays and aren't handled here.

			VfGraph vfg;
			SortedListNoValue<int> disconnectedList, outList, inList;

			if (iGraph == 1)
			{
				vfg = Vfgr1;
				disconnectedList = LstDisconnected1;
				outList = LstOut1;
				inList = LstIn1;
			}
			else
			{
				vfg = Vfgr2;
				disconnectedList = LstDisconnected2;
				outList = LstOut2;
				inList = LstIn2;
			}

			// Old groups and new groups
			var igrpCur = (int) vfg.GetGroup(inod);
			var igrpNew = (int) grpNew;

			// Groups to add to and remove from
			var igrpRemove = igrpCur & ~igrpNew;
			var igrpAdd = igrpNew & ~igrpCur;

			if (igrpRemove != 0)
			{
				// Do we need to remove from the disconnect list?
				if ((igrpRemove & (int) Group.Disconnected) != 0)
				{
					disconnectedList.Delete(inod);
				}

				// If we're no longer mapped from a node in the isomorphism
				if ((igrpRemove & (int) Group.FromMapping) != 0)
				{
					// Remove us from out list and decrement total out degree
					outList.Delete(inod);
					AddToOutDegree(iGraph, -1);
				}
				// If we're no longer mapped into a node in the isomorphism
				if ((igrpRemove & (int)Group.ToMapping) != 0)
				{
					// Remove us from in list and decrement total out degree
					inList.Delete(inod);
					AddToInDegree(iGraph, -1);
				}
			}
			if (igrpAdd != 0)
			{
				// Add us to the disconnected group if necessary
				if ((igrpAdd & (int) Group.Disconnected) != 0)
				{
					disconnectedList.Add(inod);
				}
				// If we're newly mapped from a node in the isomorphism
				if ((igrpAdd & (int)Group.FromMapping) != 0)
				{
					// Add us to the out list and increment total out degree
					outList.Add(inod);
					AddToOutDegree(iGraph, 1);
				}
				// If we're newly mapped into a node in the isomorphism
				if ((igrpAdd & (int) Group.ToMapping) != 0)
				{
					// Add us to the in list and increment total in degree
					inList.Add(inod);
					AddToInDegree(iGraph, 1);
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

				gr1.InsertNodes(3);
				gr2.InsertNodes(3);
				gr1.InsertEdge(0, 1);
				gr1.InsertEdge(1, 2);
				gr1.InsertEdge(2, 0);
				gr2.InsertEdge(0, 2);
				gr2.InsertEdge(2, 1);
				gr2.InsertEdge(1, 0);

				var vfs = new VfState(gr1, gr2, false, false, true);
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
				vfs.MakeMove(1, 0, Group.FromMapping);
				Assert.AreEqual(1, vfs.LstOut1.Count);
				Assert.AreEqual(5, vfs.LstDisconnected1.Count);
				Assert.AreEqual(Group.FromMapping, vfs.Vfgr1.GetGroup(0));
			}

			[Test]
			public void TestSetMapping()
			{
				var vfs = VfsTest();
				vfs.SetIsomorphic(0, 1);
				Assert.AreEqual(1, vfs._isomorphism1To2[0]);
				Assert.AreEqual(0, vfs._isomorphism2To1[1]);
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
				const int cRows = 10;
				const int cCols = 10;
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