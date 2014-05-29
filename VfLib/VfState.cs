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

	public class VfState<TAttr>
	{
		#region Private Variables
		internal const int MapIllegal = -1;

		// Mapping between node positions in VfGraph and the positions in the original Graph.
		// This is just the permutation to sort the original graph nodes by degrees.
		private readonly Dictionary<int, int> _degreeSortedToOriginal1;
		private readonly Dictionary<int, int> _degreeSortedToOriginal2;

		// The original ILoader's - needed to map back the permutation
		// to the original node id's after the match
		private readonly IGraphLoader<TAttr> _ldr1;
		private readonly IGraphLoader<TAttr> _ldr2;

		// The actual mappings we're building up
		private readonly Dictionary<int, int> _vfGraphInode1To2Isomorphism;
		private readonly Dictionary<int, int> _vfGraphInode2To1Isomorphism;

		private int _inDegreeTotal1, _inDegreeTotal2, _outDegreeTotal1, _outDegreeTotal2;

		private bool _fMatched; // Have we attempted a match?
		private readonly bool _fContextCheck; // Do we context check using the attributes?
		#endregion

		#region Properties
		/// <summary>
		/// Turn VfGraph to VfGraph Inode mapping into Graph to Graph nid mapping
		/// </summary>
		/// <param name="isomorphism1To2">The VfGraph to VfGraph mapping</param>
		/// <param name="degreeSortedToOriginal1">VfGraph1 Inode to Graph1 Inode mapping</param>
		/// <param name="degreeSortedToOriginal2">VfGraph2 Inode to Graph2 Inode mapping</param>
		/// <param name="isomorphismNid1ToNid2">Returned Graph1 nid to Graph2 nid mapping</param>
		/// <param name="isomorphismNid2ToNid1">Returned Graph2 nid to Graph1 nid mapping</param>
		private void VfGraphVfGraphInodeToGraphGraphNid(
			Dictionary<int, int> isomorphism1To2,
			Dictionary<int, int> degreeSortedToOriginal1,
			Dictionary<int, int> degreeSortedToOriginal2,
			out Dictionary<int, int> isomorphismNid1ToNid2,
			out Dictionary<int, int> isomorphismNid2ToNid1)
		{
			// Holding areas for new permutations
			isomorphismNid1ToNid2 = new Dictionary<int, int>();
			isomorphismNid2ToNid1 = new Dictionary<int, int>();
			foreach (var pair in isomorphism1To2)
			{
				var vfInod2 = pair.Value;
				var inode1 = degreeSortedToOriginal1[pair.Key];
				var nid1 = _ldr1.IdFromPos(inode1);
				if (vfInod2 == MapIllegal)
				{
					isomorphismNid1ToNid2[nid1] = MapIllegal;
					break;
				}
				var inode2 = degreeSortedToOriginal2[vfInod2];
				var nid2 = _ldr2.IdFromPos(inode2);
				isomorphismNid1ToNid2[nid1] = nid2;
				isomorphismNid2ToNid1[nid2] = nid1;
			}
		}

		// The two graphs we're comparing
		internal VfGraph<TAttr> Vfgr2 { get; set; }
		internal VfGraph<TAttr> Vfgr1 { get; set; }

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
		// Comparison function depends on whether we're doing full isomorphism
		// or subgraph isomorphism.
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
		public VfState(IGraphLoader<TAttr> loader1, IGraphLoader<TAttr> loader2, bool fIsomorphism = false, bool fContextCheck = false)
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

			if (fIsomorphism)
			{
				// normal isomorphism - degrees must match exactly
				FnCompareDegrees = CmpIsomorphism;
			}
			else
			{
				// Subgraph isomorphism - degrees in graph2 must be less than
				// those of graph1.
				FnCompareDegrees = CmpSubgraphIsomorphism;
			}

			// Node indices in VfGraphs are sorted by vertex degree
			_degreeSortedToOriginal1 = new CmpNodeDegrees<TAttr>(loader1).Permutation;
			_degreeSortedToOriginal2 = new CmpNodeDegrees<TAttr>(loader2).Permutation;
			Vfgr1 = new VfGraph<TAttr>(loader1, _degreeSortedToOriginal1);
			Vfgr2 = new VfGraph<TAttr>(loader2, _degreeSortedToOriginal2);

			// Set up space for isomorphism mappings
			_vfGraphInode1To2Isomorphism = new Dictionary<int, int>(loader1.NodeCount);
			_vfGraphInode2To1Isomorphism = new Dictionary<int, int>(loader2.NodeCount);

			// When we start no isomorphic mappings and all nodes are disconnected
			// from the isomorphism.
			for (var i = 0; i < loader1.NodeCount; i++)
			{
				_vfGraphInode1To2Isomorphism[i] = MapIllegal;
				LstDisconnected1.Add(i);
			}
			for (var i = 0; i < loader2.NodeCount; i++)
			{
				_vfGraphInode2To1Isomorphism[i] = MapIllegal;
				LstDisconnected2.Add(i);
			}
		}
		#endregion

		#region Matching
		// Return true if we've got a completed isomorphism
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
		/// <summary>
		/// Ensure that degrees of vertices are compatible
		/// </summary>
		/// <returns>True if compatible, False if not</returns>
		private bool FCompatibleDegrees()
		{
			if (!FnCompareDegrees(Vfgr1.NodeCount, Vfgr2.NodeCount))
			{
				return false;
			}

			// Since we've sorted the nodes by degree, we can do a quick compare on the degree sequence
			// on a vertex by vertex basis...
			for (var iNode = 0; iNode < Vfgr2.NodeCount; iNode++)
			{
				if (!FnCompareDegrees(Vfgr1.TotalDegree(iNode), Vfgr2.TotalDegree(iNode)))
				{
					return false;
				}
			}
			return true;
		}

		// Find an isomorphism between a subgraph of _vfgr1 and the entirety of _vfgr2...
		public FullMapping Match()
		{
			return Matches().DefaultIfEmpty().First();
		}

		public IEnumerable<FullMapping> Matches()
		{
			// Check for an empty second graph
			if (_ldr2.NodeCount == 0)
			{
				// If the second is empty, check for a successful "match"
				if (FnCompareDegrees(_ldr1.NodeCount, 0))
				{
					// Return empty mapping - successful, but nothing to map to
					yield return new FullMapping(new Dictionary<int, int>(), new Dictionary<int, int>());
				}
				yield break;
			}

			// Quick easy check to make the degrees compatible.
			if (!FCompatibleDegrees())
			{
				yield break;
			}

			var stkcf = new Stack<CandidateFinder<TAttr>>();
			var stkbr = new Stack<BacktrackRecord<TAttr>>();

			var fPopOut = false;
#if GATHERSTATS
			int cSearchGuesses = 0;
			int cBackTracks = 0;
			int cInfeasible = 0;
#endif

			if (_fMatched)
			{
				yield break;
			}
			_fMatched = true;

			// Non-recursive implementation of a formerly recursive function
			while (true)
			{
				CandidateFinder<TAttr> cf;
				BacktrackRecord<TAttr> btr;

				// If it's time to backtrack...
				if (fPopOut)
				{
					// If there are no more candidates left, we've failed to find an isomorphism
					if (stkcf.Count <= 0)
					{
						break; // Out of the top level while loop and return false
					}
					
					// Pop off the previous candidate finder so we can continue in our list
					cf = stkcf.Pop();

#if GATHERSTATS
					cBackTracks++;
#endif
					// Get the backtrack record...
					btr = stkbr.Pop();

					// ...and undo any actions that need to be backtracked
					btr.Backtrack(this);
				}
				else
				{
					// Moving forward - new candidate finder
					cf = new CandidateFinder<TAttr>(this);

					// and start a new backtracking record in case this candidate doesn't work
					btr = new BacktrackRecord<TAttr>();
				}

				// Assume failure
				fPopOut = true;
				Match mchCur;

				// If there are more candidates...
				while ((mchCur = cf.NextCandidateMatch()) != null)
				{
					// If the candidate match is feasible
					if (FFeasible(mchCur))
					{
						// Add it to the isomorphism so far and see if we've completed the isomorphism
						if (FAddMatchToSolution(mchCur, btr) && FCompleteMatch())
						{
							// Yea!  Isomorphism finished!
#if GATHERSTATS
							Console.WriteLine("cBackTracks = {0}", cBackTracks);
							Console.WriteLine("cSearchGuesses = {0}", cSearchGuesses);
							Console.WriteLine("cInfeasible = {0}", cInfeasible);
#endif
							// Record this match and simulate a failure...
							Dictionary<int, int> nidToNid1;
							Dictionary<int, int> nidToNid2;
							VfGraphVfGraphInodeToGraphGraphNid(_vfGraphInode1To2Isomorphism, _degreeSortedToOriginal1, _degreeSortedToOriginal2, out nidToNid1, out nidToNid2);
							yield return new FullMapping(nidToNid1, nidToNid2);
							stkcf.Push(cf);
							stkbr.Push(btr);
							break;
						}
#if GATHERSTATS
	// Made a bad guess, count it up...
							cSearchGuesses++;
#endif
						// Dang!  No full isomorphism yet! Push candidate finder/backtrack record and push on
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
		}
#endif
		#endregion

		#region Mapping
		/// <summary>
		/// Indicate that two nodes are isomorphic
		/// </summary>
		/// <param name="inod1"></param>
		/// <param name="inod2"></param>
		internal void SetIsomorphic(int inod1, int inod2)
		{
			// Set the mapping in both directions
			_vfGraphInode1To2Isomorphism[inod1] = inod2;
			_vfGraphInode2To1Isomorphism[inod2] = inod1;
		}

		// Undo a previously made mapping
		internal void RemoveFromMappingList(int iGraph, int inod)
		{
			(iGraph == 1 ? _vfGraphInode1To2Isomorphism : _vfGraphInode2To1Isomorphism)[inod] = MapIllegal;
		}
		#endregion

		#region Feasibility
		/// <summary>
		/// Count vertices in a list which are in a particular classification
		/// </summary>
		/// <param name="lstOfNodeIndices">List of node indices to check</param>
		/// <param name="vfgr">Graph which contains the vertex</param>
		/// <param name="grp">Classification to check for</param>
		/// <returns>Count of vertices in list which are classified properly</returns>
		private int GetGroupCountInList(IEnumerable<int> lstOfNodeIndices, VfGraph<TAttr> vfgr, Group grp)
		{
			return lstOfNodeIndices.Count(inod => ((int) vfgr.GetGroup(inod) & (int) grp) != 0);
		}

		
// ReSharper disable once ParameterTypeCanBeEnumerable.Local
		/// <summary>
		/// Check that two lists match WRT the current isomorphism
		/// </summary>
		/// <remarks>
		/// This is a very key piece of the VF matching algorithm.  Every time we
		/// match two vertices we verify that any of their in/out neighbors that are also
		/// in the isomorphism match as well.
		/// </remarks>
		/// <param name="lstConnected1">List from Graph1</param>
		/// <param name="lstConnected2">List from Graph2</param>
		/// <returns>True if the lists match</returns>
		private bool FLocallyIsomorphic(List<int> lstConnected1, List<int> lstConnected2)
		{
			var cnodInMapping1 = 0;

			// For each vertex in lstConnected1 that is part of the isomorphism...
			foreach (var inodMap in lstConnected1.Select(inod => _vfGraphInode1To2Isomorphism[inod]).Where(inodMap => inodMap != MapIllegal))
			{
				// Count the isomorphism participants from list1
				cnodInMapping1++;

				// If it's matched vertex isn't contained in the second group, we fail
				if (!lstConnected2.Contains(inodMap))
				{
					return false;
				}
			}

			// Check that the number of mapped elements from the first list match the number
			// from the second.
			if (cnodInMapping1 != GetGroupCountInList(lstConnected2, Vfgr2, Group.ContainedInMapping))
			{
				return false;
			}
			return true;
		}


		/// <summary>
		/// Verify counts of node classes not yet participating in isomorphism
		/// </summary>
		/// <param name="lstIn1">Nodes pointing in to newly matched node in Graph1</param>
		/// <param name="lstOut1">Nodes pointed to from newly matched node in Graph1</param>
		/// <param name="lstIn2">Nodes pointing in to newly matched node in Graph2</param>
		/// <param name="lstOut2">Nodes pointed to from newly matched node in Graph2</param>
		/// <returns></returns>
		private bool FInOutNew(
			IEnumerable<int> lstIn1,
			IEnumerable<int> lstOut1,
			IEnumerable<int> lstIn2,
			IEnumerable<int> lstOut2)
		{
			var nodesIn1 = lstIn1 as List<int> ?? lstIn1.ToList();
			var nodesIn2 = lstIn2 as List<int> ?? lstIn2.ToList();
			var nodesOut1 = lstOut1 as List<int> ?? lstOut1.ToList();
			var nodesOut2 = lstOut2 as List<int> ?? lstOut2.ToList();

			if (!FnCompareDegrees(
				GetGroupCountInList(nodesIn1, Vfgr1, Group.FromMapping),
				GetGroupCountInList(nodesIn2, Vfgr2, Group.FromMapping)))
			{
				return false;
			}

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

		/// <summary>
		/// Determine if a match is feasible or not
		/// </summary>
		/// <remarks>
		/// This is the heart of the VF isomorphism algorithm.
		/// </remarks>
		/// <param name="mtc">Match to check</param>
		/// <returns>True if feasible, False if not</returns>
		private bool FFeasible(Match mtc)
		{
			var inod1 = mtc.Inod1;
			var inod2 = mtc.Inod2;

			// Allow the user to override with a context check on the matching.
			if (_fContextCheck)
			{
				var icc1 = Vfgr1.GetAttr(inod1) as IContextCheck;
				var icc2 = Vfgr2.GetAttr(inod2) as IContextCheck;

				if (icc1 != null && icc2 != null)
				{
					if (!icc1.FCompatible(icc2))
					{
						// User says they're not compatible regardless of the topology
						return false;
					}
				}
			}

			var lstIn1 = Vfgr1.InNeighbors(inod1);
			var lstIn2 = Vfgr2.InNeighbors(inod2);
			var lstOut1 = Vfgr1.OutNeighbors(inod1);
			var lstOut2 = Vfgr2.OutNeighbors(inod2);

			// Node1's In Neighbors in mapping must map to Node2's In Neighbors...
			if (!FLocallyIsomorphic(lstIn1, lstIn2))
			{
				return false;
			}

			// Ditto the above for out neighbors...
			if (!FLocallyIsomorphic(lstOut1, lstOut2))
			{
				return false;
			}

			// Verify the in, out and new predicate from the paper.  This amounts to
			// checking that the counts for the group classifications in the corresponding
			// lists below must match.
			if (!FInOutNew(lstOut1, lstIn1, lstOut2, lstIn2))
			{
				return false;
			}

			return true;
		}
		#endregion

		#region State Change/Restoral
		/// <summary>
		/// Add a match to the isomorphism
		/// </summary>
		/// <remarks>
		/// This is only a proposed match.  If it fails, the passed in BacktrackRecord
		/// will have all the actions taken as a consequence and they can be undone using
		/// that BacktrackRecord.
		/// </remarks>
		/// <param name="mtc">Proposed match</param>
		/// <param name="btr">BacktrackRecord to record actions into</param>
		/// <returns>True if match is locally consistent with a full isomorphism</returns>
		private bool FAddMatchToSolution(Match mtc, BacktrackRecord<TAttr> btr)
		{
			var inod1 = mtc.Inod1;
			var inod2 = mtc.Inod2;

			// Record the match action in the backtrackRecord
			btr.SetMatch(mtc.Inod1, mtc.Inod2, this);

			// In and Out neighbors of the nodes in the match
			var lstIn1 = Vfgr1.InNeighbors(inod1);
			var lstIn2 = Vfgr2.InNeighbors(inod2);
			var lstOut1 = Vfgr1.OutNeighbors(inod1);
			var lstOut2 = Vfgr2.OutNeighbors(inod2);

			// Reclassify any neighbors of the added nodes that require it
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

			// If the total degrees into or out of the isomorphism don't match up properly (less for
			// subgraph isomorphism equal for exact isomorphism) then we can never match properly.
			if (!FnCompareDegrees(_outDegreeTotal1, _outDegreeTotal2) ||
			    !FnCompareDegrees(_inDegreeTotal1, _inDegreeTotal2))
			{
				return false;
			}

			// Also check the node counts which is a different check from the total degree above...
			return FnCompareDegrees(lstOut1.Count, lstOut2.Count) && FnCompareDegrees(lstIn1.Count, lstIn2.Count);
		}

		// Convenience functions so we can make incrementing/decrementing the
		// in/out counts a bit more generic...
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

			VfGraph<TAttr> vfg;
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
	}

	public class VfState : VfState<Object>
	{
		public VfState(IGraphLoader<Object> loader1, IGraphLoader<Object> loader2, bool fIsomorphism = false, bool fContextCheck = false) :
			base(loader1, loader2, fIsomorphism, fContextCheck) {}
	}

	// ReSharper disable once ClassNeverInstantiated.Global
	public class VfStateTests
	{
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

				var vfs = new VfState(gr1, gr2);
				var matches = vfs.Matches().ToArray();
				Assert.AreNotEqual(0, matches.Length);
				Assert.AreEqual(3, matches.Length);

				vfs = new VfState(gr1, gr2, true);
				matches = vfs.Matches().ToArray();
				Assert.AreEqual(3, matches.Length);

				gr2.InsertEdge(0, 1);
				gr2.InsertEdge(1, 2);
				gr2.InsertEdge(2, 0);
				gr1.InsertEdge(0, 2);
				gr1.InsertEdge(2, 1);
				gr1.InsertEdge(1, 0);

				vfs = new VfState(gr1, gr2);
				matches = vfs.Matches().ToArray();
				Assert.AreEqual(6, matches.Length);

				vfs = new VfState(gr1, gr2, true);
				matches = vfs.Matches().ToArray();
				Assert.AreEqual(6, matches.Length);
			}

			[Test]
			public void TestConstructor()
			{
				Assert.IsNotNull(VfsTest());
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
			public void TestMatch()
			{
				var gr1 = new Graph();
				var gr2 = new Graph();

				var vfs = new VfState(gr1, gr2);
				var match = vfs.Match();
				Assert.IsNotNull(match);			// Two empty graphs match
				gr2.InsertNode();
				vfs = new VfState(gr1, gr2);
				match = vfs.Match();
				Assert.IsNull(match);				// Graph with no nodes will not match one with a single isolated vertex
				gr1.InsertNode();
				vfs = new VfState(gr1, gr2);
				match = vfs.Match();
				Assert.IsNotNull(match);			// Two graphs with single isolated vertices match
				gr1.InsertNode();
				vfs = new VfState(gr1, gr2);
				match = vfs.Match();
				Assert.IsNotNull(match);			// Two isolated nodes match with one under default subgraph isomorphism
				gr1.InsertEdge(0, 1);
				vfs = new VfState(gr1, gr2);
				match = vfs.Match();
				Assert.IsNotNull(match);			// Connect the two and a subgraph isomorphism still works
			}

			[Test]
			public void TestMatchComplex()
			{
				var vfs = VfsTest();
				var matches = vfs.Matches().ToArray();
				Assert.AreEqual(1, matches.Length);
				var dict1 = matches[0].IsomorphismNid1ToNid2;
				var dict2 = matches[0].IsomorphismNid2ToNid1;
				Assert.AreEqual(1, dict1[0]);
				Assert.AreEqual(0, dict1[1]);
				Assert.AreEqual(5, dict1[2]);
				Assert.AreEqual(4, dict1[3]);
				Assert.AreEqual(3, dict1[4]);
				Assert.AreEqual(2, dict1[5]);
				Assert.AreEqual(1, dict2[0]);
				Assert.AreEqual(0, dict2[1]);
				Assert.AreEqual(5, dict2[2]);
				Assert.AreEqual(4, dict2[3]);
				Assert.AreEqual(3, dict2[4]);
				Assert.AreEqual(2, dict2[5]);
				var graph2 = VfsTestGraph2();
				graph2.DeleteEdge(1, 4);
				graph2.InsertEdge(2, 4);
				vfs = new VfState(VfsTestGraph1(), graph2);
				matches = vfs.Matches().ToArray();
				Assert.AreEqual(0, matches.Length);

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
				matches = vfs.Matches().ToArray();
				Assert.AreNotEqual(0, matches.Length);
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
				var matches = vfs.Matches().ToArray();
				Assert.AreNotEqual(0, matches.Length);
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
				var matches = vfs.Matches().ToArray();
				Assert.AreNotEqual(0, matches.Length);

				gr1 = VfsTestGraph1();
				gr2 = VfsTestGraph2();
				vfs = new VfState(gr1, gr2);
				matches = vfs.Matches().ToArray();
				Assert.AreNotEqual(0, matches.Length);
				gr1.InsertNode();
				gr1.InsertEdge(6, 3);
				gr1.InsertEdge(6, 5);

				// Graph 2 is isomorphic to a subgraph of graph 1 (default check is for
				// subgraph isomorphism).
				vfs = new VfState(gr1, gr2);
				matches = vfs.Matches().ToArray();
				Assert.AreNotEqual(0, matches.Length);

				// The converse is false
				vfs = new VfState(gr2, gr1);
				matches = vfs.Matches().ToArray();
				Assert.AreEqual(0, matches.Length);

				// The two graphs are subgraph ismorphic but not ismorphic
				vfs = new VfState(gr1, gr2, true /* fIsomorphism */);
				matches = vfs.Matches().ToArray();
				Assert.AreEqual(0, matches.Length);
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
				var matches = vfs.Matches().ToArray();
				Assert.AreNotEqual(0, matches.Length);
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
					return ((NodeColor)icc)._strColor == _strColor;
				}
			}

			[Test]
			public void TestContextCheck()
			{
				var graph1 = new Graph<NodeColor>();
				var graph2 = new Graph<NodeColor>();

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

				var vfs = new VfState<NodeColor>(graph1, graph2, true);
				var matches = vfs.Matches().ToArray();
				Assert.AreNotEqual(0, matches.Length);
				var mpMatch = matches[0].IsomorphismNid1ToNid2;
				// With no context checking, vertex 0 in the first graph can match
				// vertex 0 in the second graph
				Assert.AreEqual(0, mpMatch[0]);

				vfs = new VfState<NodeColor>(graph1, graph2, true, true);
				matches = vfs.Matches().ToArray();
				Assert.AreNotEqual(0, matches.Length);
				mpMatch = matches[0].IsomorphismNid1ToNid2;
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
				FullMapping[] matches;

				for (var i = 0; i < 10; i++)
				{
					rg.IsomorphicPair(100, out graph1, out graph2);
					vfs = new VfState(graph1, graph2);
					matches = vfs.Matches().ToArray();
					Assert.AreNotEqual(0, matches.Length);
				}

				graph1 = rg.GetGraph(100);
				graph2 = rg.GetGraph(100);
				vfs = new VfState(graph1, graph2);
				matches = vfs.Matches().ToArray();
				Assert.AreEqual(0, matches.Length);

				rg = new RandomGraph(0.3, 5000 /* seed */);
				graph1 = rg.GetGraph(100);
				rg = new RandomGraph(0.3, 5000 /* seed */);
				graph2 = rg.GetGraph(100);
				vfs = new VfState(graph1, graph2);
				matches = vfs.Matches().ToArray();
				Assert.AreEqual(1, matches.Length);

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