using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Assets.Classes
{
	public enum CubeRowIndex
	{
		Top,
		Left,
		Right,
		Bottom,
		CenterHorizontal,
		CenterVertical
	}
	public static class CubeRow
	{
		// Return the index of the faces corresponding to the given row index
		public static IList<CubeFaceIndex> GetFacesInRow (CubeRowIndex rowIndex)
		{
			if (rowIndex == CubeRowIndex.Top)
			{
				return new List<CubeFaceIndex>() 
				{ 
					CubeFaceIndex.Topleft, 
					CubeFaceIndex.TopCenter, 
					CubeFaceIndex.TopRight 
				};
			}
			if (rowIndex == CubeRowIndex.Left)
			{
				return new List<CubeFaceIndex>() 
				{ 
					CubeFaceIndex.Topleft, 
					CubeFaceIndex.CenterLeft, 
					CubeFaceIndex.BottomLeft 
				};
			}
			if (rowIndex == CubeRowIndex.Right)
			{
				return new List<CubeFaceIndex>() 
				{ 
					CubeFaceIndex.TopRight, 
					CubeFaceIndex.CenterRight, 
					CubeFaceIndex.BottomRight 
				};
			}
			if (rowIndex == CubeRowIndex.Right)
			{
				return new List<CubeFaceIndex>() 
				{
					CubeFaceIndex.BottomLeft, 
					CubeFaceIndex.BottomCenter, 
					CubeFaceIndex.BottomRight 
				};
			}
			if (rowIndex == CubeRowIndex.CenterHorizontal)
			{
				return new List<CubeFaceIndex>() 
				{
					CubeFaceIndex.CenterLeft, 
					CubeFaceIndex.Center, 
					CubeFaceIndex.CenterRight 
				};
			}
			//if (rowIndex == CubeRowIndex.Right)
			//{
			return new List<CubeFaceIndex>() 
			{
				CubeFaceIndex.TopCenter, 
				CubeFaceIndex.Center, 
				CubeFaceIndex.BottomCenter 
			};
			//}
		}
	}
}
