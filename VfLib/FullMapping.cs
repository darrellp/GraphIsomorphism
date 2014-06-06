using System.Collections.Generic;

namespace vflibcs
{
	public class FullMapping
	{
		public Dictionary<int, int> IsomorphismVid1ToVid2;
		public Dictionary<int, int> IsomorphismVid2ToVid1;

		internal FullMapping(int count1, int count2)
		{
			IsomorphismVid1ToVid2 = new Dictionary<int, int>(count1);
			IsomorphismVid2ToVid1 = new Dictionary<int, int>(count2);
		}

		internal FullMapping(Dictionary<int, int> dict1, Dictionary<int, int> dict2)
		{
			IsomorphismVid1ToVid2 = new Dictionary<int, int>(dict1);
			IsomorphismVid2ToVid1 = new Dictionary<int, int>(dict2);
		}
	}
}