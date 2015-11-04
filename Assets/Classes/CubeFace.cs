using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assets.Classes
{
	public enum CubeFaceIndex
	{
		Topleft,
		TopCenter,
		TopRight,
		CenterLeft,
		Center,
		CenterRight,
		BottomLeft,
		BottomCenter,
		BottomRight
	};

	public class CubeFace
	{
		// The angle that the face is at currently
		public float currentAngle;

		public IDictionary<CubeFaceIndex, GameObject> CubePieceList;
		// Constructor
		public CubeFace(IDictionary<CubeFaceIndex, GameObject> cubePieceList)
		{
			this.CubePieceList = cubePieceList;
		}

		// Rotate the face in the given angle around the given axis
		public void Rotate(Vector3 axis, float angle)
		{
			// Increase the current angle
			this.currentAngle += angle;
			foreach (var piece in CubePieceList)
			{
				var faceCenter = this.CubePieceList[CubeFaceIndex.Center];
				piece.Value.transform.RotateAround(faceCenter.transform.position, axis, angle);
			}
		}
		// If the cube is done rotating
		public bool IsStabilized()
		{
			return this.currentAngle % 90 == 0;
		}
		// Stabilize the face by moving it to the closest 90 degree angle
		public void Stabilize()
		{

		}
		// Move the position of the pieces
		public void RotatePiecesClockwise()
		{
			// Rotate the corner pieces
			var swapObject = this.CubePieceList[CubeFaceIndex.Topleft];
			this.CubePieceList[CubeFaceIndex.Topleft] = this.CubePieceList[CubeFaceIndex.BottomLeft];
			this.CubePieceList[CubeFaceIndex.BottomLeft] = this.CubePieceList[CubeFaceIndex.BottomRight];
			this.CubePieceList[CubeFaceIndex.BottomRight] = this.CubePieceList[CubeFaceIndex.TopRight];
			this.CubePieceList[CubeFaceIndex.TopRight] = swapObject;

			// Rotate the edge pieces
			swapObject = this.CubePieceList[CubeFaceIndex.TopCenter];
			this.CubePieceList[CubeFaceIndex.TopCenter] = this.CubePieceList[CubeFaceIndex.CenterLeft];
			this.CubePieceList[CubeFaceIndex.CenterLeft] = this.CubePieceList[CubeFaceIndex.BottomCenter];
			this.CubePieceList[CubeFaceIndex.BottomCenter] = this.CubePieceList[CubeFaceIndex.CenterRight];
			this.CubePieceList[CubeFaceIndex.CenterRight] = swapObject;
		}
		public void RotatePiecesCounterclockwise()
		{
			// Rotate the corner pieces
			var swapObject = this.CubePieceList[CubeFaceIndex.Topleft];
			this.CubePieceList[CubeFaceIndex.Topleft] = this.CubePieceList[CubeFaceIndex.TopRight];
			this.CubePieceList[CubeFaceIndex.TopRight] = this.CubePieceList[CubeFaceIndex.BottomRight];
			this.CubePieceList[CubeFaceIndex.BottomRight] = this.CubePieceList[CubeFaceIndex.BottomLeft];
			this.CubePieceList[CubeFaceIndex.BottomLeft] = swapObject;

			// Rotate the edge pieces
			swapObject = this.CubePieceList[CubeFaceIndex.TopCenter];
			this.CubePieceList[CubeFaceIndex.TopCenter] = this.CubePieceList[CubeFaceIndex.CenterRight];
			this.CubePieceList[CubeFaceIndex.CenterRight] = this.CubePieceList[CubeFaceIndex.BottomCenter];
			this.CubePieceList[CubeFaceIndex.BottomCenter] = this.CubePieceList[CubeFaceIndex.CenterLeft];
			this.CubePieceList[CubeFaceIndex.CenterLeft] = swapObject;
		}
		// Return a list of the pieces from the certain row, from left to right and/or top to bottom
		public IList<GameObject> GetPieces(CubeRowIndex rowIndex)
		{
			// Get all the faces in the given row
			var faces = CubeRow.GetFacesInRow(rowIndex);
			
			// Add all the pieces from the face list to the result
			var result = new List<GameObject>();
			foreach (var face in faces)
			{
				result.Add(this.CubePieceList[face]);
			}
			return result;
		}
		// Replace the given rows with the new ones, and return the old row
		public IList<GameObject> ReplaceRow(CubeRowIndex rowIndex, IList<GameObject> newPieces)
		{
			// Get the old row before replacig the new rows
			var oldRow = this.GetPieces(rowIndex);
			// Get all the faces in the given row and replace them with the new piece
			var faces = CubeRow.GetFacesInRow(rowIndex);
			for (int i = 0; i < 3; i++)
			{
				this.CubePieceList[faces[i]] = newPieces[i];
			}
			// Return the value of the old row
			return oldRow;
		}
	}
}
