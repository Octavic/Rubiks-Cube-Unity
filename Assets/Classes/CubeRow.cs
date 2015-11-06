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
		public static IList<CubePieceIndex> GetFacesInRow (CubeRowIndex rowIndex)
		{
			if (rowIndex == CubeRowIndex.Top)
			{
				return new List<CubePieceIndex>() 
				{ 
					CubePieceIndex.Topleft, 
					CubePieceIndex.TopCenter, 
					CubePieceIndex.TopRight 
				};
			}
			if (rowIndex == CubeRowIndex.Left)
			{
				return new List<CubePieceIndex>() 
				{ 
					CubePieceIndex.Topleft, 
					CubePieceIndex.CenterLeft, 
					CubePieceIndex.BottomLeft 
				};
			}
			if (rowIndex == CubeRowIndex.Right)
			{
				return new List<CubePieceIndex>() 
				{ 
					CubePieceIndex.TopRight, 
					CubePieceIndex.CenterRight, 
					CubePieceIndex.BottomRight 
				};
			}
			if (rowIndex == CubeRowIndex.Bottom)
			{
				return new List<CubePieceIndex>() 
				{
					CubePieceIndex.BottomLeft, 
					CubePieceIndex.BottomCenter, 
					CubePieceIndex.BottomRight 
				};
			}
			if (rowIndex == CubeRowIndex.CenterHorizontal)
			{
				return new List<CubePieceIndex>() 
				{
					CubePieceIndex.CenterLeft, 
					CubePieceIndex.Center, 
					CubePieceIndex.CenterRight 
				};
			}
			//if (rowIndex == CubeRowIndex.CenterVertical)
			//{
			return new List<CubePieceIndex>() 
			{
				CubePieceIndex.TopCenter, 
				CubePieceIndex.Center, 
				CubePieceIndex.BottomCenter 
			};
			//}
		}
	}
}
