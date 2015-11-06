using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assets.Classes
{
	public enum CubePieceIndex
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
		private float currentAngle;

		// The axis around which the face would rotate
		private Vector3 axis;
		// The origin of the rotation
		private Vector3 origin;

		public IDictionary<CubePieceIndex, GameObject> CubePieceList;
		// Constructor
		public CubeFace(IDictionary<CubePieceIndex, GameObject> cubePieceList, Vector3 axis, Vector3 origin)
		{
			this.axis = axis;
			this.origin = origin;
			this.CubePieceList = cubePieceList;
		}
		// Return true if this face contains the given piece
		public bool Contains(GameObject givenPiece)
		{
			var indexList = Enum.GetValues(typeof(CubePieceIndex));
			foreach (CubePieceIndex pieceIndex in indexList)
			{
				if(this.CubePieceList[pieceIndex].name == givenPiece.name)
				{
					return true;
				}
			}
			return false;
		}
		
		// Rotate the face in the given angle clockwise
		public void Rotate(float angle)
		{
			// Increase the current angle
			this.currentAngle += angle;
			foreach (var piece in CubePieceList)
			{
				var faceCenter = this.CubePieceList[CubePieceIndex.Center];
				piece.Value.transform.RotateAround(this.origin, this.axis, angle);
			}
		}
		// Stop the face from rotating
		public void StopRotating()
		{
			foreach (var piece in CubePieceList)
			{
				var faceCenter = this.CubePieceList[CubePieceIndex.Center];
			}
		}
		// Check to see if the cube has been stabilized
		public bool IsStabilized()
		{
			return this.currentAngle % 90 == 0;
		}
		// Clear the amount of angle rotated
		public void ClearRotation()
		{
			if (this.currentAngle % 90 != 0)
			{
				throw new Exception("Something is wrong, you shouldn't reset the angle when it's not correct");
			}
			this.currentAngle = 0;
			
		}
		// Check to see how far the cube has rotated to the closet rotatioin method
		public CubeControl.RotationMethodIndex CheckAmountRotated()
		{
			while (currentAngle < 0)
			{
				currentAngle += 360;
			}
			this.currentAngle = this.currentAngle % 360;
			
			if (currentAngle >= 45 && currentAngle < 135)
			{
				return CubeControl.RotationMethodIndex.Clockwise;
			}
			if (currentAngle >= 135 && currentAngle < 225)
			{
				return CubeControl.RotationMethodIndex.HalfCircle;
			}
			if (currentAngle >= 225 && currentAngle < 315)
			{
				return CubeControl.RotationMethodIndex.Counterclockwise;
			}
			return CubeControl.RotationMethodIndex.None;
		}
		// Stabilize the face by moving it to the closest 90 degree angle with the given velocity
		public void Stabilize(float angularMaxVelocity)
		{
			if (this.IsStabilized())
			{
				return;
			}
			var roundedAngle = this.currentAngle;
			while (roundedAngle < 0)
			{
				roundedAngle += 360;
			}
			if (roundedAngle >= 360)
			{
				roundedAngle %= 360;
			}
			var angularDistance = roundedAngle % 90;
			if (angularDistance > 45)
			{
				angularDistance -= 90;
			}
			if (Math.Abs(angularDistance) < angularMaxVelocity)
			{
				this.Rotate(-angularDistance);
				return;
			}
			if (angularDistance > 0)
			{
				this.Rotate(-angularMaxVelocity);
			}
			else
			{
				this.Rotate(angularMaxVelocity);
			}

		}
		// Move the position of the pieces
		public void RotatePiecesClockwise()
		{
			// Rotate the corner pieces
			var swapObject = this.CubePieceList[CubePieceIndex.Topleft];
			this.CubePieceList[CubePieceIndex.Topleft] = this.CubePieceList[CubePieceIndex.BottomLeft];
			this.CubePieceList[CubePieceIndex.BottomLeft] = this.CubePieceList[CubePieceIndex.BottomRight];
			this.CubePieceList[CubePieceIndex.BottomRight] = this.CubePieceList[CubePieceIndex.TopRight];
			this.CubePieceList[CubePieceIndex.TopRight] = swapObject;

			// Rotate the edge pieces
			swapObject = this.CubePieceList[CubePieceIndex.TopCenter];
			this.CubePieceList[CubePieceIndex.TopCenter] = this.CubePieceList[CubePieceIndex.CenterLeft];
			this.CubePieceList[CubePieceIndex.CenterLeft] = this.CubePieceList[CubePieceIndex.BottomCenter];
			this.CubePieceList[CubePieceIndex.BottomCenter] = this.CubePieceList[CubePieceIndex.CenterRight];
			this.CubePieceList[CubePieceIndex.CenterRight] = swapObject;
		}
		public void RotatePiecesCounterclockwise()
		{
			// Rotate the corner pieces
			var swapObject = this.CubePieceList[CubePieceIndex.Topleft];
			this.CubePieceList[CubePieceIndex.Topleft] = this.CubePieceList[CubePieceIndex.TopRight];
			this.CubePieceList[CubePieceIndex.TopRight] = this.CubePieceList[CubePieceIndex.BottomRight];
			this.CubePieceList[CubePieceIndex.BottomRight] = this.CubePieceList[CubePieceIndex.BottomLeft];
			this.CubePieceList[CubePieceIndex.BottomLeft] = swapObject;

			// Rotate the edge pieces
			swapObject = this.CubePieceList[CubePieceIndex.TopCenter];
			this.CubePieceList[CubePieceIndex.TopCenter] = this.CubePieceList[CubePieceIndex.CenterRight];
			this.CubePieceList[CubePieceIndex.CenterRight] = this.CubePieceList[CubePieceIndex.BottomCenter];
			this.CubePieceList[CubePieceIndex.BottomCenter] = this.CubePieceList[CubePieceIndex.CenterLeft];
			this.CubePieceList[CubePieceIndex.CenterLeft] = swapObject;
		}
		// Return a list of the pieces from the certain row, from left to right and/or top to bottom
		public IList<GameObject> GetPiecesInRow(CubeRowIndex rowIndex)
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
		// Replace the given rows with the new ones, and return the old row. Reverse the new pieces if required
		public IList<GameObject> ReplaceRow(CubeRowIndex rowIndex, IList<GameObject> newPieces, bool reversed)
		{
			// Reverse the new pieces if required
			if (reversed)
			{
				var swapPiece = newPieces[0];
				newPieces[0] = newPieces[2];
				newPieces[2] = swapPiece;
			}
			// Get the old row before replacig the new rows
			var oldRow = this.GetPiecesInRow(rowIndex);
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
