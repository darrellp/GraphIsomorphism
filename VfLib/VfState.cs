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

	public class VfState<TV, TE>
		where TV : class 
		where TE : class
	{
		#region Private Variables
		internal const int MapIllegal = -1;

		// Mapping between vertex positions in VfGraph and the positions in the original Graph.
		// This is just the permutation to sort the original graph vertices by degrees.
		private readonly List<int> _degreeSortedToOriginal1;
		private readonly List<int> _degreeSortedToOriginal2;
		private readonly Dictionary<VfVertex<TV, TE>, Vertex<TV, TE>> _mpVtxToOriginalVertex1;
		private readonly Dictionary<VfVertex<TV, TE>, Vertex<TV, TE>> _mpVtxToOriginalVertex2;

		// The original ILoader's - needed to map back the permutation
		// to the original vertex id's after the match
		private readonly IGraphLoader<TV, TE> _ldr1;
		private readonly IGraphLoader<TV, TE> _ldr2;

		// The actual mappings we're building up.  Note that in our VfGraph the ordering of vertices is based
		// on decreasing total degree so it's not generally the same as in the original graph.  The
		// isomorphisms being built are based on this ordering rather than that in the original graph.
		// Before they're returned we massage the permutations to match that in the original graph.
		private readonly Dictionary<int, int> _vfGraphIvtx1To2Isomorphism;
		private readonly Dictionary<int, int> _vfGraphIvtx2To1Isomorphism;

		// Counts for degrees into and out of the isomorphism being built.  Part of the
		// the magic of the VF algorithm is to check these to verify validity.
		private int _inDegreeTotal1, _inDegreeTotal2, _outDegreeTotal1, _outDegreeTotal2;

		private readonly bool _fContextCheck; // Do we context check using the attributes?
		#endregion

		#region Properties
		// The two graphs we're comparing
		internal VfGraph<TV, TE> VfGraph2 { get; set; }
		internal VfGraph<TV, TE> VfGraph1 { get; set; }

		// Lists of vertices not yet participating in the current isomorphism
		// These lists are sorted on index but since the original vertices were
		// sorted by degree, that means that these are also sorted by degree
		// which is a key performance heuristic.

		// Vertices that point into the current isomorphism set without participating in it
		internal SortedListNoValue<int> LstIn1 { get; set; }
		internal SortedListNoValue<int> LstIn2 { get; set; }

		// Vertices that point out of the current isomorphism set without participating in it
		internal SortedListNoValue<int> LstOut1 { get; set; }
		internal SortedListNoValue<int> LstOut2 { get; set; }

		// Vertices that are not connected with any vertex in the current isomorphism candidate
		internal SortedListNoValue<int> LstDisconnected1 { get; set; }
		internal SortedListNoValue<int> LstDisconnected2 { get; set; }
		#endregion

		#region Delegates
		// Comparison function depends on whether we're doing full isomorphism
		// or subgraph isomorphism.
		internal readonly Func<int, int, bool> FnCompareDegrees;

		// In a straight isomorphism degrees of matching vertices must match exactly
		private static bool CmpIsomorphism(int degree1, int degree2)
		{
			return degree1 == degree2;
		}

		// In a subgraph isomorphism, vertices in the original graph may have lower degree
		// than vertices in the subgraph since the subgraph is embedded in the "supergraph"
		// which may have vertices connecting to the subgraph increasing the degree of vertices
		// in the subgraph.
		private static bool CmpSubgraphIsomorphism(int degreeSubgraph, int degreeOriginal)
		{
			return degreeSubgraph >= degreeOriginal;
		}
		#endregion

		#region Constructors
		public VfState(
			IGraphLoader<TV, TE> loader1,
			IGraphLoader<TV, TE> loader2,
			bool fIsomorphism = false,
			bool fContextCheck = false)
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

			// Vertex indices in VfGraphs are sorted by vertex degree
			_degreeSortedToOriginal1 = new CmpVertexDegrees<TV, TE>(loader1).Permutation;
			_degreeSortedToOriginal2 = new CmpVertexDegrees<TV, TE>(loader2).Permutation;
			VfGraph1 = new VfGraph<TV, TE>(loader1, _degreeSortedToOriginal1);
			VfGraph2 = new VfGraph<TV, TE>(loader2, _degreeSortedToOriginal2);

			// Set up space for isomorphism mappings
			_vfGraphIvtx1To2Isomorphism = new Dictionary<int, int>(loader1.VertexCount);
			_vfGraphIvtx2To1Isomorphism = new Dictionary<int, int>(loader2.VertexCount);

			// When we start no isomorphic mappings and all vertices are disconnected
			// from the isomorphism.
			for (var i = 0; i < loader1.VertexCount; i++)
			{
				_vfGraphIvtx1To2Isomorphism[i] = MapIllegal;
				LstDisconnected1.Add(i);
			}
			for (var i = 0; i < loader2.VertexCount; i++)
			{
				_vfGraphIvtx2To1Isomorphism[i] = MapIllegal;
				LstDisconnected2.Add(i);
			}
		}
		#endregion

		#region Matching
		// Return true if we've got a completed isomorphism
		private bool FCompleteMatch()
		{
			// If there's nothing outside the isomorphism in graph2 then we're done...
			return LstDisconnected2.Count == 0 && LstIn2.Count == 0 && LstOut2.Count == 0;
		}

		/// <summary>
		/// Ensure that degrees of vertices are compatible
		/// </summary>
		/// <returns>True if compatible, False if not</returns>
		private bool FCompatibleDegrees()
		{
			if (!FnCompareDegrees(VfGraph1.VertexCount, VfGraph2.VertexCount))
			{
				return false;
			}

			// Since we've sorted the vertices by degree, we can do a quick compare on the degree sequence
			// on a vertex by vertex basis...
			for (var iVertex = 0; iVertex < VfGraph2.VertexCount; iVertex++)
			{
				if (!FnCompareDegrees(VfGraph1.TotalDegree(iVertex), VfGraph2.TotalDegree(iVertex)))
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
			if (_ldr2.VertexCount == 0)
			{
				// If the second is empty, check for a successful "match"
				if (FnCompareDegrees(_ldr1.VertexCount, 0))
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

			var stkcf = new Stack<CandidateFinder<TV, TE>>();
			var stkbr = new Stack<BacktrackRecord<TV, TE>>();

			var fBacktrack = false;
#if GATHERSTATS
			int cSearchGuesses = 0;
			int cBackTracks = 0;
			int cInfeasible = 0;
#endif

			// The general structure here is:
			// While (true)
			//		if (backtracking)
			//			if there are no graph2 vertices to backtrack to, we can't find any other isomorphisms so return
			//			pop off the previous graph2 vertex to continue it's match
			//			perform backtrack actions
			//		else
			//			pick a new graph2 vertex to try to match
			//		while (there are still potential graph1 vertices to match our current graph2 vertex)
			//			if (the selected graph1 vertex is a feasible match to our current graph2 vertex)
			//				Add the match (and record it if we later need to backtrack)
			//				if (we've got a complete isomorphism)
			//					yield the isomorphism
			//					Push our current graph2 vertex so it will be popped off during backtracking
			//					break the inner loop to trigger artificial backtrack
			//				push the current graph2 vertex search and continue in outer loop with no backtracking
			//					
			while (true)
			{
				CandidateFinder<TV, TE> cf;
				BacktrackRecord<TV, TE> btr;

				// If it's time to backtrack...
				if (fBacktrack)
				{
					// If there are no more candidates left, we've failed to find an isomorphism
					if (stkcf.Count <= 0)
					{
						break; // Out of the top level while loop and end enumeration
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

					// Each new candidate finder we produce here picks a vertex in graph2 and works
					// at matching it to a vertex in graph1 until there are no possibile graph1 vertices left.
					// at that point we know that the current isomorphism is impossible since no
					// graph1 vertex can be matched to the selected graph2 vertex.  At that point we
					// will pop the candidate finder, backtrack any changes we've made and move to
					// the next graph1 candidate in the previous candidate finder on the stack.
					cf = new CandidateFinder<TV, TE>(this);

					// Start a new backtracking record in case we fail to find a match for our
					// selected graph2 vertex.
					btr = new BacktrackRecord<TV, TE>();
				}

				// Assume failure
				fBacktrack = true;
				Match mchCur;

				// For all the graph1 vertices that could potentially match up with the current
				// candidateFinder's graph2 vertex...
				while ((mchCur = cf.NextCandidateMatch()) != null)
				{
					// If the candidate match is feasible
					if (FFeasible(mchCur))
					{
						// Add it to the isomorphism so far and see if we've completed the isomorphism
						if (FAddMatchToSolution(mchCur, btr) && FCompleteMatch())
						{
							// Yay!  Isomorphism found!
#if GATHERSTATS
							Console.WriteLine("cBackTracks = {0}", cBackTracks);
							Console.WriteLine("cSearchGuesses = {0}", cSearchGuesses);
							Console.WriteLine("cInfeasible = {0}", cInfeasible);
#endif
							// Record this match
							Dictionary<int, int> vidToVid1;
							Dictionary<int, int> vidToVid2;

							// Change the ivertex to ivertex VfGraph mapping in _vfGraphIvtx1To2Isomorphism
							// to an vid to vid mapping in the original graph...
							VfGraphVfGraphIvtxToGraphGraphVid(
								_vfGraphIvtx1To2Isomorphism, 
								_degreeSortedToOriginal1, 
								_degreeSortedToOriginal2, 
								out vidToVid1, 
								out vidToVid2);
							yield return new FullMapping(vidToVid1, vidToVid2);

							// This will cause a backtrack where this candidateFinder
							// will be popped off the stack and we'll continue 
							// with this graph2 vertex looking for the next solution.
							stkcf.Push(cf);
							stkbr.Push(btr);
							break;
						}
#if GATHERSTATS
	// Made a bad guess, count it up...
							cSearchGuesses++;
#endif
						// Dang!  No full isomorphism yet but our choices so far aren't infeasible.
						// Push candidate finder/backtrack record and break out of the inner loop which will
						// cause us to pick another candidate finder/graph2 vertex to be mapped.
						stkcf.Push(cf);
						stkbr.Push(btr);
						fBacktrack = false;
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
		#endregion

		#region Mapping
		/// <summary>
		/// Turn VfGraph to VfGraph Ivtx mapping into Graph to Graph vid mapping
		/// </summary>
		/// <param name="isomorphismVfIvtx1To2">The VfGraph to VfGraph mapping</param>
		/// <param name="degreeSortedToOriginal1">VfGraph1 Ivtx to Graph1 Ivtx mapping</param>
		/// <param name="degreeSortedToOriginal2">VfGraph2 Ivtx to Graph2 Ivtx mapping</param>
		/// <param name="isomorphismVid1ToVid2">Returned Graph1 vid to Graph2 vid mapping</param>
		/// <param name="isomorphismVid2ToVid1">Returned Graph2 vid to Graph1 vid mapping</param>
		private void VfGraphVfGraphIvtxToGraphGraphVid(
			Dictionary<int, int> isomorphismVfIvtx1To2,
			List<int> degreeSortedToOriginal1,
			List<int> degreeSortedToOriginal2,
			out Dictionary<int, int> isomorphismVid1ToVid2,
			out Dictionary<int, int> isomorphismVid2ToVid1)
		{
			// Holding areas for new permutations
			isomorphismVid1ToVid2 = new Dictionary<int, int>();
			isomorphismVid2ToVid1 = new Dictionary<int, int>();

			foreach (var pair in isomorphismVfIvtx1To2)
			{
				var vfIvtx2 = pair.Value;
				var ivtx1 = degreeSortedToOriginal1[pair.Key];
				var vid1 = _ldr1.IdFromPos(ivtx1);
				if (vfIvtx2 == MapIllegal)
				{
					isomorphismVid1ToVid2[vid1] = MapIllegal;
					break;
				}
				var ivtx2 = degreeSortedToOriginal2[vfIvtx2];
				var vid2 = _ldr2.IdFromPos(ivtx2);
				isomorphismVid1ToVid2[vid1] = vid2;
				isomorphismVid2ToVid1[vid2] = vid1;
			}
		}

		/// <summary>
		/// Indicate that two vertices are isomorphic
		/// </summary>
		/// <param name="ivtx1"></param>
		/// <param name="ivtx2"></param>
		internal void SetIsomorphic(int ivtx1, int ivtx2)
		{
			// Set the mapping in both directions
			_vfGraphIvtx1To2Isomorphism[ivtx1] = ivtx2;
			_vfGraphIvtx2To1Isomorphism[ivtx2] = ivtx1;
		}

		// Undo a previously made mapping
		internal void RemoveFromMappingList(int iGraph, int ivtx)
		{
			(iGraph == 1 ? _vfGraphIvtx1To2Isomorphism : _vfGraphIvtx2To1Isomorphism)[ivtx] = MapIllegal;
		}
		#endregion

		#region Feasibility
		/// <summary>
		/// Count vertices in a list which are in a particular classification
		/// </summary>
		/// <param name="lstOfVertexIndices">List of vertex indices to check</param>
		/// <param name="vfgr">Graph which contains the vertex</param>
		/// <param name="grp">Classification to check for</param>
		/// <returns>Count of vertices in list which are classified properly</returns>
		private int GetGroupCountInList(IEnumerable<int> lstOfVertexIndices, VfGraph<TV, TE> vfgr, Group grp)
		{
			return lstOfVertexIndices.Count(ivtx => ((int) vfgr.GetGroup(ivtx) & (int) grp) != 0);
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
			foreach (var ivtxMap in lstConnected1.Select(ivtx => _vfGraphIvtx1To2Isomorphism[ivtx]).Where(ivtx => ivtx != MapIllegal))
			{
				// Count the isomorphism participants from list1
				cnodInMapping1++;

				// If it's matched vertex isn't contained in the second group, we fail
				if (!lstConnected2.Contains(ivtxMap))
				{
					return false;
				}
			}

			// Check that the number of mapped elements from the first list match the number
			// from the second.
			if (cnodInMapping1 != GetGroupCountInList(lstConnected2, VfGraph2, Group.ContainedInMapping))
			{
				return false;
			}
			return true;
		}


		/// <summary>
		/// Verify counts of Vertex classes not yet participating in isomorphism
		/// </summary>
		/// <param name="lstIn1">Vertices pointing in to newly matched vertex in Graph1</param>
		/// <param name="lstOut1">Vertices pointed to from newly matched vertex in Graph1</param>
		/// <param name="lstIn2">Vertices pointing in to newly matched vertex in Graph2</param>
		/// <param name="lstOut2">Vertices pointed to from newly matched vertex in Graph2</param>
		/// <returns></returns>
		private bool FInOutNew(
			IEnumerable<int> lstIn1,
			IEnumerable<int> lstOut1,
			IEnumerable<int> lstIn2,
			IEnumerable<int> lstOut2)
		{
			var verticesIn1 = lstIn1 as List<int> ?? lstIn1.ToList();
			var verticesIn2 = lstIn2 as List<int> ?? lstIn2.ToList();
			var verticesOut1 = lstOut1 as List<int> ?? lstOut1.ToList();
			var verticesOut2 = lstOut2 as List<int> ?? lstOut2.ToList();

			if (!FnCompareDegrees(
				GetGroupCountInList(verticesIn1, VfGraph1, Group.FromMapping),
				GetGroupCountInList(verticesIn2, VfGraph2, Group.FromMapping)))
			{
				return false;
			}

			if (!FnCompareDegrees(
				GetGroupCountInList(verticesOut1, VfGraph1, Group.FromMapping),
				GetGroupCountInList(verticesOut2, VfGraph2, Group.FromMapping)))
			{
				return false;
			}

			if (!FnCompareDegrees(
				GetGroupCountInList(verticesIn1, VfGraph1, Group.ToMapping),
				GetGroupCountInList(verticesIn2, VfGraph2, Group.ToMapping)))
			{
				return false;
			}

			if (!FnCompareDegrees(
				GetGroupCountInList(verticesOut1, VfGraph1, Group.ToMapping),
				GetGroupCountInList(verticesOut2, VfGraph2, Group.ToMapping)))
			{
				return false;
			}

			if (!FnCompareDegrees(
				GetGroupCountInList(verticesOut1, VfGraph1, Group.Disconnected),
				GetGroupCountInList(verticesOut2, VfGraph2, Group.Disconnected)))
			{
				return false;
			}

			if (!FnCompareDegrees(
				GetGroupCountInList(verticesIn1, VfGraph1, Group.Disconnected),
				GetGroupCountInList(verticesIn2, VfGraph2, Group.Disconnected)))
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
			var ivtx1 = mtc.Ivtx1;
			var ivtx2 = mtc.Ivtx2;

			// Allow the user to override with a context check on the matching.
			if (_fContextCheck)
			{
				var icc1 = VfGraph1.GetAttr(ivtx1) as IContextCheck;
				var icc2 = VfGraph2.GetAttr(ivtx2) as IContextCheck;

				if (icc1 != null && icc2 != null)
				{
					if (!icc1.FCompatible(icc2))
					{
						// User says they're not compatible regardless of the topology
						return false;
					}
				}
			}

			var lstIn1 = VfGraph1.InNeighbors(ivtx1);
			var lstIn2 = VfGraph2.InNeighbors(ivtx2);
			var lstOut1 = VfGraph1.OutNeighbors(ivtx1);
			var lstOut2 = VfGraph2.OutNeighbors(ivtx2);

			// Vertex1's In Neighbors in mapping must map to Vertex2's In Neighbors...
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
		private bool FAddMatchToSolution(Match mtc, BacktrackRecord<TV, TE> btr)
		{
			var ivtx1 = mtc.Ivtx1;
			var ivtx2 = mtc.Ivtx2;

			// Record the match action in the backtrackRecord
			btr.SetMatch(mtc.Ivtx1, mtc.Ivtx2, this);

			// In and Out neighbors of the vertices in the match
			var lstIn1 = VfGraph1.InNeighbors(ivtx1);
			var lstIn2 = VfGraph2.InNeighbors(ivtx2);
			var lstOut1 = VfGraph1.OutNeighbors(ivtx1);
			var lstOut2 = VfGraph2.OutNeighbors(ivtx2);

			// Reclassify any neighbors of the added vertices that require it
			foreach (var ivtx in lstOut1)
			{
				if (((int) VfGraph1.GetGroup(ivtx) & (int) (Group.Disconnected | Group.ToMapping)) != 0)
				{
					btr.MoveToGroup(1, ivtx, Group.FromMapping, this);
				}
			}
			foreach (var ivtx in lstIn1)
			{
				if (((int) VfGraph1.GetGroup(ivtx) & (int) (Group.Disconnected | Group.FromMapping)) != 0)
				{
					btr.MoveToGroup(1, ivtx, Group.ToMapping, this);
				}
			}
			foreach (var ivtx in lstOut2)
			{
				if (((int) VfGraph2.GetGroup(ivtx) & (int) (Group.Disconnected | Group.ToMapping)) != 0)
				{
					btr.MoveToGroup(2, ivtx, Group.FromMapping, this);
				}
			}
			foreach (var ivtx in lstIn2)
			{
				if (((int) VfGraph2.GetGroup(ivtx) & (int) (Group.Disconnected | Group.FromMapping)) != 0)
				{
					btr.MoveToGroup(2, ivtx, Group.ToMapping, this);
				}
			}

			// If the total degrees into or out of the isomorphism don't match up properly (less for
			// subgraph isomorphism equal for exact isomorphism) then we can never match properly.
			if (!FnCompareDegrees(_outDegreeTotal1, _outDegreeTotal2) ||
			    !FnCompareDegrees(_inDegreeTotal1, _inDegreeTotal2))
			{
				return false;
			}

			// Also check the vertex counts which is a different check from the total degree above...
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
		/// Reclassify a vertex into a new group
		/// </summary>
		/// <param name="iGraph">Graph the vertex is being reclassified in</param>
		/// <param name="ivtx">Vertex to reclassify</param>
		/// <param name="grpNew">New classification for the vertex</param>
		internal void MakeMove(int iGraph, int ivtx, Group grpNew)
		{
			// Moves to the mapping are handled by the mapping arrays and aren't handled here.

			VfGraph<TV, TE> vfg;
			SortedListNoValue<int> disconnectedList, outList, inList;

			if (iGraph == 1)
			{
				vfg = VfGraph1;
				disconnectedList = LstDisconnected1;
				outList = LstOut1;
				inList = LstIn1;
			}
			else
			{
				vfg = VfGraph2;
				disconnectedList = LstDisconnected2;
				outList = LstOut2;
				inList = LstIn2;
			}

			// Old groups and new groups
			var igrpCur = (int) vfg.GetGroup(ivtx);
			var igrpNew = (int) grpNew;

			// Groups to add to and remove from
			var igrpRemove = igrpCur & ~igrpNew;
			var igrpAdd = igrpNew & ~igrpCur;

			if (igrpRemove != 0)
			{
				// Do we need to remove from the disconnect list?
				if ((igrpRemove & (int) Group.Disconnected) != 0)
				{
					disconnectedList.Delete(ivtx);
				}

				// If we're no longer mapped from a vertex in the isomorphism
				if ((igrpRemove & (int) Group.FromMapping) != 0)
				{
					// Remove us from out list and decrement total out degree
					outList.Delete(ivtx);
					AddToOutDegree(iGraph, -1);
				}
				// If we're no longer mapped into a vertex in the isomorphism
				if ((igrpRemove & (int)Group.ToMapping) != 0)
				{
					// Remove us from in list and decrement total out degree
					inList.Delete(ivtx);
					AddToInDegree(iGraph, -1);
				}
			}
			if (igrpAdd != 0)
			{
				// Add us to the disconnected group if necessary
				if ((igrpAdd & (int) Group.Disconnected) != 0)
				{
					disconnectedList.Add(ivtx);
				}
				// If we're newly mapped from a vertex in the isomorphism
				if ((igrpAdd & (int)Group.FromMapping) != 0)
				{
					// Add us to the out list and increment total out degree
					outList.Add(ivtx);
					AddToOutDegree(iGraph, 1);
				}
				// If we're newly mapped into a vertex in the isomorphism
				if ((igrpAdd & (int) Group.ToMapping) != 0)
				{
					// Add us to the in list and increment total in degree
					inList.Add(ivtx);
					AddToInDegree(iGraph, 1);
				}
			}
			vfg.SetGroup(ivtx, grpNew);
		}
		#endregion
	}

	public class VfState : VfState<Object, Object>
	{
		public VfState(IGraphLoader<Object, Object> loader1, IGraphLoader<Object, Object> loader2, bool fIsomorphism = false, bool fContextCheck = false) :
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
				Assert.AreEqual(0, graph.InsertVertex());
				Assert.AreEqual(1, graph.InsertVertex());
				Assert.AreEqual(2, graph.InsertVertex());
				Assert.AreEqual(3, graph.InsertVertex());
				Assert.AreEqual(4, graph.InsertVertex());
				Assert.AreEqual(5, graph.InsertVertex());
				// Circular graph with "extra" edge at (0,3)
				graph.AddEdge(0, 1);
				graph.AddEdge(1, 2);
				graph.AddEdge(2, 3);
				graph.AddEdge(3, 4);
				graph.AddEdge(4, 5);
				graph.AddEdge(5, 0);
				graph.AddEdge(0, 3);

				return graph;
			}

			private Graph VfsTestGraph2()
			{
				var graph = new Graph();
				Assert.AreEqual(0, graph.InsertVertex());
				Assert.AreEqual(1, graph.InsertVertex());
				Assert.AreEqual(2, graph.InsertVertex());
				Assert.AreEqual(3, graph.InsertVertex());
				Assert.AreEqual(4, graph.InsertVertex());
				Assert.AreEqual(5, graph.InsertVertex());
				// Same graph in reverse order with slightly offset "extra" edge at (4,1)
				graph.AddEdge(1, 0);
				graph.AddEdge(2, 1);
				graph.AddEdge(3, 2);
				graph.AddEdge(4, 3);
				graph.AddEdge(5, 4);
				graph.AddEdge(0, 5);
				graph.AddEdge(1, 4);

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

				gr1.InsertVertices(3);
				gr2.InsertVertices(3);
				gr1.AddEdge(0, 1);
				gr1.AddEdge(1, 2);
				gr1.AddEdge(2, 0);
				gr2.AddEdge(0, 2);
				gr2.AddEdge(2, 1);
				gr2.AddEdge(1, 0);

				var vfs = new VfState(gr1, gr2);
				var matches = vfs.Matches().ToArray();
				Assert.AreNotEqual(0, matches.Length);
				Assert.AreEqual(3, matches.Length);

				vfs = new VfState(gr1, gr2, true);
				matches = vfs.Matches().ToArray();
				Assert.AreEqual(3, matches.Length);

				gr2.AddEdge(0, 1);
				gr2.AddEdge(1, 2);
				gr2.AddEdge(2, 0);
				gr1.AddEdge(0, 2);
				gr1.AddEdge(2, 1);
				gr1.AddEdge(1, 0);

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
				Assert.AreEqual(Group.FromMapping, vfs.VfGraph1.GetGroup(0));
			}

			[Test]
			public void TestMatch()
			{
				var gr1 = new Graph();
				var gr2 = new Graph();

				var vfs = new VfState(gr1, gr2);
				var match = vfs.Match();
				Assert.IsNotNull(match);			// Two empty graphs match
				gr2.InsertVertex();
				vfs = new VfState(gr1, gr2);
				match = vfs.Match();
				Assert.IsNull(match);				// Graph with no vertices will not match one with a single isolated vertex
				gr1.InsertVertex();
				vfs = new VfState(gr1, gr2);
				match = vfs.Match();
				Assert.IsNotNull(match);			// Two graphs with single isolated vertices match
				gr1.InsertVertex();
				vfs = new VfState(gr1, gr2);
				match = vfs.Match();
				Assert.IsNotNull(match);			// Two isolated vertices match with one under default subgraph isomorphism
				gr1.AddEdge(0, 1);
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
				var dict1 = matches[0].IsomorphismVid1ToVid2;
				var dict2 = matches[0].IsomorphismVid2ToVid1;
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
				graph2.AddEdge(2, 4);
				vfs = new VfState(VfsTestGraph1(), graph2);
				matches = vfs.Matches().ToArray();
				Assert.AreEqual(0, matches.Length);

				var graph1 = new Graph();
				graph1.InsertVertices(11);
				graph2 = new Graph();
				graph2.InsertVertices(11);

				graph1.AddEdge(0, 2);
				graph1.AddEdge(0, 1);
				graph1.AddEdge(2, 4);
				graph1.AddEdge(2, 5);
				graph1.AddEdge(1, 5);
				graph1.AddEdge(1, 3);
				graph1.AddEdge(4, 7);
				graph1.AddEdge(5, 7);
				graph1.AddEdge(5, 8);
				graph1.AddEdge(5, 6);
				graph1.AddEdge(3, 6);
				graph1.AddEdge(7, 10);
				graph1.AddEdge(6, 9);
				graph1.AddEdge(10, 8);
				graph1.AddEdge(8, 9);

				graph2.AddEdge(0, 1);
				graph2.AddEdge(0, 9);
				graph2.AddEdge(1, 2);
				graph2.AddEdge(1, 10);
				graph2.AddEdge(9, 10);
				graph2.AddEdge(9, 8);
				graph2.AddEdge(2, 3);
				graph2.AddEdge(10, 3);
				graph2.AddEdge(10, 5);
				graph2.AddEdge(10, 7);
				graph2.AddEdge(8, 7);
				graph2.AddEdge(3, 4);
				graph2.AddEdge(7, 6);
				graph2.AddEdge(4, 5);
				graph2.AddEdge(5, 6);

				vfs = new VfState(graph1, graph2, true);
				matches = vfs.Matches().ToArray();
				Assert.AreNotEqual(0, matches.Length);
			}

			[Test]
			public void TestMatchNonConnected()
			{
				var gr1 = new Graph();
				var gr2 = new Graph();

				gr1.InsertVertices(4);
				gr2.InsertVertices(4);

				gr1.AddEdge(0, 1);
				gr1.AddEdge(2, 3);
				gr2.AddEdge(2, 1);
				gr2.AddEdge(0, 3);
				var vfs = new VfState(gr1, gr2);
				var matches = vfs.Matches().ToArray();
				Assert.AreNotEqual(0, matches.Length);
			}

			[Test]
			public void TestMatchSubgraph()
			{
				// Note that "Subgraph" is defined as a graph derived from
				// deleting vertices from another graph.  Thus, the edges
				// between the remaining vertices must match with matches in the
				// original graph.  Adding edges between vertices in a graph does
				// not constitute a supergraph under this definition.

				var gr1 = new Graph();
				var gr2 = new Graph();

				gr1.InsertVertices(4);
				gr2.InsertVertices(3);
				gr1.AddEdge(0, 1);
				gr1.AddEdge(2, 0);
				gr1.AddEdge(3, 0);
				gr1.AddEdge(2, 3);
				gr2.AddEdge(0, 1);
				gr2.AddEdge(2, 0);
				var vfs = new VfState(gr1, gr2);
				var matches = vfs.Matches().ToArray();
				Assert.AreNotEqual(0, matches.Length);

				gr1 = VfsTestGraph1();
				gr2 = VfsTestGraph2();
				vfs = new VfState(gr1, gr2);
				matches = vfs.Matches().ToArray();
				Assert.AreNotEqual(0, matches.Length);
				gr1.InsertVertex();
				gr1.AddEdge(6, 3);
				gr1.AddEdge(6, 5);

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

				graph1.InsertVertices(cRows * cCols);
				for (var iRow = 0; iRow < cRows - 1; iRow++)
				{
					for (var iCol = 0; iCol < cCols - 1; iCol++)
					{
						var ivtx = iCol * cRows + iRow;
						var iVertexToCol = ivtx + 1;
						var iVertexToRow = ivtx + cRows;

						graph1.AddEdge(ivtx, iVertexToCol);
						graph1.AddEdge(ivtx, iVertexToRow);
						graph1.AddEdge(iVertexToCol, ivtx);
						graph1.AddEdge(iVertexToRow, ivtx);
					}
				}
				var graph2 = graph1.IsomorphicShuffling(new Random(102));
				// Insert this and you'll wait a LONG time for this test to finish...
				// Note - the above is no longer true now that we have Degree compatibility check
				// graph1.AddEdge(0, cRows * cCols - 1);

				var vfs = new VfState(graph1, graph2, true);
				var matches = vfs.Matches().ToArray();
				Assert.AreNotEqual(0, matches.Length);
			}

			private class VertexColor : IContextCheck
			{
				private readonly string _strColor;

				public VertexColor(string strColor)
				{
					_strColor = strColor;
				}

				public bool FCompatible(IContextCheck icc)
				{
					return ((VertexColor)icc)._strColor == _strColor;
				}
			}

			[Test]
			public void TestContextCheck()
			{
				var graph1 = new Graph<VertexColor, Object>();
				var graph2 = new Graph<VertexColor, Object>();

				graph1.InsertVertex(new VertexColor("Blue"));
				graph1.InsertVertex(new VertexColor("Red"));
				graph1.InsertVertex(new VertexColor("Red"));
				graph1.InsertVertex(new VertexColor("Red"));
				graph1.InsertVertex(new VertexColor("Red"));
				graph1.AddEdge(0, 1);
				graph1.AddEdge(1, 2);
				graph1.AddEdge(2, 3);
				graph1.AddEdge(3, 4);
				graph1.AddEdge(4, 0);

				graph2.InsertVertex(new VertexColor("Red"));
				graph2.InsertVertex(new VertexColor("Red"));
				graph2.InsertVertex(new VertexColor("Red"));
				graph2.InsertVertex(new VertexColor("Blue"));
				graph2.InsertVertex(new VertexColor("Red"));
				graph2.AddEdge(0, 1);
				graph2.AddEdge(1, 2);
				graph2.AddEdge(2, 3);
				graph2.AddEdge(3, 4);
				graph2.AddEdge(4, 0);

				var vfs = new VfState<VertexColor, Object>(graph1, graph2, true);
				var matches = vfs.Matches().ToArray();
				Assert.AreNotEqual(0, matches.Length);
				var mpMatch = matches[0].IsomorphismVid1ToVid2;
				// With no context checking, vertex 0 in the first graph can match
				// vertex 0 in the second graph
				Assert.AreEqual(0, mpMatch[0]);

				vfs = new VfState<VertexColor, Object>(graph1, graph2, true, true);
				matches = vfs.Matches().ToArray();
				Assert.AreNotEqual(0, matches.Length);
				mpMatch = matches[0].IsomorphismVid1ToVid2;
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