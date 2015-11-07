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
	private enum RotatableCubeFaceIndex
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
	private class MoveDone
	{
		public RotatableCubeFaceIndex faceRotated;
		public RotationMethodIndex rotationMethod;
		public MoveDone(RotatableCubeFaceIndex faceRotated, RotationMethodIndex rotationMethod)
		{
			this.faceRotated = faceRotated;
			this.rotationMethod = rotationMethod;
		}
	}


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

	// List of faces and rules for the faces to rotate in
	private IDictionary<RotatableCubeFaceIndex, CubeFace> cubeFaceList;
	private IDictionary<RotatableCubeFaceIndex, CubeFaceRotationRules> cubeFaceRotationRulesList;

	// If the cube is under an ongoing rotation, and the face at which is currently being rotate
	private bool isBeingRotated;
	private bool isStabilizing;
	// If the cube is undo/redoing right now
	private bool isUndoRedoing;
	// The faces that would be the turned if the mouse if moved
	private Nullable<RotatableCubeFaceIndex> currentRotatingFaceIndex;
	// If the current rotation is revolved around mouse X or mouse Y
	private bool isMainMouseAxisX;
	// If the mouse is still and we are awaiting an input
	private bool isWaitingMouseMovement;
	// Determine the way the above face will rotate if the mouse is moved
	private bool isCurrentFaceRotatingClockwise;
	// The current mouse X and mouse Y movement;
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
		// If the cube is currently doing a undo/redo
		if (this.isUndoRedoing)
		{
			return;
		}

		// Assign flag
		this.isUndoRedoing = true;

		// Pop the move and find the opposite rotation
		var move = this.undoStack.Pop();
		var oppositeRotation = CubeControl.FindOppositeMethod(move.rotationMethod);

		this.ReconfigureFacePiecesBasedOnRotation(move.faceRotated, oppositeRotation);
		this.RotateFaceBasedOnMethod(move.faceRotated, oppositeRotation);
		this.cubeFaceList[move.faceRotated].ClearRotation();

		// Add the undid move to the redo stack
		this.redoStack.Push(move);

		// Release flag
		this.isUndoRedoing = false;
	}
	public void Redo()
	{
		// If the cube is currently doing a undo/redo
		if (this.isUndoRedoing)
		{
			return;
		}

		// Assign flag
		this.isUndoRedoing = true;

		// pop the move
		var move = this.redoStack.Pop();

		this.ReconfigureFacePiecesBasedOnRotation(move.faceRotated, move.rotationMethod);
		RotateFaceBasedOnMethod(move.faceRotated, move.rotationMethod);
		this.cubeFaceList[move.faceRotated].ClearRotation();

		// Add the redid move back into undo stack
		this.undoStack.Push(move);

		this.isUndoRedoing = false;
	}

	// Scramble the cube
	public void Scramble()
	{

	}

	// Use this for initialization
	void Start()
	{
		// Initialize values;
		this.isUndoRedoing = false;
		this.isBeingRotated = false;
		this.isStabilizing = false;
		this.currentRotatingFaceIndex = null;
		this.isCurrentFaceRotatingClockwise = false;
		this.isMainMouseAxisX = false;
		this.isWaitingMouseMovement = false;
		this.undoStack = new Stack<MoveDone>();
		this.redoStack = new Stack<MoveDone>();

		// Initialize the facelist
		this.cubeFaceList = new Dictionary<RotatableCubeFaceIndex, CubeFace>();
		this.cubeFaceRotationRulesList = new Dictionary<RotatableCubeFaceIndex, CubeFaceRotationRules>();

		//Initilizing and configuring the child cube faces into cube face
		#region
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
		#region
		// Configure white face
		var whiteCubeFaceAffectedFaces = new List<CubeFace>() { redCubeFace, greenCubeFace, orangeCubeFace, blueCubeFace };
		var whiteCubeFaceAffectedRows = new List<CubeRowIndex>(){CubeRowIndex.Top,CubeRowIndex.Top,CubeRowIndex.Top, CubeRowIndex.Top};
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
		var orangeCubeFaceRequireToFlip = new List<bool>() { true, false, true, false};
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
		var centerHorizontalCubeFaceAffectedFaces = new List<CubeFace>() { orangeCubeFace, blueCubeFace, redCubeFace, greenCubeFace};
		var centerHorizontalCubeFaceAffectedRows = new List<CubeRowIndex>() { CubeRowIndex.CenterHorizontal, CubeRowIndex.CenterHorizontal, CubeRowIndex.CenterHorizontal, CubeRowIndex.CenterHorizontal };
		var centerHorizontalCubeFaceRequireToFlip = new List<bool>() { false, false, false, false };
		var centerHorizontalCubeFaceRotationRule = new CubeFaceRotationRules(centerHorizontalCubeFaceAffectedFaces, centerHorizontalCubeFaceAffectedRows, centerHorizontalCubeFaceRequireToFlip);

		// Configure centerVertical face
		var centerVerticalCubeFaceAffectedFaces = new List<CubeFace>() { whiteCubeFace, orangeCubeFace, yellowCubeFace, redCubeFace };
		var centerVerticalCubeFaceAffectedRows = new List<CubeRowIndex>() { CubeRowIndex.CenterVertical, CubeRowIndex.CenterVertical, CubeRowIndex.CenterVertical, CubeRowIndex.CenterVertical };
		var centerVerticalCubeFaceRequireToFlip = new List<bool>() { false, true, true, false};
		var centerVerticalCubeFaceRotationRule = new CubeFaceRotationRules(centerVerticalCubeFaceAffectedFaces, centerVerticalCubeFaceAffectedRows, centerVerticalCubeFaceRequireToFlip);

		// Configure centerSideways face
		var centerSidewaysCubeFaceAffectedFaces = new List<CubeFace>() { whiteCubeFace, blueCubeFace, yellowCubeFace, greenCubeFace };
		var centerSidewaysCubeFaceAffectedRows = new List<CubeRowIndex>() { CubeRowIndex.CenterHorizontal, CubeRowIndex.CenterVertical, CubeRowIndex.CenterHorizontal, CubeRowIndex.CenterVertical };
		var centerSidewaysCubeFaceRequireToFlip = new List<bool>() { true, false, true, false};
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
		

		//this.RotateFace(RotatableCubeFaceIndex.CenterVertical, RotationMethodIndex.Clockwise);
		//this.cubeFaceList[RotatableCubeFaceIndex.CenterVertical].Rotate(90);

		//this.RotateFace(RotatableCubeFaceIndex.CenterSideways, RotationMethodIndex.Clockwise);
		//this.cubeFaceList[RotatableCubeFaceIndex.CenterSideways].Rotate(90);

		//this.RotateFace(RotatableCubeFaceIndex.CenterHorizontal, RotationMethodIndex.Clockwise);
		//this.cubeFaceList[RotatableCubeFaceIndex.CenterHorizontal].Rotate(90);
	}

	// Rotate the given face with the given method
	private void ReconfigureFacePiecesBasedOnRotation(RotatableCubeFaceIndex faceIndex, RotationMethodIndex rotationMethod)
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
			// Rotate the blocks themselves
			// this.cubeFaceList[faceIndex].Rotate(90);
			// If half circle, do it again
			if (rotationMethod == RotationMethodIndex.HalfCircle)
			{
				this.ReconfigureFacePiecesBasedOnRotation(faceIndex, RotationMethodIndex.Clockwise);
			}
		}
		else if (rotationMethod == RotationMethodIndex.Counterclockwise)
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
					affectedFaceList.RequiredToFlipFromPrevious[i+1>3?0:i+1]);
			}
			// Rotate the blocks themselves
			// this.cubeFaceList[faceIndex].Rotate(-90);
		}
		// Reassign the 3 center layers to the new configuration of the cube
		#region
		// Change the center horizontal layer
		cubeFaceList[RotatableCubeFaceIndex.CenterHorizontal].ReplaceRow(CubeRowIndex.Top, cubeFaceList[RotatableCubeFaceIndex.Back].GetPiecesInRow(CubeRowIndex.CenterHorizontal), true);
		cubeFaceList[RotatableCubeFaceIndex.CenterHorizontal].ReplaceRow(CubeRowIndex.Bottom, cubeFaceList[RotatableCubeFaceIndex.Front].GetPiecesInRow(CubeRowIndex.CenterHorizontal), false);
		cubeFaceList[RotatableCubeFaceIndex.CenterHorizontal].CubePieceList[CubePieceIndex.CenterLeft] = cubeFaceList[RotatableCubeFaceIndex.Left].CubePieceList[CubePieceIndex.Center];
		cubeFaceList[RotatableCubeFaceIndex.CenterHorizontal].CubePieceList[CubePieceIndex.CenterRight] = cubeFaceList[RotatableCubeFaceIndex.Right].CubePieceList[CubePieceIndex.Center];
		// Change the center vertical layer
		cubeFaceList[RotatableCubeFaceIndex.CenterVertical].ReplaceRow(CubeRowIndex.Top, cubeFaceList[RotatableCubeFaceIndex.Top].GetPiecesInRow(CubeRowIndex.CenterVertical), false);
		cubeFaceList[RotatableCubeFaceIndex.CenterVertical].ReplaceRow(CubeRowIndex.Bottom, cubeFaceList[RotatableCubeFaceIndex.Bottom].GetPiecesInRow(CubeRowIndex.CenterVertical), true);
		cubeFaceList[RotatableCubeFaceIndex.CenterVertical].CubePieceList[CubePieceIndex.CenterLeft] = cubeFaceList[RotatableCubeFaceIndex.Back].CubePieceList[CubePieceIndex.Center];
		cubeFaceList[RotatableCubeFaceIndex.CenterVertical].CubePieceList[CubePieceIndex.CenterRight] = cubeFaceList[RotatableCubeFaceIndex.Front].CubePieceList[CubePieceIndex.Center];
		// Change the center vertical layer
		cubeFaceList[RotatableCubeFaceIndex.CenterSideways].ReplaceRow(CubeRowIndex.Top, cubeFaceList[RotatableCubeFaceIndex.Top].GetPiecesInRow(CubeRowIndex.CenterHorizontal), false);
		cubeFaceList[RotatableCubeFaceIndex.CenterSideways].ReplaceRow(CubeRowIndex.Bottom, cubeFaceList[RotatableCubeFaceIndex.Bottom].GetPiecesInRow(CubeRowIndex.CenterHorizontal), false);
		cubeFaceList[RotatableCubeFaceIndex.CenterSideways].CubePieceList[CubePieceIndex.CenterLeft] = cubeFaceList[RotatableCubeFaceIndex.Left].CubePieceList[CubePieceIndex.Center];
		cubeFaceList[RotatableCubeFaceIndex.CenterSideways].CubePieceList[CubePieceIndex.CenterRight] = cubeFaceList[RotatableCubeFaceIndex.Right].CubePieceList[CubePieceIndex.Center];
		#endregion
	}

	// Find the opposite move
	private static RotationMethodIndex FindOppositeMethod(RotationMethodIndex rotationMethodIndex)
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

	// Determine if the given game object is on the surface of the cube piece
	private bool IsPieceOnSurfaceOfCubeFace(GameObject givenPieceFace, RotatableCubeFaceIndex cubeFaceIndex)
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
	private void RotateFaceBasedOnMethod(RotatableCubeFaceIndex cubeFaceIndex, RotationMethodIndex rotationMethod)
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

	// Determine the face currently being rotated, the direction and relation to mouse X or Y
	private void InitializeCurrentRotation()
	{
		// Get the gameObject that got clicked on with a ray
		GameObject faceClicked = null;
		Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
		RaycastHit hit;
		if (Physics.Raycast(ray, out hit))
		{
			faceClicked = hit.collider.gameObject;
		}
		// If nothing is clicked, return
		if (faceClicked == null || faceClicked.transform.parent == null)
		{
			return;
		}

		// Start the rotation
		this.isBeingRotated = true;

		// Find the parent of the piece
		var pieceClicked = faceClicked.transform.parent.gameObject;

		// Find the position of the main camera and use its X and Z values to determine the rotation
		var cameraX = mainCamera.transform.position.x;
		var cameraZ = mainCamera.transform.position.z;

		// Find if the mouse is moving more in X axis or Y axis to determine the main axis
		this.isMainMouseAxisX = (Math.Abs(this.mouseX) > Math.Abs(this.mouseY));

		// Define the current faces relative to the camera
		RotatableCubeFaceIndex currentFrontFace;
		RotatableCubeFaceIndex currentRightFace;
		RotatableCubeFaceIndex currentBackFace;
		RotatableCubeFaceIndex currentLeftFace;
		//RotatableCubeFaceIndex currentCenterHorizontal;
		RotatableCubeFaceIndex currentCenterVertical;
		RotatableCubeFaceIndex currentCenterSideways;
		// When the current front is orange, the center veritcal is reversed. use the following bool values to indicete 
		// if the current veritcal and sideways center need to be reversed
		bool IsCurrentCenterVerticalReversed = false;
		bool IsCurrentCenterSidewaysReversed = false;

		// Assign the current cube faces using the camera position
		#region
		if ((cameraZ > 0) && (Math.Abs(cameraZ) > Math.Abs(cameraX)))
		{
			currentFrontFace = RotatableCubeFaceIndex.Front;
			currentRightFace = RotatableCubeFaceIndex.Right;
			currentBackFace = RotatableCubeFaceIndex.Back;
			currentLeftFace = RotatableCubeFaceIndex.Left;
			//currentCenterHorizontal = RotatableCubeFaceIndex.CenterHorizontal;
			currentCenterVertical = RotatableCubeFaceIndex.CenterVertical;
			currentCenterSideways = RotatableCubeFaceIndex.CenterSideways;
			IsCurrentCenterVerticalReversed = false;
			IsCurrentCenterSidewaysReversed = false;
			
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
			IsCurrentCenterVerticalReversed = true;
			IsCurrentCenterSidewaysReversed = true;
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
			IsCurrentCenterVerticalReversed = false;
			IsCurrentCenterSidewaysReversed = false;
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
			IsCurrentCenterVerticalReversed = false;
			IsCurrentCenterSidewaysReversed = true;
		}
		#endregion

		// If the piece is contained in each layer
		var isPieceInTop = this.cubeFaceList[RotatableCubeFaceIndex.Top].Contains(pieceClicked);
		var isPieceInBottom = this.cubeFaceList[RotatableCubeFaceIndex.Bottom].Contains(pieceClicked);
		var isPieceInCurrentLeft = this.cubeFaceList[currentLeftFace].Contains(pieceClicked);
		var isPieceInCurrentRight = this.cubeFaceList[currentRightFace].Contains(pieceClicked);
		var isPieceInCurrentFront = this.cubeFaceList[currentFrontFace].Contains(pieceClicked);
		var isPieceInCurrentBack = this.cubeFaceList[currentBackFace].Contains(pieceClicked);

		// When dealing with the clicked face being on top
		#region
		if (this.IsPieceOnSurfaceOfCubeFace(faceClicked, RotatableCubeFaceIndex.Top))
		{
			// When moving mouse side ways, rotate the respective faces
			if (isMainMouseAxisX)
			{
				if (isPieceInCurrentBack)
				{
					this.currentRotatingFaceIndex = currentBackFace;
					this.isCurrentFaceRotatingClockwise = false;
					return;
				}
				if (isPieceInCurrentFront)
				{
					this.currentRotatingFaceIndex = currentFrontFace;
					this.isCurrentFaceRotatingClockwise = true;
					return;
				}
				this.currentRotatingFaceIndex = currentCenterSideways;
				this.isCurrentFaceRotatingClockwise = !IsCurrentCenterSidewaysReversed;
				return;
			}
			// Whenthe mouse if moving up and down, rotate the respective faces
			if (isPieceInCurrentLeft)
			{
				this.currentRotatingFaceIndex = currentLeftFace;
				this.isCurrentFaceRotatingClockwise = false;
				return;
			}
			if (isPieceInCurrentRight)
			{
				this.currentRotatingFaceIndex = currentRightFace;
				this.isCurrentFaceRotatingClockwise = true;
				return;
			}
			this.currentRotatingFaceIndex = currentCenterVertical;
			this.isCurrentFaceRotatingClockwise = !IsCurrentCenterVerticalReversed;
			return;
		}
		#endregion

		// When dealing with the clicked face bing on front
		#region
		if (this.IsPieceOnSurfaceOfCubeFace(faceClicked, currentFrontFace))
		{
			// When moving mouse side ways, rotate the respective faces
			if (isMainMouseAxisX)
			{
				if (isPieceInTop)
				{
					this.currentRotatingFaceIndex = RotatableCubeFaceIndex.Top;
					this.isCurrentFaceRotatingClockwise = false;
					return;
				}
				if (isPieceInBottom)
				{
					this.currentRotatingFaceIndex = RotatableCubeFaceIndex.Bottom;
					this.isCurrentFaceRotatingClockwise = true;
					return;
				}
				this.currentRotatingFaceIndex = RotatableCubeFaceIndex.CenterHorizontal;
				this.isCurrentFaceRotatingClockwise = false;
				return;
			}
			// Whenthe mouse if moving up and down, rotate the respective faces
			if (isPieceInCurrentLeft)
			{
				this.currentRotatingFaceIndex = currentLeftFace;
				this.isCurrentFaceRotatingClockwise = false;
				return;
			}
			if (isPieceInCurrentRight)
			{
				this.currentRotatingFaceIndex = currentRightFace;
				this.isCurrentFaceRotatingClockwise = true;
				return;
			}
			this.currentRotatingFaceIndex = currentCenterVertical;
			this.isCurrentFaceRotatingClockwise = !IsCurrentCenterVerticalReversed;
			return;
		}
		#endregion

		// When dealing with the clicked face bing on left
		#region
		if (this.IsPieceOnSurfaceOfCubeFace(faceClicked, currentLeftFace))
		{
			// When moving mouse side ways, rotate the respective faces
			if (isMainMouseAxisX)
			{
				if (isPieceInTop)
				{
					this.currentRotatingFaceIndex = RotatableCubeFaceIndex.Top;
					this.isCurrentFaceRotatingClockwise = false;
					return;
				}
				if (isPieceInBottom)
				{
					this.currentRotatingFaceIndex = RotatableCubeFaceIndex.Bottom;
					this.isCurrentFaceRotatingClockwise = true;
					return;
				}
				this.currentRotatingFaceIndex = RotatableCubeFaceIndex.CenterHorizontal;
				this.isCurrentFaceRotatingClockwise = false;
				return;
			}
			// Whenthe mouse if moving up and down, rotate the respective faces
			if (isPieceInCurrentFront)
			{
				this.currentRotatingFaceIndex = currentFrontFace;
				this.isCurrentFaceRotatingClockwise = true;
				return;
			}
			if (isPieceInCurrentBack)
			{
				this.currentRotatingFaceIndex = currentBackFace;
				this.isCurrentFaceRotatingClockwise = false;
				return;
			}
			this.currentRotatingFaceIndex = currentCenterSideways;
			this.isCurrentFaceRotatingClockwise = !IsCurrentCenterSidewaysReversed;
			return;
		}
		#endregion

		// When dealing with the clicked face bing on right
		#region
		if (this.IsPieceOnSurfaceOfCubeFace(faceClicked, currentRightFace))
		{
			// When moving mouse side ways, rotate the respective faces
			if (isMainMouseAxisX)
			{
				if (isPieceInTop)
				{
					this.currentRotatingFaceIndex = RotatableCubeFaceIndex.Top;
					this.isCurrentFaceRotatingClockwise = false;
					return;
				}
				if (isPieceInBottom)
				{
					this.currentRotatingFaceIndex = RotatableCubeFaceIndex.Bottom;
					this.isCurrentFaceRotatingClockwise = true;
					return;
				}
				this.currentRotatingFaceIndex = RotatableCubeFaceIndex.CenterHorizontal;
				this.isCurrentFaceRotatingClockwise = false;
				return;
			}
			// Whenthe mouse if moving up and down, rotate the respective faces
			if (isPieceInCurrentFront)
			{
				this.currentRotatingFaceIndex = currentFrontFace;
				this.isCurrentFaceRotatingClockwise = false;
				return;
			}
			if (isPieceInCurrentBack)
			{
				this.currentRotatingFaceIndex = currentBackFace;
				this.isCurrentFaceRotatingClockwise = true;
				return;
			}
			this.currentRotatingFaceIndex = currentCenterSideways;
			this.isCurrentFaceRotatingClockwise = !IsCurrentCenterSidewaysReversed;
			return;
		}
		#endregion

		// When dealing with the clicked face being on bottom
		#region
		if (this.IsPieceOnSurfaceOfCubeFace(faceClicked, RotatableCubeFaceIndex.Bottom))
		{
			// When moving mouse side ways, rotate the respective faces
			if (isMainMouseAxisX)
			{
				if (isPieceInCurrentBack)
				{
					this.currentRotatingFaceIndex = currentBackFace;
					this.isCurrentFaceRotatingClockwise = true;
					return;
				}
				if (isPieceInCurrentFront)
				{
					this.currentRotatingFaceIndex = currentFrontFace;
					this.isCurrentFaceRotatingClockwise = false;
					return;
				}
				this.currentRotatingFaceIndex = currentCenterSideways;
				this.isCurrentFaceRotatingClockwise = !IsCurrentCenterSidewaysReversed;
				return;
			}
			// Whenthe mouse if moving up and down, rotate the respective faces
			if (isPieceInCurrentLeft)
			{
				this.currentRotatingFaceIndex = currentLeftFace;
				this.isCurrentFaceRotatingClockwise = false;
				return;
			}
			if (isPieceInCurrentRight)
			{
				this.currentRotatingFaceIndex = currentRightFace;
				this.isCurrentFaceRotatingClockwise = true;
				return;
			}
			this.currentRotatingFaceIndex = currentCenterVertical;
			this.isCurrentFaceRotatingClockwise = !IsCurrentCenterVerticalReversed;
			return;
		}
		#endregion
	}
	// Update is called once per frame
	void Update() 
	{
		//this.RotateFace(RotatableCubeFaceIndex.CenterVertical, RotationMethodIndex.Clockwise);
		//this.cubeFaceList[RotatableCubeFaceIndex.CenterVertical].Rotate(90);

		this.mouseX = Input.GetAxis("Mouse X");
		this.mouseY = Input.GetAxis("Mouse Y");

		// If no cube face is being rotated, set the bool to false
		if (this.currentRotatingFaceIndex == null)
		{
			this.isBeingRotated = false;
		}

		// If the cube is waiting for mouse movement, but the left click is let go, cancel the waiting
		if (isWaitingMouseMovement && Input.GetMouseButtonUp(0))
		{
			isWaitingMouseMovement = false;
		}

		// If the cube is not rotating or stabilizing then rotate it based on mouse input
		if (!this.isBeingRotated && !this.isStabilizing && (Input.GetMouseButtonDown(0) || isWaitingMouseMovement))
		{
			// If the mouse if barely moving, do not initialize rotation
			if (Math.Abs(mouseX) < 0.5 && Math.Abs(mouseY) < 0.5)
			{
				this.isWaitingMouseMovement = true;
			}
			else
			{
				// Initialize the rotation
				this.isWaitingMouseMovement = false;
				this.InitializeCurrentRotation();
			}
		}

		// The cube has started rotating, and the user is continuing the rotation process
		if (this.isBeingRotated)
		{
			if (this.currentRotatingFaceIndex == null)
			{
				return;
			}
			if (this.isMainMouseAxisX)
			{
				var degree =  mouseX * this.rotationVelocity;
				if (this.isCurrentFaceRotatingClockwise)	
				{
					this.cubeFaceList[currentRotatingFaceIndex.Value].Rotate(degree);
				}
				//if (!this.isCurrentFaceRotatingClockwise)
				else
				{
					this.cubeFaceList[currentRotatingFaceIndex.Value].Rotate(-degree);
				}
			}
			//if (!this.isMainMouseAxisX)
			else
			{
				var degree = mouseY * this.rotationVelocity;
				if (this.isCurrentFaceRotatingClockwise)
				{
					this.cubeFaceList[currentRotatingFaceIndex.Value].Rotate(degree);
				}
				//if (!this.isCurrentFaceRotatingClockwise)
				else
				{
					this.cubeFaceList[currentRotatingFaceIndex.Value].Rotate(-degree);
				}
			}
		}
		// The cube was being rotated, but now the mouse key is released. Start stabilization process
		if (this.isBeingRotated && Input.GetMouseButtonUp(0))
		{
			if (this.currentRotatingFaceIndex != null)
			{
				this.isBeingRotated = false;
				this.isStabilizing = true;
			}
		}
		// The cube is stabilizing, but not yet stabilized
		if (this.isStabilizing)
		{
			var currentRotatingFace = this.cubeFaceList[currentRotatingFaceIndex.Value];

			currentRotatingFace.Stabilize(stabilizeVelocity);

			if (currentRotatingFace.IsStabilized())
			{
				// the cube is done stabilizing, get the amount rotated, apply them to the faces and then clear the rotation
				var rotationMethod = this.cubeFaceList[currentRotatingFaceIndex.Value].CheckAmountRotated();
				this.ReconfigureFacePiecesBasedOnRotation(currentRotatingFaceIndex.Value, rotationMethod);
				currentRotatingFace.ClearRotation();

				// If an actual move is done, add it to the undo stack and clear the redo stack
				if (rotationMethod != RotationMethodIndex.None)
				{
					this.redoStack.Clear();
					this.undoStack.Push(new MoveDone(currentRotatingFaceIndex.Value, rotationMethod));
				}

				this.currentRotatingFaceIndex = null;
				this.isStabilizing = false;
			}
		}
	}
}
