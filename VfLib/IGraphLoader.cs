using System;
using System.Collections;
using System.Collections.Generic;

namespace vflibcs
{
	public interface IGraphLoader<TV, TE>
	{
		// When vertices/edges are inserted, they are given ID's that they keep through their life.
		// They are entered in a list and the indices in the list may not correspond to the
		// id's.  For instance, if 3 vertices are entered they have id's 0, 1, 2 and their IDs
		// correspond to their indices.  However if vertex 0 is deleted then we have the vertices
		// 1 and 2 in positions 0 and 1 so their id's don't correspond to their indices.
		//
		// These two types of identifiers are identified as vid for their id and ivtx for their
		// index in a list.  There is a way to swap back and forth for vertex vertices with IdFromPos
		// and PosFromId.  The edges are always identified by ivtx's.
		int VertexCount { get; }
		int IdFromPos(int ivtx);
		TV GetVertexAttr(int vid);
		int OutEdgeCount(int vid);
		int GetOutEdge(int vid, int ivtxEdge, out TE attr);
		int InEdgeCount(int cEdge);
		int GetInEdge(int vid, int ivtxEdge, out TE attr);
		int PosFromId(int vid);
		IEnumerable<Vertex<TV, TE>> Vertices { get; }
	}

	public interface IGraphLoader : IGraphLoader<Object, Object> {}
}
