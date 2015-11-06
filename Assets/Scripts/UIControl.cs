using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UIControl : MonoBehaviour {

	// The current cube
	public GameObject CubeTarget;

	// The 3 buttons
	private GameObject scrambleButton;
	private GameObject undoButton;
	private GameObject redoButton;

	// The cube control of the cube
	private CubeControl CubeTargetControl;

	// Use this for initialization
	void Start () 
	{
		this.scrambleButton = this.transform.FindChild("ScrambleButton").gameObject;
		this.undoButton = this.transform.FindChild("UndoButton").gameObject;
		this.redoButton = this.transform.FindChild("RedoButton").gameObject;

		undoButton.GetComponent<Button>().interactable = false;
		redoButton.GetComponent<Button>().interactable = false;

		CubeTargetControl = CubeTarget.GetComponent<CubeControl>();	
	}
	
	// Update is called once per frame
	void Update () 
	{
		// Change the state of the undo/redo button if available
		if (CubeTargetControl.HasRedoStepsLeft())
		{
			redoButton.GetComponent<Button>().interactable = true;
		}
		else
		{
			redoButton.GetComponent<Button>().interactable = false;
		}
		if (CubeTargetControl.HasUndoStepsLeft())
		{
			undoButton.GetComponent<Button>().interactable = true;
		}
		else
		{
			undoButton.GetComponent<Button>().interactable = false;
		}
	}
}
