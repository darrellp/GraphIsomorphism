using System.Collections.Generic;

namespace vflibcs
{
	public struct FullMapping
	{
		public Dictionary<int, int> IsomorphismNid1ToNid2;
		public Dictionary<int, int> IsomorphismNid2ToNid1;

		internal FullMapping(int count1, int count2)
		{
			IsomorphismNid1ToNid2 = new Dictionary<int, int>(count1);
			IsomorphismNid2ToNid1 = new Dictionary<int, int>(count2);
		}

		internal FullMapping(Dictionary<int, int> dict1, Dictionary<int, int> dict2)
		{
			IsomorphismNid1ToNid2 = new Dictionary<int, int>(dict1);
			IsomorphismNid2ToNid1 = new Dictionary<int, int>(dict2);
		}
	}
}