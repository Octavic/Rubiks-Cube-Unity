using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;
using Assets.Classes;

public class CubeControl : MonoBehaviour
{
	// The speed at which the cube can be rotated
	public float rotationVelocity;
	// The speed at which the faces should stabilize themselves after the user let go
	public float stabilizeVelocity;
	// The speed at which the cube should be rotated during undo and redo in degrees per frame
	public float UndoRedoVelcotity;
	// The speed at which the cube should be rotated during scramble in degrees per frame
	public float ScrambleVelocity;
	// How many rotation to do during scramble
	public int ScrambleAmount;
	// The main game camera
	public GameObject mainCamera;

	// The list of available turning methods
	public enum RotationMethodIndex
	{
		None,
		Clockwise,
		Counterclockwise,
		HalfCircle
	}

	// The list of possible ways a pieceFace can be facing
	private enum pieceFaceOrientationList
	{
		TopBottom,
		LeftRight,
		FrontBack
	}
	// The list of available faces
	public enum RotatableCubeFaceIndex
	{
		Top,
		Front,
		Right,
		Back,
		Left,
		Bottom,
		CenterHorizontal,
		CenterVertical,
		CenterSideways
	}

	// A stack to keep track of all moves done
	private Stack<MoveDone> undoStack;
	// A stack to keep track of all moves undone
	private Stack<MoveDone> redoStack;

	// To record a move done
	protected class MoveDone
	{
		public RotatableCubeFaceIndex faceRotated;
		public RotationMethodIndex rotationMethod;
		public MoveDone(RotatableCubeFaceIndex faceRotated, RotationMethodIndex rotationMethod)
		{
			this.faceRotated = faceRotated;
			this.rotationMethod = rotationMethod;
		}
	}
	// To be used in the queue of pending rotations
	// The rules of rotation
	//
	// Axis: The axis around which the face is rotating
	//
	// Affected faces: 
	// The list of faces that would be affected while rotating.
	//		Eg: Rotating white will affect red, blue, orange and green faces
	//
	// Affected rows:
	// The list of the rows that is affected on said faces
	//		Eg: The above rotation will affect the top row of all 4 affected faces
	//
	// Required to flip from previous:
	// If the face row's values needs to be flipped.
	//		Eg: The default orientation is from left to right, or top to bottom. 
	//			If when the row is replaced while the orientation needs to be rotated, the value will be true
	private class CubeFaceRotationRules
	{
		public IList<CubeFace> AffectedFaces;
		public IList<CubeRowIndex> AffectedRowIndex;
		public IList<bool> RequiredToFlipFromPrevious;
		public CubeFaceRotationRules(IList<CubeFace> affectedFaces, IList<CubeRowIndex> affectedRowIndex, IList<bool> requiredToFlipFromPrevious)
		{
			this.AffectedFaces = affectedFaces;
			this.AffectedRowIndex = affectedRowIndex;
			this.RequiredToFlipFromPrevious = requiredToFlipFromPrevious;
		}
	}

	// The classes used in the job queue
	private abstract class IJob
	{
		public RotatableCubeFaceIndex cubeFaceIndex;
		protected CubeControl cube;
		public abstract void Execute();
		public abstract bool IsFinished();
	}
	private class JobRotateByMouse : IJob
	{
		private bool isClockwise;
		private bool isMainMouseX;
		public JobRotateByMouse(RotatableCubeFaceIndex cubeFaceIndex, CubeControl cube, bool isClockwise, bool isMainMouseX)
		{
			this.cubeFaceIndex = cubeFaceIndex;
			this.cube = cube;
			this.isClockwise = isClockwise;
			this.isMainMouseX = isMainMouseX;
		}
		public override void Execute()
		{
			var mouseX = Input.GetAxis("Mouse X");
			var mouseY = Input.GetAxis("Mouse Y");

			//if (this.cubeFaceIndex == null)
			//{
			//	return;
			//}
			if (this.isMainMouseX)
			{
				var degree = mouseX * cube.rotationVelocity;
				if (this.isClockwise)
				{
					cube.cubeFaceList[cubeFaceIndex].Rotate(degree);
				}
				else
				{
					cube.cubeFaceList[cubeFaceIndex].Rotate(-degree);
				}
			}
			else
			{
				var degree = mouseY * cube.rotationVelocity;
				if (this.isClockwise)
				{
					cube.cubeFaceList[cubeFaceIndex].Rotate(degree);
				}
				else
				{
					cube.cubeFaceList[cubeFaceIndex].Rotate(-degree);
				}
			}
		}

		public override bool IsFinished()
		{
			return Input.GetMouseButtonUp(0);
		}
	}
	private class JobRotateFface : IJob
	{
		private float degreesLeft;
		private float rotationVelocity;
		public JobRotateFface(RotatableCubeFaceIndex cubeFaceIndex, CubeControl cube, float degreesLeft, float rotationVelocity)
		{
			this.cubeFaceIndex = cubeFaceIndex;
			this.cube = cube;
			this.degreesLeft = degreesLeft;
			this.rotationVelocity = rotationVelocity;
		}
		public override void Execute()
		{
			if (Math.Abs(degreesLeft) <= rotationVelocity)
			{
				cube.cubeFaceList[cubeFaceIndex].Rotate(degreesLeft);
				degreesLeft = 0;
				return;
			}
			if (degreesLeft > 0)
			{
				cube.cubeFaceList[cubeFaceIndex].Rotate(rotationVelocity);
				degreesLeft -= rotationVelocity;
			}
			else
			{
				cube.cubeFaceList[cubeFaceIndex].Rotate(-rotationVelocity);
				degreesLeft += rotationVelocity;
			}
		}
		public override bool IsFinished()
		{
			return degreesLeft == 0;
		}
	}
	private class JobStabilize : IJob
	{
		public JobStabilize(RotatableCubeFaceIndex cubeFaceIndex, CubeControl cube)
		{
			this.cubeFaceIndex = cubeFaceIndex;
			this.cube = cube;
		}
		public override void Execute()
		{
			cube.cubeFaceList[cubeFaceIndex].Stabilize(cube.stabilizeVelocity);
		}
		public override bool IsFinished()
		{
			return cube.cubeFaceList[cubeFaceIndex].IsStabilized();
		}
	}
	private class JobReassignFaces : IJob
	{
		public JobReassignFaces(RotatableCubeFaceIndex cubeFaceIndex, CubeControl cube)
		{
			this.cubeFaceIndex = cubeFaceIndex;
			this.cube = cube;
		}
		public override void Execute()
		{
			var rotationMethod = this.cube.cubeFaceList[cubeFaceIndex].CheckAmountRotated();
			cube.ReconfigureFacePiecesBasedOnRotation(cubeFaceIndex, rotationMethod);
			cube.cubeFaceList[cubeFaceIndex].ClearRotation();
		}
		public override bool IsFinished()
		{
			return true;
		}

		// Get the move done to be pushed into the undo/redo stack
		public MoveDone GetMove()
		{
			return new MoveDone(cubeFaceIndex, this.cube.cubeFaceList[cubeFaceIndex].CheckAmountRotated());
		}
	}


	// List of faces and rules for the faces to rotate in
	protected IDictionary<RotatableCubeFaceIndex, CubeFace> cubeFaceList;
	private IDictionary<RotatableCubeFaceIndex, CubeFaceRotationRules> cubeFaceRotationRulesList;

	// If the mouse is still and we are awaiting an input
	private bool isWaitingMouseMovement;

	// The random number generator
	System.Random randomNumberGenerator;

	// The queue  pending moves
	private Queue<IJob> jobQueue;

	// The current mouse x and mouse y positions, updated every frame
	private float mouseX;
	private float mouseY;

	// If there's steps left to undo/redo
	public bool HasUndoStepsLeft()
	{
		return this.undoStack.Count > 0;
	}
	public bool HasRedoStepsLeft()
	{
		return this.redoStack.Count > 0;
	}

	// Undo/Redo the last move
	public void Undo()
	{
		// Pop the move and find the opposite rotation
		var move = this.undoStack.Pop();
		var oppositeRotation = CubeControl.FindOppositeMethod(move.rotationMethod);

		//this.ReconfigureFacePiecesBasedOnRotation(move.faceRotated, oppositeRotation);
		//this.RotateFaceBasedOnMethod(move.faceRotated, oppositeRotation);
		//this.cubeFaceList[move.faceRotated].ClearRotation();

		// Insert the new job into the job queue followed by a stabilize and reassign
		this.jobQueue.Enqueue(new JobRotateFface(move.faceRotated, this, GetDegreeFromRotationMethod(oppositeRotation), this.UndoRedoVelcotity));
		this.jobQueue.Enqueue(new JobStabilize(move.faceRotated, this));
		this.jobQueue.Enqueue(new JobReassignFaces(move.faceRotated, this));

		// Add the undid move to the redo stack
		this.redoStack.Push(move);
	}
	public void Redo()
	{
		// pop the move
		var move = this.redoStack.Pop();

		//this.ReconfigureFacePiecesBasedOnRotation(move.faceRotated, move.rotationMethod);
		//RotateFaceBasedOnMethod(move.faceRotated, move.rotationMethod);
		//this.cubeFaceList[move.faceRotated].ClearRotation();

		// Insert the new job into the job queue
		this.jobQueue.Enqueue(new JobRotateFface(move.faceRotated, this, GetDegreeFromRotationMethod(move.rotationMethod), this.UndoRedoVelcotity));
		this.jobQueue.Enqueue(new JobStabilize(move.faceRotated, this));
		this.jobQueue.Enqueue(new JobReassignFaces(move.faceRotated, this));

		// Add the redid move back into undo stack
		this.undoStack.Push(move);
	}

	// Scramble the cube
	public void Scramble()
	{
		// Clear the undo and redo stack
		this.undoStack.Clear();
		this.redoStack.Clear();

		// The amount of total rotatable faces
		var totalRotatableFacesAmount = Enum.GetNames(typeof(RotatableCubeFaceIndex)).Length;
		// The amount of total possible rotation methods
		var totalRotationMethodAmount = Enum.GetNames(typeof(RotationMethodIndex)).Length;

		// The face previously rotated so we don't do it again
		var previousFace = RotatableCubeFaceIndex.Front;
		
		// Loop for the amount required to scramble and do a random move on a random face everytime
		for (int i = 0; i < this.ScrambleAmount; i++)
		{
			// Get the randomized face
			var currentFace = (RotatableCubeFaceIndex)randomNumberGenerator.Next(0, totalRotatableFacesAmount);
			// If the face is the same as the previous one, change it
			while (currentFace == previousFace)
			{
				currentFace = (RotatableCubeFaceIndex)randomNumberGenerator.Next(0, totalRotatableFacesAmount);
			}
			previousFace = currentFace;
            var currentMethod = (RotationMethodIndex)randomNumberGenerator.Next(1, totalRotationMethodAmount);

			// Push the current move into the job queue
			this.jobQueue.Enqueue(new JobRotateFface(currentFace, this, this.GetDegreeFromRotationMethod(currentMethod), this.ScrambleVelocity));
			// Push stabilize and reassign
			this.jobQueue.Enqueue(new JobStabilize(currentFace, this));
			this.jobQueue.Enqueue(new JobReassignFaces(currentFace, this));

			// For debugging, insert a lit of all done moves
			Debug.Log(currentFace.ToString() + "  " + currentMethod.ToString());
		}
	}

	// Find the opposite move
	public static RotationMethodIndex FindOppositeMethod(RotationMethodIndex rotationMethodIndex)
	{
		switch (rotationMethodIndex)
		{
			case RotationMethodIndex.Counterclockwise:
				{
					return RotationMethodIndex.Clockwise;
				}
			case RotationMethodIndex.Clockwise:
				{
					return RotationMethodIndex.Counterclockwise;
				}
			case RotationMethodIndex.HalfCircle:
				{
					return RotationMethodIndex.HalfCircle;
				}
			default:
				{
					break;
				}
		}
		return RotationMethodIndex.None;
	}

	//// Insert the given move into the undo stack
	//// public void InsertIntoUndoStack(RotatableCubeFaceIndex index, RotationMethodIndex method)
	////{
	////	this.undoStack.Push(new MoveDone(index, method));
	////}

	// Reassgin the center cube faces
	protected void ReassignCenterPieceFaces()
	{
		// Reassgin the center horizontal layer
		cubeFaceList[RotatableCubeFaceIndex.CenterHorizontal].ReplaceRow(CubeRowIndex.Top, cubeFaceList[RotatableCubeFaceIndex.Back].GetPiecesInRow(CubeRowIndex.CenterHorizontal), true);
		cubeFaceList[RotatableCubeFaceIndex.CenterHorizontal].ReplaceRow(CubeRowIndex.Bottom, cubeFaceList[RotatableCubeFaceIndex.Front].GetPiecesInRow(CubeRowIndex.CenterHorizontal), false);
		cubeFaceList[RotatableCubeFaceIndex.CenterHorizontal].CubePieceList[CubePieceIndex.CenterLeft] = cubeFaceList[RotatableCubeFaceIndex.Left].CubePieceList[CubePieceIndex.Center];
		cubeFaceList[RotatableCubeFaceIndex.CenterHorizontal].CubePieceList[CubePieceIndex.CenterRight] = cubeFaceList[RotatableCubeFaceIndex.Right].CubePieceList[CubePieceIndex.Center];
		// Reassgin the center vertical layer
		cubeFaceList[RotatableCubeFaceIndex.CenterVertical].ReplaceRow(CubeRowIndex.Top, cubeFaceList[RotatableCubeFaceIndex.Top].GetPiecesInRow(CubeRowIndex.CenterVertical), true);
		cubeFaceList[RotatableCubeFaceIndex.CenterVertical].ReplaceRow(CubeRowIndex.Bottom, cubeFaceList[RotatableCubeFaceIndex.Bottom].GetPiecesInRow(CubeRowIndex.CenterVertical), false);
		cubeFaceList[RotatableCubeFaceIndex.CenterVertical].CubePieceList[CubePieceIndex.CenterLeft] = cubeFaceList[RotatableCubeFaceIndex.Front].CubePieceList[CubePieceIndex.Center];
		cubeFaceList[RotatableCubeFaceIndex.CenterVertical].CubePieceList[CubePieceIndex.CenterRight] = cubeFaceList[RotatableCubeFaceIndex.Back].CubePieceList[CubePieceIndex.Center];
		// Reassgin the center vertical layer
		cubeFaceList[RotatableCubeFaceIndex.CenterSideways].ReplaceRow(CubeRowIndex.Top, cubeFaceList[RotatableCubeFaceIndex.Top].GetPiecesInRow(CubeRowIndex.CenterHorizontal), false);
		cubeFaceList[RotatableCubeFaceIndex.CenterSideways].ReplaceRow(CubeRowIndex.Bottom, cubeFaceList[RotatableCubeFaceIndex.Bottom].GetPiecesInRow(CubeRowIndex.CenterHorizontal), false);
		cubeFaceList[RotatableCubeFaceIndex.CenterSideways].CubePieceList[CubePieceIndex.CenterLeft] = cubeFaceList[RotatableCubeFaceIndex.Left].CubePieceList[CubePieceIndex.Center];
		cubeFaceList[RotatableCubeFaceIndex.CenterSideways].CubePieceList[CubePieceIndex.CenterRight] = cubeFaceList[RotatableCubeFaceIndex.Right].CubePieceList[CubePieceIndex.Center];
	}

	// Rotate the given face with the given method
	protected void ReconfigureFacePiecesBasedOnRotation(RotatableCubeFaceIndex faceIndex, RotationMethodIndex rotationMethod)
	{
		if (rotationMethod == RotationMethodIndex.None)
		{
			return;
		}
		if (rotationMethod == RotationMethodIndex.Clockwise || rotationMethod == RotationMethodIndex.HalfCircle)
		{
			// Rotate the pieces themselves first
			this.cubeFaceList[faceIndex].RotatePiecesClockwise();
			var affectedFaceList = this.cubeFaceRotationRulesList[faceIndex];
			// Initialize the first row to be inserted
			var replaceRow = affectedFaceList.AffectedFaces[3].GetPiecesInRow(affectedFaceList.AffectedRowIndex[3]);
			for (int i = 0; i < 4; i++)
			{
				replaceRow = affectedFaceList.AffectedFaces[i].ReplaceRow(
					affectedFaceList.AffectedRowIndex[i],
					replaceRow,
					affectedFaceList.RequiredToFlipFromPrevious[i]);
			}
			this.ReassignCenterPieceFaces();
			// Rotate the blocks themselves
			// this.cubeFaceList[faceIndex].Rotate(90);
			// If half circle, do it again
			if (rotationMethod == RotationMethodIndex.HalfCircle)
			{
				this.ReconfigureFacePiecesBasedOnRotation(faceIndex, RotationMethodIndex.Clockwise);
				this.ReassignCenterPieceFaces();
			}
			return;
		}
		else
		{
			// Rotate the pieces themselves first
			this.cubeFaceList[faceIndex].RotatePiecesCounterclockwise();
			var affectedFaceList = this.cubeFaceRotationRulesList[faceIndex];
			// Initialize the first row to be inserted
			var replaceRow = affectedFaceList.AffectedFaces[0].GetPiecesInRow(affectedFaceList.AffectedRowIndex[0]);
			for (int i = 3; i >= 0; i--)
			{

				replaceRow = affectedFaceList.AffectedFaces[i].ReplaceRow(
					affectedFaceList.AffectedRowIndex[i],
					replaceRow,
					// for face 0 to rotate to face 1, use the flipped bool for face 1
					// thus, use i+1. If i+1>3, then that means it's the first cube. use 0 instead
					affectedFaceList.RequiredToFlipFromPrevious[i + 1 > 3 ? 0 : i + 1]);
			}
			// Rotate the blocks themselves
			// this.cubeFaceList[faceIndex].Rotate(-90);
		}
		this.ReassignCenterPieceFaces();
	}

	// Determine if the given game object is on the surface of the cube piece
	protected bool IsPieceOnSurfaceOfCubeFace(GameObject givenPieceFace, RotatableCubeFaceIndex cubeFaceIndex)
	{
		// If the piece is not contained in the face, return false
		var givenPiece = givenPieceFace.transform.parent;
		if (!this.cubeFaceList[cubeFaceIndex].Contains(givenPiece.gameObject))
		{
			return false;
		}
		
		// Get the blakc base of the given piece
		Transform givenPieceBase = null;
		for(int i =0;i < givenPiece.childCount; i ++)
		{
			givenPieceBase = givenPiece.GetChild(i);
			if(givenPieceBase.transform.localScale.x == 2)
			{
				break;
			}
		}
		if (cubeFaceIndex == RotatableCubeFaceIndex.Top || cubeFaceIndex == RotatableCubeFaceIndex.Bottom)
		{
			return givenPieceFace.transform.position.y != givenPieceBase.transform.position.y;
		}
		if (cubeFaceIndex == RotatableCubeFaceIndex.Left || cubeFaceIndex == RotatableCubeFaceIndex.Right)
		{
			return givenPieceFace.transform.position.x != givenPieceBase.transform.position.x;
		}
		return givenPieceFace.transform.position.z != givenPieceBase.transform.position.z;

	}

	// Rotate the given face based on the rotation method
	protected void RotateFaceBasedOnMethod(RotatableCubeFaceIndex cubeFaceIndex, RotationMethodIndex rotationMethod)
	{
		switch (rotationMethod)
		{
			case RotationMethodIndex.Clockwise:
				{
					this.cubeFaceList[cubeFaceIndex].Rotate(90);
					return;
				}
			case RotationMethodIndex.Counterclockwise:
				{
					this.cubeFaceList[cubeFaceIndex].Rotate(-90);
					return;
				}
			case RotationMethodIndex.HalfCircle:
				{
					this.cubeFaceList[cubeFaceIndex].Rotate(180);
					return;
				}
			default:
				{
					break;
				}
		}
		return;
	}

	// Determine the face currently being rotated from mouse, the direction and relation to mouse X or Y
    private void InitializeCurrentRotation()
	{
		// Get the gameObject that got clicked on with a ray
		GameObject stickerClicked = null;
		Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
		RaycastHit hit;
		if (Physics.Raycast(ray, out hit))
		{
			stickerClicked = hit.collider.gameObject;
		}
		// If nothing is clicked, return
		if (stickerClicked == null || stickerClicked.transform.parent == null)
		{
			return;
		}

		//// Start the rotation
		//this.isBeingRotated = true;

		// Find the parent of the piece
		var pieceClicked = stickerClicked.transform.parent.gameObject;

		// Find the position of the main camera and use its X and Z values to determine the rotation
		var cameraX = mainCamera.transform.position.x;
		var cameraZ = mainCamera.transform.position.z;

		// Find if the mouse is moving more in X axis or Y axis to determine the main axis
		var isMainMouseAxisX = (Math.Abs(this.mouseX) > Math.Abs(this.mouseY));

		// Define the current faces relative to the camera
		RotatableCubeFaceIndex currentFrontFace;
		RotatableCubeFaceIndex currentRightFace;
		RotatableCubeFaceIndex currentBackFace;
		RotatableCubeFaceIndex currentLeftFace;
		//RotatableCubeFaceIndex currentCenterHorizontal;
		RotatableCubeFaceIndex currentCenterVertical;
		RotatableCubeFaceIndex currentCenterSideways;
		// When the current front is orange, the center veritcal is reversed. use the following bool values to indicete 
		// if the current veritcal and sideways movements is normal or needs to be reversed
		bool IsCurrentVerticalNormal = false;
		bool IsCurrentSidewaysNormal = false;

		// Assign the current cube faces using the camera position
		#region Assign current cube faces
		if ((cameraZ > 0) && (Math.Abs(cameraZ) > Math.Abs(cameraX)))
		{
			currentFrontFace = RotatableCubeFaceIndex.Front;
			currentRightFace = RotatableCubeFaceIndex.Right;
			currentBackFace = RotatableCubeFaceIndex.Back;
			currentLeftFace = RotatableCubeFaceIndex.Left;
			//currentCenterHorizontal = RotatableCubeFaceIndex.CenterHorizontal;
			currentCenterVertical = RotatableCubeFaceIndex.CenterVertical;
			currentCenterSideways = RotatableCubeFaceIndex.CenterSideways;
			IsCurrentVerticalNormal = true;
			IsCurrentSidewaysNormal = true;

		}
		else if ((cameraZ < 0) && (Math.Abs(cameraZ) > Math.Abs(cameraX)))
		{
			currentFrontFace = RotatableCubeFaceIndex.Back;
			currentRightFace = RotatableCubeFaceIndex.Left;
			currentBackFace = RotatableCubeFaceIndex.Front;
			currentLeftFace = RotatableCubeFaceIndex.Right;
			//currentCenterHorizontal = RotatableCubeFaceIndex.CenterHorizontal;
			currentCenterVertical = RotatableCubeFaceIndex.CenterVertical;
			currentCenterSideways = RotatableCubeFaceIndex.CenterSideways;
			IsCurrentVerticalNormal = false;
			IsCurrentSidewaysNormal = false;
		}
		else if (cameraX > 0)
		{
			currentFrontFace = RotatableCubeFaceIndex.Left;
			currentRightFace = RotatableCubeFaceIndex.Front;
			currentBackFace = RotatableCubeFaceIndex.Right;
			currentLeftFace = RotatableCubeFaceIndex.Back;
			//currentCenterHorizontal = RotatableCubeFaceIndex.CenterHorizontal;
			currentCenterVertical = RotatableCubeFaceIndex.CenterSideways;
			currentCenterSideways = RotatableCubeFaceIndex.CenterVertical;
			IsCurrentVerticalNormal = true;
			IsCurrentSidewaysNormal = false;
		}
		else
		{
			currentFrontFace = RotatableCubeFaceIndex.Right;
			currentRightFace = RotatableCubeFaceIndex.Back;
			currentBackFace = RotatableCubeFaceIndex.Left;
			currentLeftFace = RotatableCubeFaceIndex.Front;
			//currentCenterHorizontal = RotatableCubeFaceIndex.CenterHorizontal;
			currentCenterVertical = RotatableCubeFaceIndex.CenterSideways;
			currentCenterSideways = RotatableCubeFaceIndex.CenterVertical;
			IsCurrentVerticalNormal = false;
			IsCurrentSidewaysNormal = true;
		}
		#endregion 

		// If the piece is contained in each layer
		var isPieceInTop = this.cubeFaceList[RotatableCubeFaceIndex.Top].Contains(pieceClicked);
		var isPieceInBottom = this.cubeFaceList[RotatableCubeFaceIndex.Bottom].Contains(pieceClicked);
		var isPieceInCurrentLeft = this.cubeFaceList[currentLeftFace].Contains(pieceClicked);
		var isPieceInCurrentRight = this.cubeFaceList[currentRightFace].Contains(pieceClicked);
		var isPieceInCurrentFront = this.cubeFaceList[currentFrontFace].Contains(pieceClicked);
		var isPieceInCurrentBack = this.cubeFaceList[currentBackFace].Contains(pieceClicked);

		// Values required to define rotation
		var currentRotatingFaceIndex = RotatableCubeFaceIndex.Front;
		var isCurrentFaceRotatingClockwise = true;

		// When dealing with the clicked sticker being on top
		#region Sticker On Top
		if (this.IsPieceOnSurfaceOfCubeFace(stickerClicked, RotatableCubeFaceIndex.Top))
		{
			// When moving mouse side ways, rotate the respective faces
			if (isMainMouseAxisX)
			{
				if (isPieceInCurrentBack)
				{
					currentRotatingFaceIndex = currentBackFace;
					isCurrentFaceRotatingClockwise = false;
				}
				else if (isPieceInCurrentFront)
				{
					currentRotatingFaceIndex = currentFrontFace;
					isCurrentFaceRotatingClockwise = true;
				}
				else
				{
					currentRotatingFaceIndex = currentCenterSideways;
					isCurrentFaceRotatingClockwise = IsCurrentSidewaysNormal;
				}
			}
			// When the mouse if moving up and down, rotate the respective faces
			else if (isPieceInCurrentLeft)
			{
				currentRotatingFaceIndex = currentLeftFace;
				isCurrentFaceRotatingClockwise = false;
			}
			else if (isPieceInCurrentRight)
			{
				currentRotatingFaceIndex = currentRightFace;
				isCurrentFaceRotatingClockwise = true;
			}
			else
			{
				currentRotatingFaceIndex = currentCenterVertical;
				isCurrentFaceRotatingClockwise = IsCurrentVerticalNormal;
			}
		}
		#endregion
		// When dealing with the clicked sticker being on front
		#region Sticker On Front
		else if (this.IsPieceOnSurfaceOfCubeFace(stickerClicked, currentFrontFace))
		{
			// When moving mouse side ways, rotate the respective faces
			if (isMainMouseAxisX)
			{
				if (isPieceInTop)
				{
					currentRotatingFaceIndex = RotatableCubeFaceIndex.Top;
					isCurrentFaceRotatingClockwise = false;
				}
				else if (isPieceInBottom)
				{
					currentRotatingFaceIndex = RotatableCubeFaceIndex.Bottom;
					isCurrentFaceRotatingClockwise = true;
				}
				else
				{
					currentRotatingFaceIndex = RotatableCubeFaceIndex.CenterHorizontal;
					isCurrentFaceRotatingClockwise = false;
				}
			}
			// When the mouse if moving up and down, rotate the respective faces
			else if (isPieceInCurrentLeft)
			{
				currentRotatingFaceIndex = currentLeftFace;
				isCurrentFaceRotatingClockwise = false;
			}
			else if (isPieceInCurrentRight)
			{
				currentRotatingFaceIndex = currentRightFace;
				isCurrentFaceRotatingClockwise = true;
			}
			else
			{
				currentRotatingFaceIndex = currentCenterVertical;
				isCurrentFaceRotatingClockwise = IsCurrentVerticalNormal;
			}
		}
		#endregion
		// When dealing with the clicked sticker being on left
		#region Sticker On Left
		else if (this.IsPieceOnSurfaceOfCubeFace(stickerClicked, currentLeftFace))
		{
			// When moving mouse side ways, rotate the respective faces
			if (isMainMouseAxisX)
			{
				if (isPieceInTop)
				{
					currentRotatingFaceIndex = RotatableCubeFaceIndex.Top;
					isCurrentFaceRotatingClockwise = false;
				}
				else if (isPieceInBottom)
				{
					currentRotatingFaceIndex = RotatableCubeFaceIndex.Bottom;
					isCurrentFaceRotatingClockwise = true;
				}
				else
				{
					currentRotatingFaceIndex = RotatableCubeFaceIndex.CenterHorizontal;
					isCurrentFaceRotatingClockwise = false;
				}
			}
			// When the mouse if moving up and down, rotate the respective faces
			else if (isPieceInCurrentFront)
			{
				currentRotatingFaceIndex = currentFrontFace;
				isCurrentFaceRotatingClockwise = true;
			}
			else if (isPieceInCurrentBack)
			{
				currentRotatingFaceIndex = currentBackFace;
				isCurrentFaceRotatingClockwise = false;
			}
			else
			{
				currentRotatingFaceIndex = currentCenterSideways;
				isCurrentFaceRotatingClockwise = IsCurrentSidewaysNormal;
			}
		}
		#endregion
		// When dealing with the clicked sticker being on right
		#region Sticker On Right
		else if (this.IsPieceOnSurfaceOfCubeFace(stickerClicked, currentRightFace))
		{
			// When moving mouse side ways, rotate the respective faces
			if (isMainMouseAxisX)
			{
				if (isPieceInTop)
				{
					currentRotatingFaceIndex = RotatableCubeFaceIndex.Top;
					isCurrentFaceRotatingClockwise = false;
				}
				else if (isPieceInBottom)
				{
					currentRotatingFaceIndex = RotatableCubeFaceIndex.Bottom;
					isCurrentFaceRotatingClockwise = true;
				}
				else
				{
					currentRotatingFaceIndex = RotatableCubeFaceIndex.CenterHorizontal;
					isCurrentFaceRotatingClockwise = false;
				}
			}
			// When the mouse if moving up and down, rotate the respective faces
			else if (isPieceInCurrentFront)
			{
				currentRotatingFaceIndex = currentFrontFace;
				isCurrentFaceRotatingClockwise = false;
			}
			else if (isPieceInCurrentBack)
			{
				currentRotatingFaceIndex = currentBackFace;
				isCurrentFaceRotatingClockwise = true;
			}
			else
			{
				currentRotatingFaceIndex = currentCenterSideways;
				isCurrentFaceRotatingClockwise = !IsCurrentSidewaysNormal;
			}
		}
		#endregion
		// When dealing with the clicked sticker being on bottom
		#region Sticker On Bottom
		else// if (this.IsPieceOnSurfaceOfCubeFace(stickerClicked, RotatableCubeFaceIndex.Bottom))
		{
			// When moving mouse side ways, rotate the respective faces
			if (isMainMouseAxisX)
			{
				if (isPieceInCurrentBack)
				{
					currentRotatingFaceIndex = currentBackFace;
					isCurrentFaceRotatingClockwise = true;
				}
				else if (isPieceInCurrentFront)
				{
					currentRotatingFaceIndex = currentFrontFace;
					isCurrentFaceRotatingClockwise = false;
				}
				else
				{
					currentRotatingFaceIndex = currentCenterSideways;
					isCurrentFaceRotatingClockwise = !IsCurrentSidewaysNormal;
				}
			}
			// When the mouse if moving up and down, rotate the respective faces
			else if (isPieceInCurrentLeft)
			{
				currentRotatingFaceIndex = currentLeftFace;
				isCurrentFaceRotatingClockwise = false;
			}
			else if (isPieceInCurrentRight)
			{
				currentRotatingFaceIndex = currentRightFace;
				isCurrentFaceRotatingClockwise = true;
			}
			else
			{
				currentRotatingFaceIndex = currentCenterVertical;
				isCurrentFaceRotatingClockwise = IsCurrentVerticalNormal;
			}
		}
		#endregion

		// Insert the defined rotation into the job queue, then stabilize and reassign
		this.jobQueue.Enqueue(new JobRotateByMouse(currentRotatingFaceIndex, this, isCurrentFaceRotatingClockwise, isMainMouseAxisX));
		this.jobQueue.Enqueue(new JobStabilize(currentRotatingFaceIndex, this));
		this.jobQueue.Enqueue(new JobReassignFaces(currentRotatingFaceIndex, this));
	}

	// Give the degree corresponding to the rotation method
	private int GetDegreeFromRotationMethod(RotationMethodIndex rotationMethod)
	{
		switch (rotationMethod)
		{
			case RotationMethodIndex.Clockwise:
				return 90;
			case RotationMethodIndex.Counterclockwise:
				return -90;
			case RotationMethodIndex.HalfCircle:
				return 180;
			default:
				break;
		}
		return 0;
	}

	// Use this for initialization
	void Start()
	{
		// Initialize values;
		this.isWaitingMouseMovement = false;
		this.undoStack = new Stack<MoveDone>();
		this.redoStack = new Stack<MoveDone>();
		this.randomNumberGenerator = new System.Random();

		// Initialize the pending rotation queue
		this.jobQueue = new Queue<IJob>();

		// Initialize the facelist
		this.cubeFaceList = new Dictionary<RotatableCubeFaceIndex, CubeFace>();
		this.cubeFaceRotationRulesList = new Dictionary<RotatableCubeFaceIndex, CubeFaceRotationRules>();

		//Initilizing and configuring the child cube faces into cube face
		#region Acquire cube pieces
		// Get the center cube piece
		var centerCube = this.transform.FindChild("CenterCube").gameObject;
		// Get all the center cube pieces
		var centerWhite = this.transform.FindChild("CenterWhite").gameObject;
		var centerRed = this.transform.FindChild("CenterRed").gameObject;
		var centerBlue = this.transform.FindChild("CenterBlue").gameObject;
		var centerOrange = this.transform.FindChild("CenterOrange").gameObject;
		var centerGreen = this.transform.FindChild("CenterGreen").gameObject;
		var centerYellow = this.transform.FindChild("CenterYellow").gameObject;
		// Get all the corner pieces
		var cornerWhiteRedBlue = this.transform.FindChild("CornerWhiteRedBlue").gameObject;
		var cornerWhiteBlueOrange = this.transform.FindChild("CornerWhiteBlueOrange").gameObject;
		var cornerWhiteOrangeGreen = this.transform.FindChild("CornerWhiteOrangeGreen").gameObject;
		var cornerWhiteGreenRed = this.transform.FindChild("CornerWhiteGreenRed").gameObject;
		var cornerYellowRedBlue = this.transform.FindChild("CornerYellowRedBlue").gameObject;
		var cornerYellowBlueOrange = this.transform.FindChild("CornerYellowBlueOrange").gameObject;
		var cornerYellowOrangeGreen = this.transform.FindChild("CornerYellowOrangeGreen").gameObject;
		var cornerYellowGreenRed = this.transform.FindChild("CornerYellowGreenRed").gameObject;
		// Get all the edge pieces
		var edgeWhiteRed = this.transform.FindChild("EdgeWhiteRed").gameObject;
		var edgeWhiteBlue = this.transform.FindChild("EdgeWhiteBlue").gameObject;
		var edgeWhiteOrange = this.transform.FindChild("EdgeWhiteOrange").gameObject;
		var edgeWhiteGreen = this.transform.FindChild("EdgeWhiteGreen").gameObject;
		var edgeRedBlue = this.transform.FindChild("EdgeRedBlue").gameObject;
		var edgeBlueOrange = this.transform.FindChild("EdgeBlueOrange").gameObject;
		var edgeOrangeGreen = this.transform.FindChild("EdgeOrangeGreen").gameObject;
		var edgeGreenRed = this.transform.FindChild("EdgeGreenRed").gameObject;
		var edgeYellowRed = this.transform.FindChild("EdgeYellowRed").gameObject;
		var edgeYellowBlue = this.transform.FindChild("EdgeYellowBlue").gameObject;
		var edgeYellowOrange = this.transform.FindChild("EdgeYellowOrange").gameObject;
		var edgeYellowGreen = this.transform.FindChild("EdgeYellowGreen").gameObject;

		// Assign the correct center, edge and corner pieces to the white face
		var whiteCubeFaceValues = new Dictionary<CubePieceIndex, GameObject>();
		whiteCubeFaceValues.Add(CubePieceIndex.TopCenter, edgeWhiteOrange);
		whiteCubeFaceValues.Add(CubePieceIndex.CenterLeft, edgeWhiteGreen);
		whiteCubeFaceValues.Add(CubePieceIndex.Center, centerWhite);
		whiteCubeFaceValues.Add(CubePieceIndex.CenterRight, edgeWhiteBlue);
		whiteCubeFaceValues.Add(CubePieceIndex.BottomCenter, edgeWhiteRed);
		whiteCubeFaceValues.Add(CubePieceIndex.Topleft, cornerWhiteOrangeGreen);
		whiteCubeFaceValues.Add(CubePieceIndex.TopRight, cornerWhiteBlueOrange);
		whiteCubeFaceValues.Add(CubePieceIndex.BottomLeft, cornerWhiteGreenRed);
		whiteCubeFaceValues.Add(CubePieceIndex.BottomRight, cornerWhiteRedBlue);
		var whiteCubeFace = new CubeFace(whiteCubeFaceValues, this.transform.up, centerWhite.transform.position);

		// Assign the correct center, edge and corner pieces to the red face
		var redCubeFaceValues = new Dictionary<CubePieceIndex, GameObject>();
		redCubeFaceValues.Add(CubePieceIndex.TopCenter, edgeWhiteRed);
		redCubeFaceValues.Add(CubePieceIndex.CenterLeft, edgeGreenRed);
		redCubeFaceValues.Add(CubePieceIndex.Center, centerRed);
		redCubeFaceValues.Add(CubePieceIndex.CenterRight, edgeRedBlue);
		redCubeFaceValues.Add(CubePieceIndex.BottomCenter, edgeYellowRed);
		redCubeFaceValues.Add(CubePieceIndex.Topleft, cornerWhiteGreenRed);
		redCubeFaceValues.Add(CubePieceIndex.TopRight, cornerWhiteRedBlue);
		redCubeFaceValues.Add(CubePieceIndex.BottomLeft, cornerYellowGreenRed);
		redCubeFaceValues.Add(CubePieceIndex.BottomRight, cornerYellowRedBlue);
		var redCubeFace = new CubeFace(redCubeFaceValues, this.transform.forward, centerRed.transform.position);

		// Assign the correct center, edge and corner pieces to the blue face
		var blueCubeFaceValues = new Dictionary<CubePieceIndex, GameObject>();
		blueCubeFaceValues.Add(CubePieceIndex.TopCenter, edgeWhiteBlue);
		blueCubeFaceValues.Add(CubePieceIndex.CenterLeft, edgeRedBlue);
		blueCubeFaceValues.Add(CubePieceIndex.Center, centerBlue);
		blueCubeFaceValues.Add(CubePieceIndex.CenterRight, edgeBlueOrange);
		blueCubeFaceValues.Add(CubePieceIndex.BottomCenter, edgeYellowBlue);
		blueCubeFaceValues.Add(CubePieceIndex.Topleft, cornerWhiteRedBlue);
		blueCubeFaceValues.Add(CubePieceIndex.TopRight, cornerWhiteBlueOrange);
		blueCubeFaceValues.Add(CubePieceIndex.BottomLeft, cornerYellowRedBlue);
		blueCubeFaceValues.Add(CubePieceIndex.BottomRight, cornerYellowBlueOrange);
		var blueCubeFace = new CubeFace(blueCubeFaceValues, this.transform.right * -1, centerBlue.transform.position);

		// Assign the correct center, edge and corner pieces to the orange face
		var orangeCubeFaceValues = new Dictionary<CubePieceIndex, GameObject>();
		orangeCubeFaceValues.Add(CubePieceIndex.TopCenter, edgeWhiteOrange);
		orangeCubeFaceValues.Add(CubePieceIndex.CenterLeft, edgeBlueOrange);
		orangeCubeFaceValues.Add(CubePieceIndex.Center, centerOrange);
		orangeCubeFaceValues.Add(CubePieceIndex.CenterRight, edgeOrangeGreen);
		orangeCubeFaceValues.Add(CubePieceIndex.BottomCenter, edgeYellowOrange);
		orangeCubeFaceValues.Add(CubePieceIndex.Topleft, cornerWhiteBlueOrange);
		orangeCubeFaceValues.Add(CubePieceIndex.TopRight, cornerWhiteOrangeGreen);
		orangeCubeFaceValues.Add(CubePieceIndex.BottomLeft, cornerYellowBlueOrange);
		orangeCubeFaceValues.Add(CubePieceIndex.BottomRight, cornerYellowOrangeGreen);
		var orangeCubeFace = new CubeFace(orangeCubeFaceValues, this.transform.forward * -1, centerOrange.transform.position);

		// Assign the correct center, edge and corner pieces to the green face
		var greenCubeFaceValues = new Dictionary<CubePieceIndex, GameObject>();
		greenCubeFaceValues.Add(CubePieceIndex.TopCenter, edgeWhiteGreen);
		greenCubeFaceValues.Add(CubePieceIndex.CenterLeft, edgeOrangeGreen);
		greenCubeFaceValues.Add(CubePieceIndex.Center, centerGreen);
		greenCubeFaceValues.Add(CubePieceIndex.CenterRight, edgeGreenRed);
		greenCubeFaceValues.Add(CubePieceIndex.BottomCenter, edgeYellowGreen);
		greenCubeFaceValues.Add(CubePieceIndex.Topleft, cornerWhiteOrangeGreen);
		greenCubeFaceValues.Add(CubePieceIndex.TopRight, cornerWhiteGreenRed);
		greenCubeFaceValues.Add(CubePieceIndex.BottomLeft, cornerYellowOrangeGreen);
		greenCubeFaceValues.Add(CubePieceIndex.BottomRight, cornerYellowGreenRed);
		var greenCubeFace = new CubeFace(greenCubeFaceValues, this.transform.right, centerGreen.transform.position);

		// Assign the correct center, edge and corner pieces to the yellow face
		var yellowCubeFaceValues = new Dictionary<CubePieceIndex, GameObject>();
		yellowCubeFaceValues.Add(CubePieceIndex.TopCenter, edgeYellowRed);
		yellowCubeFaceValues.Add(CubePieceIndex.CenterLeft, edgeYellowGreen);
		yellowCubeFaceValues.Add(CubePieceIndex.Center, centerYellow);
		yellowCubeFaceValues.Add(CubePieceIndex.CenterRight, edgeYellowBlue);
		yellowCubeFaceValues.Add(CubePieceIndex.BottomCenter, edgeYellowOrange);
		yellowCubeFaceValues.Add(CubePieceIndex.Topleft, cornerYellowGreenRed);
		yellowCubeFaceValues.Add(CubePieceIndex.TopRight, cornerYellowRedBlue);
		yellowCubeFaceValues.Add(CubePieceIndex.BottomLeft, cornerYellowOrangeGreen);
		yellowCubeFaceValues.Add(CubePieceIndex.BottomRight, cornerYellowBlueOrange);
		var yellowCubeFace = new CubeFace(yellowCubeFaceValues, this.transform.up * -1, centerYellow.transform.position);

		// Assign the correct center, edge and corner pieces to the centerHorizontal face
		var centerHorizontalCubeFaceValues = new Dictionary<CubePieceIndex, GameObject>();
		centerHorizontalCubeFaceValues.Add(CubePieceIndex.TopCenter, centerOrange);
		centerHorizontalCubeFaceValues.Add(CubePieceIndex.CenterLeft, centerGreen);
		centerHorizontalCubeFaceValues.Add(CubePieceIndex.Center, centerCube);
		centerHorizontalCubeFaceValues.Add(CubePieceIndex.CenterRight, centerBlue);
		centerHorizontalCubeFaceValues.Add(CubePieceIndex.BottomCenter, centerRed);
		centerHorizontalCubeFaceValues.Add(CubePieceIndex.Topleft, edgeOrangeGreen);
		centerHorizontalCubeFaceValues.Add(CubePieceIndex.TopRight, edgeBlueOrange);
		centerHorizontalCubeFaceValues.Add(CubePieceIndex.BottomLeft, edgeGreenRed);
		centerHorizontalCubeFaceValues.Add(CubePieceIndex.BottomRight, edgeRedBlue);
		var centerHorizontalCubeFace = new CubeFace(centerHorizontalCubeFaceValues, this.transform.up, centerCube.transform.position);

		// Assign the correct center, edge and corner pieces to the centerVertical face
		var centerVerticalCubeFaceValues = new Dictionary<CubePieceIndex, GameObject>();
		centerVerticalCubeFaceValues.Add(CubePieceIndex.TopCenter, centerWhite);
		centerVerticalCubeFaceValues.Add(CubePieceIndex.CenterLeft, centerOrange);
		centerVerticalCubeFaceValues.Add(CubePieceIndex.Center, centerCube);
		centerVerticalCubeFaceValues.Add(CubePieceIndex.CenterRight, centerRed);
		centerVerticalCubeFaceValues.Add(CubePieceIndex.BottomCenter, centerYellow);
		centerVerticalCubeFaceValues.Add(CubePieceIndex.Topleft, edgeWhiteOrange);
		centerVerticalCubeFaceValues.Add(CubePieceIndex.TopRight, edgeWhiteRed);
		centerVerticalCubeFaceValues.Add(CubePieceIndex.BottomLeft, edgeYellowOrange);
		centerVerticalCubeFaceValues.Add(CubePieceIndex.BottomRight, edgeYellowRed);
		var centerVerticalCubeFace = new CubeFace(centerVerticalCubeFaceValues, this.transform.right * -1, centerCube.transform.position);

		// Assign the correct center, edge and corner pieces to the centerSideways face
		var centerSidewaysCubeFaceValues = new Dictionary<CubePieceIndex, GameObject>();
		centerSidewaysCubeFaceValues.Add(CubePieceIndex.TopCenter, centerWhite);
		centerSidewaysCubeFaceValues.Add(CubePieceIndex.CenterLeft, centerGreen);
		centerSidewaysCubeFaceValues.Add(CubePieceIndex.Center, centerCube);
		centerSidewaysCubeFaceValues.Add(CubePieceIndex.CenterRight, centerBlue);
		centerSidewaysCubeFaceValues.Add(CubePieceIndex.BottomCenter, centerYellow);
		centerSidewaysCubeFaceValues.Add(CubePieceIndex.Topleft, edgeWhiteGreen);
		centerSidewaysCubeFaceValues.Add(CubePieceIndex.TopRight, edgeWhiteBlue);
		centerSidewaysCubeFaceValues.Add(CubePieceIndex.BottomLeft, edgeYellowGreen);
		centerSidewaysCubeFaceValues.Add(CubePieceIndex.BottomRight, edgeYellowBlue);
		var centerSidewaysCubeFace = new CubeFace(centerSidewaysCubeFaceValues, this.transform.forward, centerCube.transform.position);

		// Add all the completed faces to the face list
		this.cubeFaceList.Add(RotatableCubeFaceIndex.Top, whiteCubeFace);
		this.cubeFaceList.Add(RotatableCubeFaceIndex.Front, redCubeFace);
		this.cubeFaceList.Add(RotatableCubeFaceIndex.Right, blueCubeFace);
		this.cubeFaceList.Add(RotatableCubeFaceIndex.Back, orangeCubeFace);
		this.cubeFaceList.Add(RotatableCubeFaceIndex.Left, greenCubeFace);
		this.cubeFaceList.Add(RotatableCubeFaceIndex.Bottom, yellowCubeFace);
		this.cubeFaceList.Add(RotatableCubeFaceIndex.CenterHorizontal, centerHorizontalCubeFace);
		this.cubeFaceList.Add(RotatableCubeFaceIndex.CenterVertical, centerVerticalCubeFace);
		this.cubeFaceList.Add(RotatableCubeFaceIndex.CenterSideways, centerSidewaysCubeFace);
		#endregion

		// Configure the affected faces list while doing a clockwise rotation
		// The affected faces are listed in the clockwise order, the corresponding boolean value indicates if the row needs to be flipped
		// from the previous face
		#region Assign cube rotation relationship
		// Configure white face
		var whiteCubeFaceAffectedFaces = new List<CubeFace>() { redCubeFace, greenCubeFace, orangeCubeFace, blueCubeFace };
		var whiteCubeFaceAffectedRows = new List<CubeRowIndex>() { CubeRowIndex.Top, CubeRowIndex.Top, CubeRowIndex.Top, CubeRowIndex.Top };
		var whiteCubeFaceRequireToFlip = new List<bool>() { false, false, false, false };
		var whiteCubeFaceRotationRule = new CubeFaceRotationRules(whiteCubeFaceAffectedFaces, whiteCubeFaceAffectedRows, whiteCubeFaceRequireToFlip);

		// Configure red face
		var redCubeFaceAffectedFaces = new List<CubeFace>() { whiteCubeFace, blueCubeFace, yellowCubeFace, greenCubeFace };
		var redCubeFaceAffectedRows = new List<CubeRowIndex>() { CubeRowIndex.Bottom, CubeRowIndex.Left, CubeRowIndex.Top, CubeRowIndex.Right };
		var redCubeFaceRequireToFlip = new List<bool>() { true, false, true, false };
		var redCubeFaceRotationRule = new CubeFaceRotationRules(redCubeFaceAffectedFaces, redCubeFaceAffectedRows, redCubeFaceRequireToFlip);

		// Configure blue face
		var blueCubeFaceAffectedFaces = new List<CubeFace>() { whiteCubeFace, orangeCubeFace, yellowCubeFace, redCubeFace };
		var blueCubeFaceAffectedRows = new List<CubeRowIndex>() { CubeRowIndex.Right, CubeRowIndex.Left, CubeRowIndex.Right, CubeRowIndex.Right };
		var blueCubeFaceRequireToFlip = new List<bool>() { false, true, true, false };
		var blueCubeFaceRotationRule = new CubeFaceRotationRules(blueCubeFaceAffectedFaces, blueCubeFaceAffectedRows, blueCubeFaceRequireToFlip);

		// Configure orange face
		var orangeCubeFaceAffectedFaces = new List<CubeFace>() { whiteCubeFace, greenCubeFace, yellowCubeFace, blueCubeFace };
		var orangeCubeFaceAffectedRows = new List<CubeRowIndex>() { CubeRowIndex.Top, CubeRowIndex.Left, CubeRowIndex.Bottom, CubeRowIndex.Right };
		var orangeCubeFaceRequireToFlip = new List<bool>() { false, true, false, true };
		var orangeCubeFaceRotationRule = new CubeFaceRotationRules(orangeCubeFaceAffectedFaces, orangeCubeFaceAffectedRows, orangeCubeFaceRequireToFlip);

		// Configure green face
		var greenCubeFaceAffectedFaces = new List<CubeFace>() { whiteCubeFace, redCubeFace, yellowCubeFace, orangeCubeFace };
		var greenCubeFaceAffectedRows = new List<CubeRowIndex>() { CubeRowIndex.Left, CubeRowIndex.Left, CubeRowIndex.Left, CubeRowIndex.Right };
		var greenCubeFaceRequireToFlip = new List<bool>() { true, false, false, true };
		var greenCubeFaceRotationRule = new CubeFaceRotationRules(greenCubeFaceAffectedFaces, greenCubeFaceAffectedRows, greenCubeFaceRequireToFlip);

		// Configure yellow face
		var yellowCubeFaceAffectedFaces = new List<CubeFace>() { redCubeFace, blueCubeFace, orangeCubeFace, greenCubeFace };
		var yellowCubeFaceAffectedRows = new List<CubeRowIndex>() { CubeRowIndex.Bottom, CubeRowIndex.Bottom, CubeRowIndex.Bottom, CubeRowIndex.Bottom };
		var yellowCubeFaceRequireToFlip = new List<bool>() { false, false, false, false };
		var yellowCubeFaceRotationRule = new CubeFaceRotationRules(yellowCubeFaceAffectedFaces, yellowCubeFaceAffectedRows, yellowCubeFaceRequireToFlip);

		// Configure centerHorizontal face
		var centerHorizontalCubeFaceAffectedFaces = new List<CubeFace>() { orangeCubeFace, blueCubeFace, redCubeFace, greenCubeFace };
		var centerHorizontalCubeFaceAffectedRows = new List<CubeRowIndex>() { CubeRowIndex.CenterHorizontal, CubeRowIndex.CenterHorizontal, CubeRowIndex.CenterHorizontal, CubeRowIndex.CenterHorizontal };
		var centerHorizontalCubeFaceRequireToFlip = new List<bool>() { false, false, false, false };
		var centerHorizontalCubeFaceRotationRule = new CubeFaceRotationRules(centerHorizontalCubeFaceAffectedFaces, centerHorizontalCubeFaceAffectedRows, centerHorizontalCubeFaceRequireToFlip);

		// Configure centerVertical face
		var centerVerticalCubeFaceAffectedFaces = new List<CubeFace>() { whiteCubeFace, orangeCubeFace, yellowCubeFace, redCubeFace };
		var centerVerticalCubeFaceAffectedRows = new List<CubeRowIndex>() { CubeRowIndex.CenterVertical, CubeRowIndex.CenterVertical, CubeRowIndex.CenterVertical, CubeRowIndex.CenterVertical };
		var centerVerticalCubeFaceRequireToFlip = new List<bool>() { false, true, true, false };
		var centerVerticalCubeFaceRotationRule = new CubeFaceRotationRules(centerVerticalCubeFaceAffectedFaces, centerVerticalCubeFaceAffectedRows, centerVerticalCubeFaceRequireToFlip);

		// Configure centerSideways face
		var centerSidewaysCubeFaceAffectedFaces = new List<CubeFace>() { whiteCubeFace, blueCubeFace, yellowCubeFace, greenCubeFace };
		var centerSidewaysCubeFaceAffectedRows = new List<CubeRowIndex>() { CubeRowIndex.CenterHorizontal, CubeRowIndex.CenterVertical, CubeRowIndex.CenterHorizontal, CubeRowIndex.CenterVertical };
		var centerSidewaysCubeFaceRequireToFlip = new List<bool>() { true, false, true, false };
		var centerSidewaysCubeFaceRotationRule = new CubeFaceRotationRules(centerSidewaysCubeFaceAffectedFaces, centerSidewaysCubeFaceAffectedRows, centerSidewaysCubeFaceRequireToFlip);

		// Add all of the rotation rules to the rotation rule list
		this.cubeFaceRotationRulesList.Add(RotatableCubeFaceIndex.Top, whiteCubeFaceRotationRule);
		this.cubeFaceRotationRulesList.Add(RotatableCubeFaceIndex.Front, redCubeFaceRotationRule);
		this.cubeFaceRotationRulesList.Add(RotatableCubeFaceIndex.Right, blueCubeFaceRotationRule);
		this.cubeFaceRotationRulesList.Add(RotatableCubeFaceIndex.Back, orangeCubeFaceRotationRule);
		this.cubeFaceRotationRulesList.Add(RotatableCubeFaceIndex.Left, greenCubeFaceRotationRule);
		this.cubeFaceRotationRulesList.Add(RotatableCubeFaceIndex.Bottom, yellowCubeFaceRotationRule);
		this.cubeFaceRotationRulesList.Add(RotatableCubeFaceIndex.CenterHorizontal, centerHorizontalCubeFaceRotationRule);
		this.cubeFaceRotationRulesList.Add(RotatableCubeFaceIndex.CenterVertical, centerVerticalCubeFaceRotationRule);
		this.cubeFaceRotationRulesList.Add(RotatableCubeFaceIndex.CenterSideways, centerSidewaysCubeFaceRotationRule);

		#endregion

		// Test Code

		// this.jobQueue.Enqueue(new JobRotateByMouse(RotatableCubeFaceIndex.Front, this, true, false));
		// this.jobQueue.Enqueue(new JobStabilize(RotatableCubeFaceIndex.Front, this));
		// this.jobQueue.Enqueue(new JobReassignFaces(RotatableCubeFaceIndex.Front, this));
	}

	// Update is called once per frame
	void Update() 
	{
		// Update the current mouse X and Y
		this.mouseX = Input.GetAxis("Mouse X");
		this.mouseY = Input.GetAxis("Mouse Y");

		// Update the current state of the left click mouse button
		var isLeftClickDown = Input.GetMouseButtonDown(0);

		if (Input.GetMouseButtonUp(0))
		{
			this.isWaitingMouseMovement = false;
		}

		// When the job queue is not empty, execute the job
		while (jobQueue.Count > 0)
		{
			// Run the current job
			var currentJob = this.jobQueue.Peek();
			currentJob.Execute();

			// if the current job is not finished, return and do it again next update
			if (!currentJob.IsFinished())
			{
				return;
			}

			// When the current job is done, check to see if it's a rotation made by user
			if (currentJob is JobRotateByMouse)
			{
				var cubeFaceIndex = currentJob.cubeFaceIndex;
				var rotationMethod = this.cubeFaceList[cubeFaceIndex].CheckAmountRotated();
				// if the rotation method is not none, clear redo stack and push the move
				if (rotationMethod != RotationMethodIndex.None)
				{
					this.redoStack.Clear();
					this.undoStack.Push(new MoveDone(cubeFaceIndex, rotationMethod));
				}
            }
			// move onto next job
			this.jobQueue.Dequeue();
		}

		// Job queue is empty, listen for inputs
		if (isLeftClickDown || isWaitingMouseMovement)
		{
			// Left click has been clicked, but mouse has not moved very much or at all, wait for further input
			if (Math.Abs(mouseX) <= 0.3 && Math.Abs(mouseY) <= 0.3)
			{
				this.isWaitingMouseMovement = true;
			}
			else
			{
				// Mouse has moved pass the threash hold, run initialize rotation
				this.isWaitingMouseMovement = false;
				this.InitializeCurrentRotation();
			}
		}
    }
}
